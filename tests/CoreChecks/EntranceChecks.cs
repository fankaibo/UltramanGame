using System;
using UltramanGame.Core;

static class EntranceChecks
{
    public static void Run(Action<bool,string> check)
    {
        foreach(int hz in new[]{15,30,60})
        {
            float dt=1f/hz;var battle=new Battle();battle.Tick(dt,new PlayerInput{Tracking=true,Transform=true});
            int transforms=0,starts=0;bool paused=false,ignored=true;
            for(int n=0;n<hz*10&&battle.Phase!=GamePhase.Battle;n++)
            {
                if(!paused&&battle.TransformationAge>=3.3f)
                {
                    float age=battle.TransformationAge;battle.Pause();
                    for(int f=0;f<hz;f++)battle.Tick(dt,default);
                    while(battle.Phase==GamePhase.Paused)battle.Tick(dt,new PlayerInput{Tracking=true});
                    check(Math.Abs(battle.TransformationAge-age)<.0001f,"monster entrance resumes the same clock at "+hz);paused=true;
                }
                while(battle.TryCue(out var cue)){if(cue==GameCue.Transform)transforms++;if(cue==GameCue.BattleStart)starts++;}
                ignored&=battle.Punches==0&&battle.Energy==0&&battle.EnemyHealth==50;
                battle.Tick(dt,new PlayerInput{Tracking=true,LeftPunch=true,Beam=true,Shield=true});
            }
            while(battle.TryCue(out var cue)){if(cue==GameCue.Transform)transforms++;if(cue==GameCue.BattleStart)starts++;}
            check(ignored&&battle.Punches==0&&battle.Energy==0&&battle.EnemyHealth==50,"intro ignores attack inputs at "+hz);
            check(transforms==1&&starts==1&&battle.Phase==GamePhase.Battle,"opening has one transform and battle start at "+hz);
            check(battle.InstructionRemaining>=Battle.InstructionReactionSeconds-.001f,"opening preserves the complete reaction allowance at "+hz);
            battle.Tick(dt,new PlayerInput{Tracking=true,RightPunch=true});
            check(battle.Action==HeroAction.RightPunch,"child can attack immediately after opening at "+hz);
        }
    }
}
