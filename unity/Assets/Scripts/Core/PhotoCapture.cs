using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace UltramanGame.Core
{
    // Uses monotonic time supplied by the caller. Each explicit Begin can yield at most one save.
    public sealed class PhotoCountdown
    {
        public const double Seconds=5;
        public bool Running {get;private set;}
        double started;
        public void Begin(double now) {if(Running)return;started=now;Running=true;}
        public int Remaining(double now) => Running?Math.Max(1,(int)Math.Ceiling(Seconds-(now-started))):0;
        public bool TakeShot(double now)
        {if(!Running||now-started<Seconds)return false;Running=false;return true;}
        public void Cancel() {Running=false;}
    }

    public enum PhotoStage {Closed,Framing,Countdown,Review}

    public sealed class PhotoSession
    {
        readonly PhotoCountdown countdown=new PhotoCountdown();
        public PhotoStage Stage {get;private set;}
        public bool Missed {get;private set;}
        public void Open() {countdown.Cancel();Missed=false;Stage=PhotoStage.Framing;}
        public bool Begin(double now,bool freshPerson)
        {
            if(Stage!=PhotoStage.Framing||!freshPerson)return false;
            Missed=false;countdown.Begin(now);Stage=PhotoStage.Countdown;return true;
        }
        public int Remaining(double now)=>countdown.Remaining(now);
        public bool Tick(double now,bool freshPerson)
        {
            if(Stage!=PhotoStage.Countdown||!countdown.TakeShot(now))return false;
            Missed=!freshPerson;Stage=freshPerson?PhotoStage.Review:PhotoStage.Framing;
            return freshPerson;
        }
        public void Back()
        {
            countdown.Cancel();Missed=false;
            Stage=Stage==PhotoStage.Countdown?PhotoStage.Framing:PhotoStage.Closed;
        }
        public void Close() {countdown.Cancel();Missed=false;Stage=PhotoStage.Closed;}
    }

    public sealed class PhotoFrame
    {
        public const int MaxBytes=2*1024*1024;
        public long CapturedMs;
        public bool Synthetic,Present;
        public byte[] Png;
        public bool Fresh(long now) => CapturedMs>0&&CapturedMs<=now+50&&now-CapturedMs<=750;
        static void ReadExactly(Stream stream,byte[] b)
        {int offset=0;while(offset<b.Length) {int n=stream.Read(b,offset,b.Length-offset);if(n==0)throw new EndOfStreamException();offset+=n;}}
        static uint UInt(byte[] b,int p) => ((uint)b[p]<<24)|((uint)b[p+1]<<16)|((uint)b[p+2]<<8)|b[p+3];
        public static PhotoFrame Read(Stream stream)
        {
            var h=new byte[18];ReadExactly(stream,h);
            if(h[0]!='U'||h[1]!='G'||h[2]!='F'||h[3]!='1'||h[16]>1||h[17]>1)throw new IOException("Invalid photo header");
            long stamp=0;for(int i=4;i<12;i++)stamp=(stamp<<8)|h[i];
            uint size=UInt(h,12);
            if(stamp<=0||size<33||size>MaxBytes)throw new IOException("Invalid photo timestamp or length");
            var png=new byte[(int)size];ReadExactly(stream,png);
            byte[] signature={137,80,78,71,13,10,26,10};
            for(int i=0;i<8;i++)if(png[i]!=signature[i])throw new IOException("Invalid PNG");
            uint w=UInt(png,16),height=UInt(png,20);
            if(UInt(png,8)!=13||png[12]!='I'||png[13]!='H'||png[14]!='D'||png[15]!='R'||w<1||w>640||height<1||height>480||png[24]!=8||png[25]!=6)
                throw new IOException("Invalid photo PNG dimensions or channels");
            return new PhotoFrame {CapturedMs=stamp,Synthetic=h[16]==1,Present=h[17]==1,Png=png};
        }
    }

    // Exists only while the user is in the photo screen. A bounded latest slot cannot build a frame backlog.
    public sealed class PhotoClient : IDisposable
    {
        readonly Thread worker;
        readonly int port;
        volatile bool stopping;
        TcpClient connection;
        PhotoFrame latest;
        public string Status {get;private set;}="连接中";
        public PhotoClient(int port)
        {this.port=port;worker=new Thread(Run){IsBackground=true,Name="Local photo cutout"};worker.Start();}
        public PhotoFrame TakeLatest()=>Interlocked.Exchange(ref latest,null);
        void Run()
        {
            while(!stopping)
            {
                try
                {
                    using(var client=new TcpClient())
                    {
                        connection=client;client.NoDelay=true;client.ReceiveTimeout=15000;
                        client.Connect("127.0.0.1",port);
                        if(stopping)break;
                        Status="已连接，等待人像";
                        using(var stream=client.GetStream())while(!stopping)
                        {Interlocked.Exchange(ref latest,PhotoFrame.Read(stream));Status="人像流正常";}
                    }
                }
                catch(Exception e) when(e is IOException||e is SocketException||e is ObjectDisposedException) {Status="正在重连人像流";}
                finally {connection=null;Interlocked.Exchange(ref latest,null);}
                for(int i=0;i<10&&!stopping;i++)Thread.Sleep(50);
            }
        }
        public void Dispose() {stopping=true;connection?.Close();worker.Join(1000);Interlocked.Exchange(ref latest,null);}
    }

    public static class PhotoFiles
    {
        public static string Downloads => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads");
        public static string Save(string folder,byte[] png,bool synthetic=false)
        {
            Directory.CreateDirectory(folder);
            string name=(synthetic?"合成测试_":"")+"迪迦合照_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+"_"+Guid.NewGuid().ToString("N").Substring(0,8)+".png";
            string path=Path.Combine(folder,name),pending=path+".tmp";
            try
            {
                using(var file=new FileStream(pending,FileMode.CreateNew,FileAccess.Write,FileShare.None))file.Write(png,0,png.Length);
                File.Move(pending,path);return path;
            }
            finally {if(File.Exists(pending))File.Delete(pending);}
        }
    }
}
