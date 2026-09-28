using System;

namespace UltramanGame.Core
{
    // Every tenth punch changes presentation only. The airborne beat belongs
    // to the contact, so the next ordinary punch does not teleport the monster.
    public sealed class MonsterLaunchMotion
    {
        public const float Landing=.66f,Recovery=.88f,StepSeconds=.24f,Duration=1.36f;
        public float Age {get;private set;}=10;
        public int Landings {get;private set;}
        public bool Left {get;private set;}
        bool landed;
        public bool Active=>Age<Duration;
        static float Smooth(float t){t=Math.Max(0,Math.Min(1,t));return t*t*(3-2*t);}
        public static bool Uppercut(Battle state)
        {
            if(!ComboStrikeMotion.Active(state))return false;
            int ordinal=state.Punches+(state.ActionAge<Battle.PunchHitSeconds?1:0);
            return ordinal%10==0;
        }
        public float Air=>Active&&Age<Landing?(float)Math.Sin(Math.PI*Age/Landing):0;
        public float Lift=>Active&&Age<Landing?.72f*4*(Age/Landing)*(1-Age/Landing):0;
        public float Travel=>Active?Smooth(Age/.30f)*(1-Smooth((Age-Recovery)/(Duration-Recovery))):0;
        public float FootTravel(bool left)=>Active?Smooth(Age/.30f)*(1-Smooth((Age-Recovery-(left==Left?0:StepSeconds))/StepSeconds)):0;
        public float FootLift(bool left)
        {
            float t=(Age-Recovery-(left==Left?0:StepSeconds))/StepSeconds;
            return Active&&t>0&&t<1?.12f*(float)Math.Sin(Math.PI*t):0;
        }
        public float Compression=>Active&&Age>=Landing?Smooth((Age-Landing)/.07f)*(1-Smooth((Age-Landing-.07f)/.28f)):0;
        public float Camera=>Active?1-Smooth((Age-.78f)/(Duration-.78f)):0;
        public void Begin(bool left){Age=0;Left=left;landed=false;}
        public void Clear(){Age=10;landed=false;}
        public void Tick(float dt)
        {
            if(!Active)return;
            Age+=Math.Max(0,dt);
            if(!landed&&Age>=Landing){Landings++;landed=true;}
        }
    }
}
