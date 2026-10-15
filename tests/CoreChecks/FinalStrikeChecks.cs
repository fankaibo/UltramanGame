using System;
using UltramanGame.Core;

static class FinalStrikeChecks
{
    static void Step(Battle b,float seconds)
    {for(float t=0;t<seconds;t+=.02f)b.Tick(.02f,new PlayerInput{Tracking=true,Shield=true});}
    static Battle Ready(bool beam,int health=0)
    {
        var b=new Battle(health>0?health:beam?24:10);b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});Step(b,Battle.TransformationSeconds+.2f);
        for(int i=0;i<(beam?15:b.MaxHealth-1);i++)
        {b.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});Step(b,.5f);}
        while(b.TryCue(out _)){}return b;
    }
    public static void Run(Action<bool,string> check)
    {
        foreach(int hz in new[]{15,30,60})foreach(bool beam in new[]{false,true})
        {
            var b=Ready(beam);float dt=1f/hz;int wins=0,damage=0;float health=b.EnemyHealth;
            b.Tick(dt,new PlayerInput{Tracking=true,Beam=beam,RightPunch=!beam});
            while(b.EnemyHealth>0)b.Tick(dt,new PlayerInput{Tracking=true});
            check(b.Phase==GamePhase.Battle&&b.Action==(beam?HeroAction.Beam:HeroAction.RightPunch),"lethal strike keeps its action until release finishes "+hz+" beam="+beam);
            float contactAge=b.ActionAge;
            b.Pause();float pausedAge=b.ActionAge;
            for(int i=0;i<hz;i++)b.Tick(dt,default);
            while(b.Phase==GamePhase.Paused)b.Tick(dt,new PlayerInput{Tracking=true});
            check(b.Phase==GamePhase.Battle&&Math.Abs(b.ActionAge-pausedAge)<.001f&&Math.Abs(pausedAge-contactAge)<.001f,"finishing action survives pause without reapplying damage "+hz+" beam="+beam);
            bool single=true;int initialPunches=b.Punches,hits=b.HitsTaken;
            for(int i=0;i<hz*3;i++)
            {
                b.Tick(dt,new PlayerInput{Tracking=true,LeftPunch=true,RightPunch=true,Beam=true,Shield=true,GuardIntent=true,BeamIntent=true});
                if(b.EnemyHealth<health){damage++;health=b.EnemyHealth;}
                while(b.TryCue(out var cue))if(cue==GameCue.Victory)wins++;
                single&=b.Punches==initialPunches&&b.HitsTaken==hits&&b.EnemyHealth==0;
            }
            check(single&&wins==1&&damage==1&&b.Phase==GamePhase.Victory,"finishing sequence accepts no second hit or enemy retaliation "+hz+" beam="+beam);
            check(b.ActionAge>=(beam?Battle.BeamSeconds:Battle.PunchSeconds),"victory waits for full terminal action "+hz+" beam="+beam);
        }
        var lastCharge=Ready(false,15);lastCharge.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});Step(lastCharge,.5f);
        int energyCues=0,victories=0;while(lastCharge.TryCue(out var cue)){if(cue==GameCue.EnergyReady)energyCues++;if(cue==GameCue.Victory)victories++;}
        check(lastCharge.Phase==GamePhase.Victory&&lastCharge.Energy==15&&energyCues==0&&victories==1,"fifteenth lethal punch keeps energy accounting without prompting another beam");
    }
}
