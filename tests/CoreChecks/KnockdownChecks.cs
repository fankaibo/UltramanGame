using System;
using UltramanGame.Core;

static class KnockdownChecks
{
    static PlayerInput Tracked() => new PlayerInput {Tracking=true};
    static Battle Hit(float dt)
    {
        var b=new Battle();b.Tick(dt,new PlayerInput{Tracking=true,Transform=true});
        for(int i=0;i<2000&&b.HitsTaken==0;i++)b.Tick(dt,Tracked());
        if(b.Action!=HeroAction.Hurt)throw new Exception("Knockdown setup did not reach an unblocked hit");
        while(b.TryCue(out _)){}return b;
    }
    static int Landings(Battle b)
    {int count=0;while(b.TryCue(out var cue))if(cue==GameCue.HeroLanded)count++;return count;}
    public static void Run(Action<bool,string> check)
    {
        foreach(int fps in new[]{15,30,60})
        {
            float dt=1f/fps;var b=Hit(dt);int landings=0;bool valid=true,held=false;
            while(b.Action==HeroAction.Hurt)
            {
                float age=b.ActionAge;
                b.Tick(dt,new PlayerInput{Tracking=true,LeftPunch=true,RightPunch=true,Shield=true});
                landings+=Landings(b);
                if(b.ActionAge<KnockdownMotion.LandingSeconds)valid&=landings==0;
                if(b.ActionAge>=KnockdownMotion.LandingSeconds)valid&=landings==1;
                valid&=b.Punches==0&&b.EnemyHealth==b.MaxHealth&&b.Energy==0&&!b.Shield;
                if(age>.6f&&age<1.4f)held|=b.Action==HeroAction.Hurt;
            }
            check(valid&&held&&landings==1&&b.HitsTaken==1,$"{fps} FPS fall lands once, holds recovery and cannot attack while down");
            b.Tick(dt,new PlayerInput{Tracking=true,Shield=true});
            check(b.Shield&&b.Action==HeroAction.None,$"{fps} FPS guard works immediately after standing up");
            b.Tick(dt,new PlayerInput{Tracking=true,LeftPunch=true});
            for(int i=0;i<8;i++)b.Tick(dt,Tracked());
            check(b.Punches==1,$"{fps} FPS fresh punch works after recovery");
        }
        foreach(float interruptedAt in new[]{.15f,.45f,1.05f})
        {
            var b=Hit(.02f);while(b.ActionAge<interruptedAt)b.Tick(.02f,Tracked());Landings(b);
            b.Tick(.02f,default);int landings=0;
            for(int i=0;i<100;i++){b.Tick(.02f,Tracked());landings+=Landings(b);}
            check(b.Phase==GamePhase.Battle&&b.Action==HeroAction.None&&landings==0&&b.HitsTaken==1,
                $"tracking loss at {interruptedAt}s cancels fall without replaying the landing on resume");
        }
        check(KnockdownMotion.Weight(0)==0&&KnockdownMotion.Weight(.5f)==1&&KnockdownMotion.Weight(KnockdownMotion.Duration)==0,
            "fall has a grounded hold and returns upright before input unlocks");
    }
}
