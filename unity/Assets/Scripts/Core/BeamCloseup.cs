using System;

namespace UltramanGame.Core
{
    // A short presentation hold. Battle still receives tracking/pause input with a zero delta.
    public sealed class BeamCloseup
    {
        public const float Duration=.98f;
        public bool Active { get; private set; }
        public float Age { get; private set; }
        public float Focus => !Active?0:Math.Min(Smooth(Age/.16f),Smooth((Duration-Age)/.24f));
        static float Smooth(float value)
        { value=Math.Max(0,Math.Min(1,value));return value*value*(3-2*value); }
        public void Begin() { Active=true;Age=0; }
        public void Cancel() { Active=false;Age=0; }
        public void Tick(float dt,Battle battle,bool suppressed=false)
        {
            if(!Active)return;
            if(suppressed||battle.Phase!=GamePhase.Battle||battle.Action!=HeroAction.Beam)
            {Cancel();return;}
            Age+=Math.Max(0,Math.Min(dt,.1f));
            if(Age>=Duration) {Age=Duration;Active=false;}
        }
    }
}
