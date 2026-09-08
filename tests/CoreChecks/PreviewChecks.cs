using System;
using System.IO;
using System.Threading;
using UltramanGame.Core;

static class PreviewChecks
{
    // Minimal JPEG header fixture for transport/parser tests; rendering uses real JPEGs in Unity.
    static byte[] Packet(int width=320)
    {
        var jpeg=new byte[] {255,216,255,192,0,11,8,0,240,(byte)(width>>8),(byte)width,1,1,17,0,255,217};
        var packet=new byte[18+jpeg.Length];
        packet[0]=85;packet[1]=71;packet[2]=80;packet[3]=49;
        long stamp=123456789;for(int i=11;i>=4;i--) { packet[i]=(byte)stamp;stamp>>=8; }
        packet[15]=(byte)jpeg.Length;packet[16]=1;packet[17]=2;
        Array.Copy(jpeg,0,packet,18,jpeg.Length);return packet;
    }
    sealed class FragmentedStream : MemoryStream
    {
        public FragmentedStream(byte[] data):base(data) { }
        public override int Read(byte[] buffer,int offset,int count) => base.Read(buffer,offset,Math.Min(3,count));
    }
    static bool Rejected(byte[] packet)
    { try { PreviewFrame.Read(new MemoryStream(packet));return false; } catch(IOException) { return true; } }
    public static void Run(Action<bool,string> check)
    {
        var packet=Packet();
        var pair=new byte[packet.Length*2];Array.Copy(packet,pair,packet.Length);Array.Copy(packet,0,pair,packet.Length,packet.Length);
        using(var stream=new FragmentedStream(pair))
        {
            var first=PreviewFrame.Read(stream);var second=PreviewFrame.Read(stream);
            check(first.CapturedMs==123456789 && first.Synthetic && second.Quality==2 && stream.Position==pair.Length,
                "preview handles fragmented and back-to-back packets with matched metadata");
            check(first.Fresh(123457539) && !first.Fresh(123457540) && !first.Fresh(123456738),
                "preview expires after 750 ms and rejects future captures");
        }
        packet=Packet();packet[12]=1;
        check(Rejected(packet),"oversized preview rejected before allocation");
        check(Rejected(Packet(321)),"JPEG dimensions bounded before decoding");
        packet=Packet();packet[16]=2;
        check(Rejected(packet),"unknown preview source rejected");
        packet=Packet();packet[17]=3;
        check(Rejected(packet),"unknown preview quality rejected");
        packet=Packet();Array.Resize(ref packet,packet.Length-1);
        check(Rejected(packet),"truncated preview rejected");
        packet=Packet();packet[3]=50;
        check(Rejected(packet),"unsupported preview protocol rejected");
    }
    public static void Bridge(int port,Action<bool,string> check)
    {
        using(var client=new PreviewClient(port))
        {
            PreviewFrame frame=null;
            for(int i=0;i<100 && frame==null;i++) { Thread.Sleep(20);frame=client.TakeLatest(); }
            check(frame!=null && frame.CapturedMs==123456789 && frame.Synthetic && frame.Quality==1 && frame.Jpeg.Length==17,
                "Python preview packet reaches real C# background receiver");
        }
    }
}
