using System;

namespace UltramanGame.Core
{
    // The planted recovery foot belongs to the beam contact and survives a
    // following ordinary punch. Combat damage and input clocks stay untouched.
    public sealed class MonsterBeamMotion
    {
        public const float Landing=.36f,Return=1.12f,FootHome=1.70f,Duration=1.90f;
        public float Age {get;private set;}=10;
        public bool Left {get;private set;}
        public int Landings {get;private set;}
        bool landed;
        public bool Active=>Age<Duration;
        static float Smooth(float t){t=Math.Max(0,Math.Min(1,t));return t*t*(3-2*t);}
        public float Weight=>Active?Smooth(Age/.12f)*(1-Smooth((Age-1.02f)/.70f)):0;
        public float Chest=>Weight*(1+.045f*(float)Math.Sin(Age*24));
        public float Reach=>Active?Smooth(Age/Landing)*(1-Smooth((Age-Return)/(FootHome-Return))):0;
        public float Travel=>Active?.32f*Smooth(Age/.40f)*(1-Smooth((Age-1.02f)/(Duration-1.02f))):0;
        public float Lift=>!Active?0:Age<Landing?.20f*(float)Math.Sin(Math.PI*Age/Landing):Age>Return&&Age<FootHome?.14f*(float)Math.Sin(Math.PI*(Age-Return)/(FootHome-Return)):0;
        public float Pitch=>Lift*(Age<Landing?35:-35);
        public float Clip=>Age<1.02f?.16f*Smooth(Age/.14f):.16f+.24f*Smooth((Age-1.02f)/(Duration-1.02f));
        public void Begin(bool left){Age=0;Left=left;landed=false;}
        public void Clear(){Age=10;landed=false;}
        public void Tick(float dt)
        {
            if(!Active)return;Age+=Math.Max(0,dt);
            if(!landed&&Age>=Landing){Landings++;landed=true;}
        }
    }
}
