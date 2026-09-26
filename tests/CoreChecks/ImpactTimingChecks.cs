using System;
using UltramanGame.Core;

static class ImpactTimingChecks
{
    public static void Run(Action<bool,string> check)
    {
        var timing=new ImpactTiming();
        check(Math.Abs(timing.Delta(.02f,GamePhase.Battle)-.02f)<.00001f,"normal combat runs in real time");
        timing.Hit(false);float advanced=0;
        check(timing.Delta(.02f,GamePhase.Battle)==0,"impact initially holds the actual contact pose");
        for(int i=0;i<13;i++){advanced+=timing.Delta(.01f,GamePhase.Battle);timing.Tick(.01f,GamePhase.Battle);}
        check(Math.Abs(advanced-.021f)<.0001f&&timing.Remaining==0&&timing.HoldRemaining==0,"contact hold and slow release consume their bounded intervals including partial frames");
        timing.Hit(false);
        float oneStep=timing.Delta(.13f,GamePhase.Battle);
        check(Math.Abs(oneStep-advanced)<.0001f,"impact duration is independent of render frame subdivision");
        timing.Hit(true);timing.Tick(.01f,GamePhase.Paused);
        check(timing.Remaining==0&&timing.HoldRemaining==0,"tracking pause immediately clears both impact phases");
        timing.Hit(true);timing.Clear();check(timing.Delta(.1f,GamePhase.Battle)==.1f,"restart restores normal combat time");
        var battle=new Battle();battle.Tick(.01f,new PlayerInput{Tracking=true,Transform=true});
        for(int i=0;i<240;i++)battle.Tick(.01f,new PlayerInput{Tracking=true});
        timing.Hit(false);battle.Tick(timing.Delta(.016f,battle.Phase),new PlayerInput{Tracking=true,LeftPunch=true});
        check(battle.Action==HeroAction.LeftPunch,"a new punch is accepted during impact slow motion");
        timing.Clear();
        for(int i=0;i<8;i++)battle.Tick(.02f,new PlayerInput{Tracking=true});
        timing.Hit(false);float contactAge=battle.ActionAge,health=battle.EnemyHealth;
        for(int i=0;i<3;i++)
        {battle.Tick(timing.Delta(.016f,battle.Phase),new PlayerInput{Tracking=true});timing.Tick(.016f,battle.Phase);}
        check(battle.ActionAge==contactAge&&battle.EnemyHealth==health&&battle.Punches==1,"held contact neither skips the pose nor applies duplicate damage");
        battle.Tick(timing.Delta(.016f,battle.Phase),default);
        check(battle.Phase==GamePhase.Paused,"tracking input still pauses during impact slow motion");
    }
}
