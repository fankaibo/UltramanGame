using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using UltramanGame.Core;

static class Program
{
    static int count;
    static long sequence;
    static long stamp=100000;
    static void Check(bool condition,string name) { if(!condition) throw new Exception(name);count++;Console.WriteLine("PASS "+name); }
    static PoseFrame Pose(string kind="neutral")
    {
        var p=new PosePoint[33];for(int i=0;i<33;i++) p[i]=new PosePoint(.5f,.5f);
        p[11]=new PosePoint(.65f,.3f);p[12]=new PosePoint(.35f,.3f);
        p[13]=new PosePoint(.69f,.48f);p[14]=new PosePoint(.31f,.48f);
        p[15]=new PosePoint(.67f,.61f);p[16]=new PosePoint(.33f,.61f);
        if(kind=="punch") { p[13]=new PosePoint(.79f,.31f);p[15]=new PosePoint(.99f,.3f); }
        if(kind=="right") { p[14]=new PosePoint(.21f,.31f);p[16]=new PosePoint(.01f,.3f); }
        if(kind=="shield") { p[15]=new PosePoint(.55f,.32f);p[16]=new PosePoint(.45f,.32f); }
        if(kind=="beam") { p[13]=new PosePoint(.54f,.50f);p[15]=new PosePoint(.54f,.27f);p[14]=new PosePoint(.28f,.41f);p[16]=new PosePoint(.53f,.41f); }
        if(kind=="raised") { p[15]=new PosePoint(.7f,.05f);p[16]=new PosePoint(.3f,.05f); }
        stamp+=33;
        return new PoseFrame { schema=1,streamId="test",sequence=++sequence,capturedMs=stamp,tracked=true,points=p };
    }
    static PlayerInput Feed(GestureRecognizer recognizer,string kind)
    {var f=Pose(kind);return recognizer.Update(f,f.capturedMs);}
    static int Holds(GestureRecognizer r,string kind,int times,Func<PlayerInput,bool> fired)
    {int n=0;for(int i=0;i<times;i++)if(fired(Feed(r,kind)))n++;return n;}
    static Battle Started()
    {
        var b=new Battle();b.Tick(.02f,new PlayerInput {Tracking=true,Transform=true});
        Advance(b,2.4f);return b;
    }
    static void Advance(Battle b,float seconds,bool shield=false)
    {for(int i=0;i<(int)Math.Ceiling(seconds/.02f);i++)b.Tick(.02f,new PlayerInput{Tracking=true,Shield=shield});}
    static void Punch(Battle b)
    {b.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});Advance(b,.5f);}

    static int Main(string[] args)
    {
        try
        {
            var pose=Pose();Check(PoseQuality.Valid(pose,stamp),"complete fresh pose accepted");
            Check(!PoseQuality.Valid(pose,stamp+351),"stale capture rejected");
            Check(!PoseQuality.Valid(pose,stamp-51),"future capture rejected");
            pose.points[15].visibility=.2f;Check(!PoseQuality.Valid(pose,stamp),"occluded wrist rejected");
            pose=Pose();pose.points[1].x=float.NaN;Check(!PoseQuality.Valid(pose,stamp),"NaN even outside arm joints rejected");
            var r=new GestureRecognizer();Holds(r,"neutral",25,x=>false);
            Check(Holds(r,"punch",45,x=>x.LeftPunch)==1,"held left punch triggers once");
            Holds(r,"neutral",20,x=>false);
            Check(Holds(r,"punch",20,x=>x.LeftPunch)==1,"retracted arm rearms punch");
            Holds(r,"neutral",20,x=>false);
            Check(Holds(r,"right",30,x=>x.RightPunch)==1,"right-hand punch accepted");
            Holds(r,"neutral",20,x=>false);
            Check(Holds(r,"beam",55,x=>x.Beam)==1,"held beam triggers once");
            Holds(r,"neutral",20,x=>false);
            Check(Holds(r,"shield",30,x=>x.Shield)>10,"stable shield recognized");
            Check(Holds(r,"shield",20,x=>x.LeftPunch||x.RightPunch||x.Beam)==0,"shield cannot generate attacks");
            Holds(r,"neutral",20,x=>false);
            Check(Holds(r,"raised",50,x=>x.Transform)==1,"transform pose triggers once");
            r.Update(null,stamp);Check(!Feed(r,"beam").Beam,"tracking loss clears held gesture");
            var b=Started();Check(b.Phase==GamePhase.Battle,"transform enters battle");
            b.Tick(.02f,new PlayerInput {Tracking=true,Beam=true});Check(b.Action==HeroAction.None,"beam requires energy");
            Punch(b);Check(b.EnemyHealth==23 && b.Punches==1,"one punch applies one hit");
            Advance(b,.8f);Check(b.EnemyHealth==23,"idle frames cannot repeat a hit");
            b=Started();Advance(b,4.1f);Check(b.Enemy==EnemyPhase.Windup && b.HitsTaken==0,"enemy warns before hit");
            Advance(b,2.5f,true);Check(b.Blocks==1 && b.HitsTaken==0,"held shield blocks telegraphed attack");
            b=Started();Advance(b,6.6f);Check(b.HitsTaken==1 && b.Phase==GamePhase.Battle,"unblocked hit recovers without failure");
            Advance(b,120);Check(b.Phase==GamePhase.Battle,"repeated enemy hits never cause failure");
            b=Started();b.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});b.Tick(.02f,default);
            Check(b.Phase==GamePhase.Paused,"lost tracking pauses battle");
            Advance(b,.8f);Check(b.Phase==GamePhase.Paused,"recovery requires stable tracking interval");
            Advance(b,1);Check(b.EnemyHealth==24 && b.HitsTaken==0,"resume cancels pending player and enemy hits");
            b=Started();for(int i=0;i<6;i++)Punch(b);
            Check(b.Energy==6,"attacks charge energy");
            float before=b.EnemyHealth;b.Tick(.02f,new PlayerInput{Tracking=true,Beam=true});Advance(b,1.6f);
            Check(b.Energy==0 && b.EnemyHealth==before-9,"beam consumes energy and deals one hit");
            b=Started();for(int i=0;i<30 && b.Phase!=GamePhase.Victory;i++)Punch(b);
            Check(b.Phase==GamePhase.Victory,"ordinary attacks can finish battle");
            int hits=b.HitsTaken;Advance(b,30);Check(b.HitsTaken==hits,"victory stops enemy attacks");
            if(args.Length>0 && args[0]=="--bridge") BridgeCheck();
            Console.WriteLine($"{count} checks passed");return 0;
        }
        catch(Exception e) { Console.Error.WriteLine("FAIL "+e);return 1; }
    }
    static void BridgeCheck()
    {
        using(var client=new PoseClient())
        {
            string line=null;var deadline=DateTime.UtcNow.AddSeconds(5);
            while(line==null && DateTime.UtcNow<deadline) {line=client.TakeLatest();Thread.Sleep(30);}
            Check(line!=null,"C# receives Python loopback frame");
            var frame=JsonSerializer.Deserialize<PoseFrame>(line,new JsonSerializerOptions{IncludeFields=true});
            Check(PoseQuality.Valid(frame,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),"Python to C# schema and freshness match");
        }
    }
}
