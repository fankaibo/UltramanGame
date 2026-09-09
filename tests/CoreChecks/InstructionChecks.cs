using System;
using UltramanGame.Core;

static class InstructionChecks
{
    static void Step(Battle b,float seconds,bool shield=false)
    {while(seconds>.00001f){float dt=Math.Min(.02f,seconds);b.Tick(dt,new PlayerInput{Tracking=true,Shield=shield});seconds-=dt;}}
    static Battle Start()
    {
        var b=new Battle(200);b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
        while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});return b;
    }
    static void Windup(Battle b)
    {
        for(int i=0;i<2000&&b.Enemy!=EnemyPhase.Windup;i++)Step(b,.02f);
        if(b.Enemy!=EnemyPhase.Windup)throw new Exception("Warning not reached");
    }
    static Battle AlmostReady()
    {
        var b=Start();
        for(int i=0;i<14;i++){b.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});Step(b,.5f,true);}
        if(b.Energy!=14)throw new Exception("Could not fill fourteen energy");
        b.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});Step(b,.13f);
        if(b.Energy!=15||b.Action!=HeroAction.LeftPunch)throw new Exception("Final punch recovery not reached");
        return b;
    }
    public static void Run(Action<bool,string> check)
    {
        var b=Start();Windup(b);b.GiveInstructionTime(2.24f,true);Step(b,5.2f);
        check(b.Enemy==EnemyPhase.Windup&&b.HitsTaken==0,"warning leaves three seconds after the recorded instruction finishes");
        Step(b,.65f,true);check(b.Blocks==1&&b.HitsTaken==0,"late child defense still blocks after the longer warning");
        b=Start();Windup(b);Step(b,1);b.GiveInstructionTime(5,true);Step(b,7.9f);
        check(b.Enemy==EnemyPhase.Windup&&b.HitsTaken==0,"delayed or longer warning voice starts its reaction interval at actual playback");
        Step(b,.2f);check(b.Enemy==EnemyPhase.Attack,"warning ends after the extended response interval");
        b=Start();Windup(b);b.GiveInstructionTime(5.3f);Step(b,8.1f);
        check(b.Enemy==EnemyPhase.Rest&&b.EnemyAge==0&&b.HitsTaken==0,"beam lesson holds off the monster through speech plus three seconds");
        b.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});Step(b,.13f);
        check(b.Punches==1,"instruction breathing room never delays a child's attack");
        b=AlmostReady();b.GiveInstructionTime(3.98f);Step(b,6.8f);
        check(b.Energy==15&&b.Enemy==EnemyPhase.Rest,"full energy is preserved while the child listens and prepares");
        Step(b,30,true);check(b.Energy==15,"beam readiness does not expire if the child takes longer");
        b=AlmostReady();float health=b.EnemyHealth;
        b.Tick(.02f,new PlayerInput{Tracking=true,Beam=true});Step(b,1);
        check(b.Energy==0&&b.EnemyHealth==health-9,"one-frame beam gesture during punch recovery is buffered and lands once");
        Step(b,2);check(b.EnemyHealth==health-9,"buffered beam does not release twice");
        b=AlmostReady();health=b.EnemyHealth;b.Tick(.02f,new PlayerInput{Tracking=true,Beam=true});b.Pause();Step(b,2.5f);
        check(b.Energy==15&&b.EnemyHealth==health,"pause discards a buffered beam without spending energy");
        b.GiveInstructionTime(5.3f);b.Pause();Step(b,1.3f);
        check(b.InstructionRemaining<=3&&b.Enemy==EnemyPhase.Rest,"resume clears the previous lesson deadline and grants fresh reaction time");
    }
}
