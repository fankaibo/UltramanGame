using System;
using System.IO;
using UltramanGame.Core;

static class PhotoChecks
{
    public static void Run(Action<bool,string> check)
    {
        var c=new PhotoCountdown();c.Begin(100);
        check(c.Remaining(100)==5&&!c.TakeShot(104.999),"photo waits the full five seconds");
        c.Begin(102);check(c.TakeShot(105),"repeated click does not reset or duplicate the countdown");
        check(!c.TakeShot(106)&&!c.Running,"photo deadline yields exactly one capture");
        c.Begin(200);c.Cancel();check(!c.TakeShot(206),"cancelled countdown never captures");
        c.Begin(300);check(c.Remaining(304)==1&&c.TakeShot(305),"retake gets a new full five seconds");
        var data=Packet();var frame=PhotoFrame.Read(new MemoryStream(data));
        check(frame.Present&&frame.Synthetic&&frame.Fresh(10000),"photo packet carries a fresh synthetic cutout");
        check(!frame.Fresh(10751)&&!frame.Fresh(9949),"photo rejects stale and future capture times");
        foreach(var offset in new[]{0,16,17,18,18+24,18+25})
        {var bad=(byte[])data.Clone();bad[offset]=255;check(Rejected(bad),"photo rejects invalid header, PNG or channels at "+offset);}
        var oversized=(byte[])data.Clone();oversized[12]=1;
        check(Rejected(oversized),"photo rejects oversized payload before allocating it");
        var dimensions=(byte[])data.Clone();dimensions[18+16]=1;
        check(Rejected(dimensions),"photo bounds PNG dimensions before image decoding");
        check(Rejected(new byte[17]),"truncated photo packet fails closed");
        string folder=Path.Combine(Path.GetTempPath(),"ultraman-photo-test-"+Guid.NewGuid());
        try
        {
            string a=PhotoFiles.Save(folder,new byte[]{1,2,3},true),b=PhotoFiles.Save(folder,new byte[]{4,5},true);
            check(a!=b&&File.ReadAllBytes(a).Length==3&&File.ReadAllBytes(b).Length==2,"retakes save distinct files without overwriting");
            check(Directory.GetFiles(folder,"*.tmp").Length==0,"photo save leaves no partial file");
            check(Path.GetFileName(PhotoFiles.Downloads)=="Downloads"&&Path.IsPathRooted(PhotoFiles.Downloads),"photo uses the current user Downloads directory");
        }
        finally {if(Directory.Exists(folder))Directory.Delete(folder,true);}
    }
    static bool Rejected(byte[] bytes)
    {try {PhotoFrame.Read(new MemoryStream(bytes));return false;}catch(IOException) {return true;}}
    static byte[] Packet()
    {
        var b=new byte[51];b[0]=85;b[1]=71;b[2]=70;b[3]=49;b[10]=39;b[11]=16;b[15]=33;b[16]=b[17]=1;
        new byte[]{137,80,78,71,13,10,26,10}.CopyTo(b,18);
        b[18+11]=13;b[18+12]=73;b[18+13]=72;b[18+14]=68;b[18+15]=82;
        b[18+18]=2;b[18+19]=128;b[18+22]=1;b[18+23]=224;b[18+24]=8;b[18+25]=6;return b;
    }
}
