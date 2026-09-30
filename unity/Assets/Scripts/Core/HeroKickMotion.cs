using System;

namespace UltramanGame.Core
{
    // A leg variation of an accepted combo input. The ordinal, single damage
    // event and energy award remain owned by Battle, never the animation.
    public static class HeroKickMotion
    {
        public static bool Active(Battle state)
        {
            if(!ComboStrikeMotion.Active(state))return false;
            int ordinal=state.Punches+(state.ActionAge<Battle.PunchHitSeconds?1:0);
            return ordinal%20==5;
        }
        static float Smooth(float t){t=Math.Max(0,Math.Min(1,t));return t*t*(3-2*t);}
        public static float Chamber(float age)=>Smooth(age/.07f)*(1-Smooth((age-.21f)/.16f));
        public static float Extension(float age)=>Smooth((age-.045f)/.075f)*(1-Smooth((age-.15f)/.09f));
        public static float Drive(float age)=>Smooth(age/.12f)*(1-Smooth((age-.16f)/.22f));
    }
}
