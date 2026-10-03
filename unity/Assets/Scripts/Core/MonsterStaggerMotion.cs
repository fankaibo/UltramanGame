using System;

namespace UltramanGame.Core
{
    // A recovery step belongs to the contact, so another ordinary punch does
    // not teleport the lifted foot back to its starting point.
    public sealed class MonsterStaggerMotion
    {
        public const float Landing=.24f,Return=.52f,Duration=.88f;
        public float Age {get;private set;}=10;
        public float Weight {get;private set;}
        public bool Left {get;private set;}
        public int Landings {get;private set;}
        bool landed;
        public bool Active=>Age<Duration&&Weight>.001f;
        static float Smooth(float t){t=Math.Max(0,Math.Min(1,t));return t*t*(3-2*t);}
        // Keep the recovery step visible through contact, but ease the root
        // carry farther into the landing.  This avoids an abrupt stop when a
        // second punch arrives during the old .43 s root envelope.
        public float Reach=>Smooth(Age/Landing)*(1-Smooth((Age-Return)/(Duration-Return)))*Weight;
        public float Root=>Smooth(Age/.29f)*(1-Smooth((Age-.48f)/(Duration-.48f)))*Weight;
        public float Lift=>Active?(Age<Landing?.20f*(float)Math.Sin(Math.PI*Age/Landing):Age>Return?.14f*(float)Math.Sin(Math.PI*(Age-Return)/(Duration-Return)):0)*Weight:0;
        public float Pitch=>Lift*(Age<Landing?40:-43);
        public void Begin(bool left){Age=0;Weight=1;Left=left;landed=false;}
        public void Clear(){Age=10;Weight=0;landed=false;}
        public void Tick(float dt,bool allowed)
        {
            Age+=dt;Weight=Math.Max(0,Math.Min(1,Weight+(allowed?1:-1)*dt/.18f));
            if(!landed&&Age>=Landing&&Age<Duration&&Weight>.95f&&allowed){Landings++;landed=true;}
            if(Age>=Duration)Weight=0;
        }
    }
}
