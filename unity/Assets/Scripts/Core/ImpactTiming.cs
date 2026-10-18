using System;

namespace UltramanGame.Core
{
    // One contact clock for poses and combat. A short hold then slow release
    // avoids skipping animation frames while a separate renderer was frozen.
    // Input and tracking still reach Battle every frame; no global time scale.
    public sealed class ImpactTiming
    {
        public float Remaining {get;private set;}
        public float HoldRemaining {get;private set;}
        public void Hit(bool special)
        {
            HoldRemaining=Math.Max(HoldRemaining,special?.14f:.065f);
            Remaining=Math.Max(Remaining,HoldRemaining+(special?.085f:.055f));
        }
        public float Delta(float dt,GamePhase phase)
        {
            if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<0)throw new ArgumentOutOfRangeException(nameof(dt));
            if(phase!=GamePhase.Battle||Remaining<=0)return dt;
            float held=Math.Min(dt,HoldRemaining);
            float slowed=Math.Min(dt-held,Math.Max(0,Remaining-HoldRemaining));
            return dt-held-slowed+slowed*.2f;
        }
        public void Tick(float dt,GamePhase phase)
        {
            Remaining=phase==GamePhase.Battle?Math.Max(0,Remaining-dt):0;
            HoldRemaining=phase==GamePhase.Battle?Math.Max(0,HoldRemaining-dt):0;
        }
        public void Clear() {Remaining=HoldRemaining=0;}
    }
}
