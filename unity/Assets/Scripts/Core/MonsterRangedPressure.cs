using System;

namespace UltramanGame.Core
{
    // An animation request, never extra damage or a change to an enemy clock.
    // Consecutive impacts force an occasional recovery step. The hit receipt
    // survives a hero's immediate transition to guard after releasing a shot.
    public sealed class MonsterRangedPressure
    {
        Battle observed;
        int sequence;
        float quiet;
        public int Hits {get;private set;}
        public bool Step {get;private set;}
        public bool Left {get;private set;}
        public void Clear(){Hits=0;quiet=0;Step=false;}
        public void Tick(Battle state,float dt,bool available,bool suppressed)
        {
            Step=false;
            if(!ReferenceEquals(observed,state)){observed=state;sequence=0;Clear();}
            quiet+=Math.Max(0,dt);if(quiet>2)Hits=0;
            bool owns=state.Phase==GamePhase.Battle&&!suppressed&&!state.Finishing&&state.Action!=HeroAction.Beam&&
                state.Enemy!=EnemyPhase.Attack&&(state.Enemy!=EnemyPhase.Windup||state.WarningDuration-state.EnemyAge>1.1f);
            if(!owns){sequence=state.HitSequence;Clear();return;}
            if(sequence==state.HitSequence)return;
            sequence=state.HitSequence;
            if(!state.LastHitRanged){Clear();return;}
            quiet=0;Hits=Math.Min(3,Hits+1);
            if(Hits<3||!available)return;
            Hits=0;Left=state.LastHitAction==HeroAction.LeftPunch;Step=true;
        }
    }
}
