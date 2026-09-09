using System;

namespace UltramanGame.Core
{
    // Brief slow motion, never a global time-scale change. Input and tracking still
    // reach Battle on every frame, including pause during an impact or closeup.
    public sealed class ImpactTiming
    {
        public float Remaining {get;private set;}
        public void Hit(bool special) {Remaining=Math.Max(Remaining,special?.085f:.055f);}
        public float Delta(float dt,GamePhase phase)
        {
            if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<0)throw new ArgumentOutOfRangeException(nameof(dt));
            if(phase!=GamePhase.Battle||Remaining<=0)return dt;
            float slowed=Math.Min(dt,Remaining);return dt-slowed+slowed*.2f;
        }
        public void Tick(float dt,GamePhase phase)
        {Remaining=phase==GamePhase.Battle?Math.Max(0,Remaining-dt):0;}
        public void Clear() {Remaining=0;}
    }
}
