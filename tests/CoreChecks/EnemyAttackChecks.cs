using System;
using UltramanGame.Core;

static class EnemyAttackChecks
{
    static PlayerInput Tracked(bool shield=false) => new PlayerInput {Tracking=true,Shield=shield};
    static void Step(Battle b,float seconds,bool shield=false)
    {while(seconds>.00001f) {float dt=Math.Min(seconds,.02f);b.Tick(dt,Tracked(shield));seconds-=dt;}}
    static Battle Start(int hp=50)
    {
        var b=new Battle(hp);b.Tick(.02f,new PlayerInput {Tracking=true,Transform=true});
        while(b.Phase==GamePhase.Transforming)b.Tick(.02f,Tracked());return b;
    }
    static void Rush(Battle b,float dt=.02f,bool shield=false)
    {
        for(int i=0;i<2000&&b.Enemy!=EnemyPhase.Attack;i++)b.Tick(dt,Tracked(shield));
        if(b.Enemy!=EnemyPhase.Attack)throw new Exception("Monster did not enter attack");
    }
    static void Punch(Battle b)
    {
        int before=b.Punches;
        for(int i=0;i<20&&b.Punches==before;i++)
        {b.Tick(.02f,new PlayerInput {Tracking=true,LeftPunch=true});Step(b,.6f,true);}
        if(b.Punches!=before+1)throw new Exception("Could not land test punch");
    }
    public static void Run(Action<bool,string> check)
    {
        foreach(int fps in new[]{15,30,60})
        {
            float dt=1f/fps;var b=Start();Rush(b,dt);
            check(b.HitsTaken==0&&b.Blocks==0,$"{fps} FPS rush begins without instant damage");
            while(b.EnemyAge+dt<Battle.EnemyHitSeconds-.00001f)b.Tick(dt,Tracked());
            check(b.HitsTaken==0,$"{fps} FPS approach before contact cannot hit");
            while(b.EnemyAge<Battle.EnemyHitSeconds)b.Tick(dt,Tracked());
            check(b.HitsTaken==1,$"{fps} FPS contact resolves one enemy hit");
            Step(b,1.8f);check(b.HitsTaken==1,$"{fps} FPS follow-through and retreat do not hit again");
        }
        var guard=Start();Rush(guard);Step(guard,.38f);guard.Tick(.03f,Tracked(true));
        check(guard.Blocks==1&&guard.HitsTaken==0&&guard.Energy==0,"shield raised during rush can block at contact without energy reward");
        Step(guard,.8f);check(guard.Blocks==1&&guard.HitsTaken==0,"releasing after contact does not turn a block into damage");
        var released=Start();Rush(released,shield:true);Step(released,.38f,true);released.Tick(.03f,Tracked());
        check(released.Blocks==0&&released.HitsTaken==1,"shield released before contact no longer blocks");
        var paused=Start();Rush(paused);Step(paused,.3f);paused.Tick(.02f,default);Step(paused,2);
        check(paused.HitsTaken==0&&paused.Enemy==EnemyPhase.Rest,"tracking loss cancels rush and pending contact across resume");
        var beam=Start();for(int i=0;i<15;i++)Punch(beam);Rush(beam,shield:true);Step(beam,.2f,true);
        int hits=beam.HitsTaken;beam.Tick(.02f,new PlayerInput {Tracking=true,Beam=true});
        check(beam.Action==HeroAction.Beam&&beam.Enemy==EnemyPhase.Rest,"beam interrupts incoming rush immediately");
        Step(beam,2);check(beam.HitsTaken==hits,"canceled rush cannot land after beam protection ends");
        var win=Start(10);for(int i=0;i<9;i++)Punch(win);Rush(win,shield:true);
        hits=win.HitsTaken;win.Tick(.02f,new PlayerInput {Tracking=true,LeftPunch=true});Step(win,.2f);
        check(win.Phase==GamePhase.Victory&&win.HitsTaken==hits,"winning punch stops incoming enemy contact");
    }
}
