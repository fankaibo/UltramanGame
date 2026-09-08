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
        var session=new PhotoSession();session.Open();
        check(session.Stage==PhotoStage.Framing&&!session.Tick(100,true),"photo opens a live viewfinder without taking a picture");
        check(!session.Begin(100,false)&&session.Stage==PhotoStage.Framing,"photo waits for a fresh person before countdown");
        check(session.Begin(100,true)&&!session.Begin(102,true)&&!session.Tick(104.99,true),"explicit photo click starts one full countdown");
        session.Back();check(session.Stage==PhotoStage.Framing&&!session.Tick(106,true),"cancel returns to live posing without saving");
        session.Begin(200,true);check(!session.Tick(205,false)&&session.Missed&&session.Stage==PhotoStage.Framing,"camera loss at shutter returns to live viewfinder");
        check(!session.Tick(206,true),"camera recovery cannot silently take a missed photo");
        session.Begin(300,true);check(session.Tick(305,true)&&session.Stage==PhotoStage.Review,"finished shot enters a distinct photo review");
        check(!session.Tick(1000,true)&&!session.Begin(1000,true)&&session.Stage==PhotoStage.Review,"photo review stays frozen until explicit user action");
        session.Open();check(session.Stage==PhotoStage.Framing&&!session.Tick(1100,true),"retake lets the player pose again before starting a countdown");
        session.Begin(1200,true);session.Close();check(!session.Tick(1205,true)&&session.Stage==PhotoStage.Closed,"closing the viewfinder cancels pending capture");
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
