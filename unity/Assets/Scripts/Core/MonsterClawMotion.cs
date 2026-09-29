using System;

namespace UltramanGame.Core
{
    // Offsets in the monster's standing right/up/forward basis. The hands lag
    // the chest impulse; a beam and an airborne reaction keep their own clocks.
    public static class MonsterClawMotion
    {
        public readonly struct Offset
        {
            public readonly float X,Y,Z;
            public Offset(float x,float y,float z){X=x;Y=y;Z=z;}
            public float Length=>(float)Math.Sqrt(X*X+Y*Y+Z*Z);
        }
        static float Smooth(float t){t=Math.Max(0,Math.Min(1,t));return t*t*(3-2*t);}
        static float Pulse(float age,float delay,float peak,float end)=>age<delay?0:
            Smooth((age-delay)/(peak-delay))*(1-Smooth((age-peak)/(end-peak)));
        public static float Carry(float age)=>1-Smooth(age/.18f);
        public static Offset Sample(bool left,float hitAge,float punchSide,bool accent,float launchAge,float beamAge)
        {
            float sign=left?-1:1;
            if(beamAge>=0&&beamAge<MonsterBeamMotion.Duration)
            {
                float t=beamAge-(left?.05f:0);
                float brace=Smooth(t/.20f)*(1-Smooth((t-1.0f)/.70f));
                return new Offset(sign*(left?.34f:.24f)*brace,(left?.32f:.17f)*brace,-.19f*brace);
            }
            if(launchAge>=0&&launchAge<MonsterLaunchMotion.Duration)
            {
                float flight=Pulse(launchAge,left?.035f:.015f,.27f,.78f);
                float land=Pulse(launchAge,MonsterLaunchMotion.Landing,.78f,1.10f);
                return new Offset(sign*(left?.46f:.35f)*flight,(left?.34f:.25f)*flight-.12f*land,-.16f*flight);
            }
            bool struck=left==(punchSide>0);
            float weight=Pulse(hitAge,struck?.015f:.05f,struck?.12f:.19f,MonsterRecoilMotion.Duration);
            float force=accent?1.2f:1;
            return new Offset(sign*(struck?.36f:-.08f)*weight*force,(struck?.22f:-.06f)*weight*force,-(struck?.12f:.22f)*weight*force);
        }
    }
}
