using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace UltramanGame.Core
{
    public sealed class PreviewFrame
    {
        public const int MaxJpegBytes=128*1024, FreshnessMs=750;
        public long CapturedMs;
        public bool Synthetic;
        public int Quality;
        public byte[] Jpeg;
        public bool Fresh(long now) => CapturedMs>0 && CapturedMs<=now+50 && now-CapturedMs<=FreshnessMs;

        static void ReadExactly(Stream stream,byte[] buffer)
        {
            int offset=0;
            while(offset<buffer.Length)
            {
                int n=stream.Read(buffer,offset,buffer.Length-offset);
                if(n==0) throw new EndOfStreamException();
                offset+=n;
            }
        }
        public static PreviewFrame Read(Stream stream)
        {
            var h=new byte[18];ReadExactly(stream,h);
            if(h[0]!='U'||h[1]!='G'||h[2]!='P'||h[3]!='1'||h[16]>1||h[17]>2)
                throw new IOException("Invalid preview header");
            long stamp=0;for(int i=4;i<12;i++) stamp=(stamp<<8)|h[i];
            uint length=0;for(int i=12;i<16;i++) length=(length<<8)|h[i];
            if(stamp<0 || length<4 || length>MaxJpegBytes) throw new IOException("Invalid preview size or timestamp");
            var jpeg=new byte[(int)length];ReadExactly(stream,jpeg);
            if(!SmallJpeg(jpeg)) throw new IOException("Invalid preview JPEG or dimensions");
            return new PreviewFrame { CapturedMs=stamp,Synthetic=h[16]==1,Quality=h[17],Jpeg=jpeg };
        }
        // Check the encoded dimensions before Unity allocates a decoded texture.
        static bool SmallJpeg(byte[] data)
        {
            if(data[0]!=255 || data[1]!=216 || data[data.Length-2]!=255 || data[data.Length-1]!=217) return false;
            int p=2;
            while(p+4<=data.Length)
            {
                if(data[p++]!=255) return false;
                while(p<data.Length && data[p]==255) p++;
                if(p+3>data.Length) return false;
                int marker=data[p++],length=(data[p]<<8)|data[p+1];
                if(length<2 || p+length>data.Length) return false;
                if(marker==192) // OpenCV emits baseline JPEG (SOF0).
                {
                    if(length<8 || data[p+2]!=8) return false;
                    int height=(data[p+3]<<8)|data[p+4],width=(data[p+5]<<8)|data[p+6];
                    return width>0 && width<=320 && height>0 && height<=240;
                }
                if(marker==218 || marker==217) return false;
                p+=length;
            }
            return false;
        }
    }

    // JPEG traffic has its own socket and bounded latest-frame slot, separate from actions.
    public sealed class PreviewClient : IDisposable
    {
        readonly int port;
        readonly Thread worker;
        volatile bool stopping;
        TcpClient client;
        PreviewFrame latest;
        volatile string status="正在连接取景画面";
        public string Status => status;
        public PreviewClient(int port=8766)
        { this.port=port;worker=new Thread(Run) { IsBackground=true,Name="Local camera preview" };worker.Start(); }
        public PreviewFrame TakeLatest() => Interlocked.Exchange(ref latest,null);
        void Run()
        {
            while(!stopping)
            {
                try
                {
                    using(var connection=new TcpClient())
                    {
                        client=connection;connection.NoDelay=true;connection.ReceiveTimeout=1500;
                        connection.Connect("127.0.0.1",port);
                        if(stopping) break;
                        status="等待新的相机画面";
                        using(var stream=connection.GetStream())
                            while(!stopping) Interlocked.Exchange(ref latest,PreviewFrame.Read(stream));
                    }
                }
                catch(Exception e) when(e is IOException || e is SocketException || e is ObjectDisposedException)
                { status="等待相机预览服务"; }
                finally { client=null;Interlocked.Exchange(ref latest,null); }
                for(int i=0;i<10 && !stopping;i++) Thread.Sleep(100);
            }
        }
        public void Dispose() { stopping=true;client?.Close();worker.Join(2000); }
    }
}
