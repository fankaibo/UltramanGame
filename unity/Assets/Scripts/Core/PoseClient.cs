using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace UltramanGame.Core
{
    // No Unity calls on this worker. Only a bounded latest-message slot crosses threads.
    public sealed class PoseClient : IDisposable
    {
        const int MaxLineBytes=32768;
        readonly int port;
        readonly Thread worker;
        readonly Func<long> consumptionClock;
        volatile bool stopping;
        TcpClient client;
        string latest;
        volatile string status="正在连接动作识别服务";
        public string Status => status;
        public PoseClient(int port=8765,Func<long> consumptionClock=null)
        { this.port=port;this.consumptionClock=consumptionClock??(()=>DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()); worker=new Thread(Run) { IsBackground=true, Name="Local pose bridge" }; worker.Start(); }
        public string TakeLatest() => Interlocked.Exchange(ref latest,null);
        // Preview decoding or a descheduled main thread may take longer than
        // the allowed clock skew. Pair the packet with its consumption time,
        // never the start of the render frame or a replacement capture stamp.
        public PoseFrame TakeLatestPose(Func<string,PoseFrame> deserialize,out long observedMs)
        {
            observedMs=0;
            try {var line=TakeLatest();return line==null?null:deserialize(line);}
            finally {observedMs=consumptionClock();}
        }
        void Run()
        {
            while(!stopping)
            {
                try
                {
                    using(var connection=new TcpClient())
                    {
                        client=connection;
                        connection.NoDelay=true;
                        connection.ReceiveTimeout=1500;
                        connection.Connect("127.0.0.1",port);
                        if(stopping) break;
                        status="等待摄像头画面";
                        using(var stream=connection.GetStream())
                        using(var line=new MemoryStream())
                        {
                            var buffer=new byte[8192];
                            int count;
                            while(!stopping && (count=stream.Read(buffer,0,buffer.Length))>0)
                            {
                                for(int i=0;i<count;i++)
                                {
                                    if(buffer[i]==10)
                                    {
                                        if(line.Length>0)
                                        {Interlocked.Exchange(ref latest,Encoding.UTF8.GetString(line.ToArray()));status="动作服务已连接";}
                                        line.SetLength(0);
                                    }
                                    else
                                    {
                                        if(line.Length>=MaxLineBytes) throw new IOException("Pose line too large");
                                        line.WriteByte(buffer[i]);
                                    }
                                }
                            }
                        }
                    }
                }
                catch(Exception e) when(e is IOException || e is SocketException || e is ObjectDisposedException)
                { status="等待动作识别服务（127.0.0.1）"; }
                finally { status="等待动作识别服务重新连接";client=null; Interlocked.Exchange(ref latest,null); }
                for(int i=0;i<10 && !stopping;i++) Thread.Sleep(100);
            }
        }
        public void Dispose()
        { stopping=true; client?.Close(); worker.Join(2000); }
    }
}
