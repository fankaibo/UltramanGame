using System;

namespace UltramanGame.Core
{
    // Presentation-only sampling of the authored windup. The warning and
    // contact clocks remain in Battle; a delayed voice may extend the hold.
    public static class MonsterWindupMotion
    {
        static float Smooth(float x){x=Math.Max(0,Math.Min(1,x));return x*x*(3-2*x);}
        public static float Coil(float age)=>Smooth(age/.8f)*(1-Smooth((age-1.9f)/1.2f));
        public static float Breath(float age,float duration)=>(float)Math.Sin(age*2.6f)*Smooth(age/.7f)*Smooth((duration-age)/.7f);
        public static float Clip(float age,float duration,float length)
        {
            float hold=age<.5f?.4f*Smooth(age/.5f):age<1.9f?.4f+.65f*Smooth((age-.5f)/1.4f):
                age<3.0f?1.05f-.45f*Smooth((age-1.9f)/1.1f):.60f+.045f*(float)Math.Sin((age-3)*2.5f);
            float prepare=Smooth((age-duration+1.1f)/1.1f);
            return Math.Min(length,hold+(length-hold)*prepare);
        }
    }
}
