using System;
using UltramanGame.Core;

static class ImpactTimingChecks
{
    public static void Run(Action<bool,string> check)
    {
        var timing=new ImpactTiming();
        check(Math.Abs(timing.Delta(.02f,GamePhase.Battle)-.02f)<.00001f,"normal combat runs in real time");
        timing.Hit(false);float advanced=0;
        for(int i=0;i<6;i++){advanced+=timing.Delta(.01f,GamePhase.Battle);timing.Tick(.01f,GamePhase.Battle);}
        check(Math.Abs(advanced-.016f)<.0001f&&timing.Remaining==0,"impact slows only its bounded interval, including final partial frame");
        timing.Hit(true);timing.Tick(.01f,GamePhase.Paused);
        check(timing.Remaining==0,"tracking pause immediately clears impact slow motion");
        timing.Hit(true);timing.Clear();check(timing.Delta(.1f,GamePhase.Battle)==.1f,"restart restores normal combat time");
        var battle=new Battle();battle.Tick(.01f,new PlayerInput{Tracking=true,Transform=true});
        for(int i=0;i<240;i++)battle.Tick(.01f,new PlayerInput{Tracking=true});
        timing.Hit(false);battle.Tick(timing.Delta(.016f,battle.Phase),new PlayerInput{Tracking=true,LeftPunch=true});
        check(battle.Action==HeroAction.LeftPunch,"a new punch is accepted during impact slow motion");
        battle.Tick(timing.Delta(.016f,battle.Phase),default);
        check(battle.Phase==GamePhase.Paused,"tracking input still pauses during impact slow motion");
    }
}
