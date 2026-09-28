using System;

namespace UltramanGame.Core
{
    // Open the two-fighter composition before the lunge. The same clock spans
    // the warning, claw contact and retreat; it never delays combat or input.
    public sealed class EnemyExchangeMotion
    {
        public const float LeadSeconds=.60f, ReturnStart=Battle.EnemyAttackSeconds, ReturnSeconds=.55f;
        public float Focus {get;private set;}
        public float Travel {get;private set;}
        public int Side {get;private set;}
        Battle observed;
        static float Ease(float t){t=Math.Max(0,Math.Min(1,t));return t*t*(3-2*t);}
        public void Clear(){Focus=Travel=0;Side=0;}
        public void Tick(Battle state,float dt,bool suppressed)
        {
            if(!ReferenceEquals(observed,state)){observed=state;Clear();}
            if(suppressed||state.Phase!=GamePhase.Battle||state.Action==HeroAction.Beam){Clear();return;}
            float desired=Envelope(state);
            // A delayed voice cue can extend/cancel a warning. Ease out from
            // the current shot rather than snapping when its clock changes.
            Focus=Math.Max(desired,Math.Max(0,Focus-Math.Max(0,dt)/.24f));
            if(desired>0)
            {
                Side=(state.EnemyAttackCount+(state.Enemy==EnemyPhase.Windup?1:0))%2==0?-1:1;
                Travel=state.Enemy==EnemyPhase.Windup?0:state.Enemy==EnemyPhase.Attack?Ease(state.EnemyAge/Battle.EnemyHitSeconds):1;
            }
        }
        static float Envelope(Battle state)
        {
            if(state.Action==HeroAction.Hurt)return 1;
            if(state.Enemy==EnemyPhase.Windup)return Ease(1-(state.WarningDuration-state.EnemyAge)/LeadSeconds);
            if(state.Enemy==EnemyPhase.Attack)return 1-Ease((state.EnemyAge-ReturnStart)/ReturnSeconds);
            if(state.Enemy==EnemyPhase.Recover)return 1-Ease((Battle.EnemyAttackSeconds+state.EnemyAge-ReturnStart)/ReturnSeconds);
            return 0;
        }
    }
}
