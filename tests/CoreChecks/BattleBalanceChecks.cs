using System;
using UltramanGame.Core;

static class BattleBalanceChecks
{
    static void Step(Battle battle,float seconds,bool shield=true)
    { for(int i=0;i<(int)Math.Ceiling(seconds/.02f);i++)battle.Tick(.02f,new PlayerInput {Tracking=true,Shield=shield}); }
    static Battle Start(int health=Battle.DefaultMonsterHits)
    {
        var b=new Battle(health);b.Tick(.02f,new PlayerInput {Tracking=true,Transform=true});Step(b,2.4f);return b;
    }
    static void Hit(Battle b)
    {
        int before=b.Punches;
        for(int tries=0;tries<20&&b.Punches==before&&b.Phase==GamePhase.Battle;tries++)
        {b.Tick(.02f,new PlayerInput {Tracking=true,LeftPunch=true});Step(b,.6f);}
        if(b.Punches!=before+1)throw new Exception("Could not land exactly one test punch");
    }
    static int DrainReady(Battle b)
    {int ready=0;while(b.TryCue(out var cue))if(cue==GameCue.EnergyReady)ready++;return ready;}
    public static void Run(Action<bool,string> check)
    {
        var b=Start();check(b.MaxHealth==50&&b.EnemyHealth==50,"default monster withstands 50 ordinary hits");
        var low=Start(10);for(int i=0;i<9;i++)Hit(low);
        check(low.EnemyHealth==1&&low.Phase==GamePhase.Battle,"custom health remains alive before final hit");
        Hit(low);check(low.Phase==GamePhase.Victory&&low.Punches==10,"custom health wins on exact configured hit");
        var high=Start(100);for(int i=0;i<50;i++)Hit(high);
        check(high.EnemyHealth==50&&high.Phase==GamePhase.Battle,"larger custom health does not end at default health");
        check(new Battle(-1).MaxHealth==10&&new Battle(1000).MaxHealth==200,"invalid monster settings clamp to playable bounds");
        Step(b,13);check(b.Blocks>0&&b.Energy==0,"successful defense does not charge the beam");
        for(int i=0;i<14;i++) {Hit(b);DrainReady(b);}
        check(b.Punches==14&&b.Energy==14,"fourteen ordinary hits do not fill beam energy");
        b.Tick(.02f,new PlayerInput {Tracking=true,Beam=true});
        check(b.Action!=HeroAction.Beam&&b.Energy==14,"beam attempt at fourteen hits is rejected without spending energy");
        Hit(b);check(b.Punches==15&&b.Energy==15&&DrainReady(b)==1,"fifteenth hit fills energy and announces readiness once");
        Hit(b);check(b.Energy==15&&DrainReady(b)==0,"extra hits cap energy without repeat readiness cue");
        float health=b.EnemyHealth;b.Tick(.02f,new PlayerInput {Tracking=true,Beam=true});Step(b,1.6f);
        check(b.Energy==0&&b.EnemyHealth==health-9,"full beam consumes all energy and deals nine ordinary hits of damage");
        for(int i=0;i<14;i++)Hit(b);
        check(b.Energy==14,"beam needs fourteen fresh hits to return to fourteen energy");
        Hit(b);check(b.Energy==15,"next beam again requires fifteen fresh ordinary hits");
        b=Start();for(int i=0;i<49;i++)Hit(b);
        check(b.EnemyHealth==1&&b.Phase==GamePhase.Battle,"default monster survives forty-nine ordinary hits");
        Hit(b);check(b.EnemyHealth==0&&b.Phase==GamePhase.Victory&&b.Punches==50,"default monster falls on fiftieth ordinary hit");
        b=Start();b.Tick(.02f,new PlayerInput {Tracking=true,LeftPunch=true});b.Tick(.02f,default);Step(b,2);
        check(b.Punches==0&&b.Energy==0,"interrupted punch before impact awards no energy");
    }
}
