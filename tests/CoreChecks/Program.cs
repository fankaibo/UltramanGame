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
            PreviewChecks.Run(Check);
            PhotoChecks.Run(Check);
            BeamCloseupChecks.Run(Check);
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
            TrackingRegressions();
            FriendlyMotionRegressions();
            MotionChecks.Run(Check);
            BattleBalanceChecks.Run(Check);
            EnemyAttackChecks.Run(Check);
            InstructionChecks.Run(Check);
            var b=Started();Check(b.Phase==GamePhase.Battle,"transform enters battle");
            b.Tick(.02f,new PlayerInput {Tracking=true,Beam=true});Check(b.Action==HeroAction.None,"beam requires energy");
            Punch(b);Check(b.EnemyHealth==b.MaxHealth-1 && b.Punches==1,"one punch applies one hit");
            Advance(b,.8f);Check(b.EnemyHealth==b.MaxHealth-1,"idle frames cannot repeat a hit");
            b=Started();Advance(b,7.1f);Check(b.Enemy==EnemyPhase.Windup && b.HitsTaken==0,"enemy warns before hit");
            Advance(b,5.9f,true);Check(b.Blocks==1 && b.HitsTaken==0,"held shield blocks telegraphed attack");
            b=Started();Advance(b,13f);Check(b.HitsTaken==1 && b.Phase==GamePhase.Battle,"unblocked hit recovers without failure");
            Advance(b,120);Check(b.Phase==GamePhase.Battle,"repeated enemy hits never cause failure");
            b=Started();b.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});b.Tick(.02f,default);
            Check(b.Phase==GamePhase.Paused,"lost tracking pauses battle");
            Advance(b,.8f);Check(b.Phase==GamePhase.Paused,"recovery requires stable tracking interval");
            Advance(b,1);Check(b.EnemyHealth==b.MaxHealth && b.HitsTaken==0,"resume cancels pending player and enemy hits");
            b=Started();for(int i=0;i<60 && b.Energy<Battle.MaxEnergy;i++)Punch(b);
            Check(b.Energy==Battle.MaxEnergy,"attacks charge energy");
            float before=b.EnemyHealth;b.Tick(.02f,new PlayerInput{Tracking=true,Beam=true});Advance(b,1.6f);
            Check(b.Energy==0 && b.EnemyHealth==before-9,"beam consumes energy and deals one hit");
            b=Started();for(int i=0;i<100 && b.Phase!=GamePhase.Victory;i++)Punch(b);
            Check(b.Phase==GamePhase.Victory,"ordinary attacks can finish battle");
            int hits=b.HitsTaken;Advance(b,30);Check(b.HitsTaken==hits,"victory stops enemy attacks");
            if(args.Length>0 && args[0]=="--bridge") BridgeCheck(args.Length>1?int.Parse(args[1]):8765);
            if(args.Length>2 && args[0]=="--bridge") PreviewChecks.Bridge(int.Parse(args[2]),Check);
            Console.WriteLine($"{count} checks passed");return 0;
        }
        catch(Exception e) { Console.Error.WriteLine("FAIL "+e);return 1; }
    }
    static void FriendlyMotionRegressions()
    {
        var r=new GestureRecognizer();Holds(r,"neutral",25,x=>false);
        int punches=0;
        for(int i=0;i<40;i++)
        {
            var p=Pose();p.points[13]=new PosePoint(.77f,.40f);p.points[15]=new PosePoint(.93f,.35f);
            if(r.Update(p,p.capturedMs).LeftPunch)punches++;
        }
        Check(punches==1,"relaxed bent-arm punch triggers once without requiring full extension");
        Holds(r,"neutral",15,x=>false);
        int beams=0;
        for(int i=0;i<30;i++)
        {
            var p=Pose();p.points[13]=new PosePoint(.57f,.49f);p.points[15]=new PosePoint(.51f,.36f);
            p.points[14]=new PosePoint(.28f,.43f);p.points[16]=new PosePoint(.45f,.46f);
            if(r.Update(p,p.capturedMs).Beam)beams++;
        }
        Check(beams==1,"shorter relaxed L pose can fire a single beam");
        r.Reset();Holds(r,"neutral",20,x=>false);bool guarded=false;
        for(int i=0;i<20;i++)
        {
            var p=Pose();p.points[13]=new PosePoint(.7f,.43f);p.points[15]=new PosePoint(.56f,.435f);
            p.points[14]=new PosePoint(.30f,.43f);p.points[16]=new PosePoint(.44f,.435f);
            guarded|=r.Update(p,p.capturedMs).Shield;
        }
        Check(guarded,"relaxed crossed hands can hold a shield");
        var b=Started();b.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});Advance(b,.22f);
        b.Tick(.02f,new PlayerInput{Tracking=true,RightPunch=true});Advance(b,.65f);
        Check(b.Punches==2,"second punch near recovery is buffered and lands once");
        Advance(b,1);Check(b.Punches==2,"buffered punch does not repeat without new input");
        b=Started();b.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});Advance(b,.22f);
        b.Tick(.02f,new PlayerInput{Tracking=true,RightPunch=true});b.Tick(.02f,default);Advance(b,2.2f);
        Check(b.Punches==1,"tracking loss clears buffered attacks before resume");
    }
    static void TrackingRegressions()
    {
        var p=Pose();p.points[13].visibility=.1f;
        Check(PoseQuality.Present(p,stamp) && !PoseQuality.Valid(p,stamp),"obscured elbow is not a missing player");
        var presence=new PlayerPresence();
        Check(!presence.Update(null,stamp),"no grace before a player is seen");
        Check(presence.Update(p,stamp),"torso acquires presence with obscured arm");
        Check(presence.Update(null,stamp+450),"brief missing frames preserve presence");
        Check(!presence.Update(p,stamp+650),"old frames cannot prolong presence after timeout");
        p=Pose();p.points[11].visibility=p.points[12].visibility=.1f;
        Check(!PoseQuality.Present(p,stamp),"missing torso does not count as player presence");
        p=Pose();p.points[11]=new PosePoint(.52f,.3f);p.points[12]=new PosePoint(.48f,.3f);
        Check(PoseQuality.Present(p,stamp),"narrow shoulder projection does not mean player left");

        var r=new GestureRecognizer();int transforms=0;
        for(int i=0;i<60;i++)
        {
            p=Pose("raised");p.points[13].visibility=.1f;
            if(r.Update(p,stamp).Transform) transforms++;
        }
        Check(transforms==1,"raised visible wrists transform despite an obscured elbow");

        r=new GestureRecognizer();Holds(r,"neutral",25,x=>false);int right=0,left=0;
        for(int i=0;i<40;i++)
        {
            p=Pose("right");p.points[13].visibility=.1f;var input=r.Update(p,stamp);
            if(input.RightPunch) right++;if(input.LeftPunch) left++;
        }
        Check(right==1 && left==0,"visible arm can punch while other arm is obscured");

        r=new GestureRecognizer();Holds(r,"neutral",25,x=>false);Holds(r,"beam",55,x=>false);
        p=Pose("beam");p.points[15].visibility=.1f;var blocked=r.Update(p,stamp);
        Check(blocked.Tracking && !blocked.Beam && !blocked.Shield,"missing wrist disables move without losing player");
        Check(Holds(r,"beam",55,x=>x.Beam)==0,"held beam cannot refire across arm occlusion");

        r=new GestureRecognizer();Holds(r,"neutral",25,x=>false);Holds(r,"punch",30,x=>false);
        p=Pose("punch");p.points[13].visibility=.1f;r.Update(p,stamp);
        Check(Holds(r,"punch",30,x=>x.LeftPunch)==0,"occluded punching arm must retract before another hit");

        var battle=Started();r=new GestureRecognizer();presence=new PlayerPresence();
        for(int i=0;i<120;i++)
        {
            p=Pose();p.points[13].visibility=.1f;
            var input=r.Update(p,stamp);input.Tracking=presence.Update(p,stamp);battle.Tick(.02f,input);
        }
        Check(battle.Phase==GamePhase.Battle,"persistent arm occlusion does not interrupt battle");
        battle.Tick(.02f,new PlayerInput{Tracking=presence.Update(null,stamp+650)});
        Check(battle.Phase==GamePhase.Paused,"actual stream loss still pauses battle after grace");
    }
    static void BridgeCheck(int port)
    {
        using(var client=new PoseClient(port))
        {
            string line=null;var deadline=DateTime.UtcNow.AddSeconds(5);
            while(line==null && DateTime.UtcNow<deadline) {line=client.TakeLatest();Thread.Sleep(30);}
            Check(line!=null,"C# receives Python loopback frame");
            var frame=JsonSerializer.Deserialize<PoseFrame>(line,new JsonSerializerOptions{IncludeFields=true});
            Check(PoseQuality.Valid(frame,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),"Python to C# schema and freshness match");
        }
    }
}
