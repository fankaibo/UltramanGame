using System;

namespace UltramanGame.Core
{
    // The camera follows the contact and recovery beat, independently of the
    // input/attack clock. It never holds Battle or postpones the next gesture.
    public sealed class ComboCameraMotion
    {
        public const float Duration=.80f;
        public float Age {get;private set;}=10;
        public int Side {get;private set;}
        public int Shots {get;private set;}
        float weight;
        int lastOrdinal;
        Battle observed;
        static float Smooth(float t){t=Math.Max(0,Math.Min(1,t));return t*t*(3-2*t);}
        // Spread the dolly over a few more frames so the larger forward move
        // used by the full-body close shot does not produce a TV-jarring jump
        // at 30/60 Hz. The contact window and total shot duration stay intact.
        public float Focus=>Age>=Duration?0:Smooth(Age/.27f)*(1-Smooth((Age-.30f)/.50f))*weight;
        public void Clear(){Age=10;weight=0;Side=0;}
        public void Tick(Battle state,float dt,bool suppressed)
        {
            if(!ReferenceEquals(observed,state)){observed=state;lastOrdinal=0;Clear();}
            if(suppressed||state.Phase!=GamePhase.Battle||state.Action==HeroAction.Beam||state.Action==HeroAction.Hurt)
            {Clear();return;}
            dt=Math.Max(0,dt);Age+=dt;
            bool available=!state.Shield&&(state.Enemy==EnemyPhase.Rest||state.Enemy==EnemyPhase.Recover);
            // A close shot may now dolly farther forward so the upper-body
            // arcade framing is readable while the lower legs can fall below
            // the TV's bottom rail. Return that offset over the same gentle
            // envelope instead of snapping back when a guard or warning wins
            // ownership.
            if(!available)weight=Math.Max(0,weight-dt/.30f);
            if(available&&ComboStrikeMotion.Active(state)&&state.ActionAge<=Battle.PunchHitSeconds+.06f)
            {
                int ordinal=state.Punches+(state.ActionAge<Battle.PunchHitSeconds?1:0);
                if(ordinal!=lastOrdinal)
                {lastOrdinal=ordinal;Age=state.ActionAge;Side=state.Action==HeroAction.LeftPunch?-1:1;weight=1;Shots++;}
            }
        }
    }
}
