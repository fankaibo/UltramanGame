using System;

namespace UltramanGame.Core
{
    // Every fifth normal hit gets a different presentation, not extra damage
    // or a new gesture. The ordinal stays constant across damage application.
    public static class ComboStrikeMotion
    {
        public static bool Active(Battle state)
        {
            if(state.Phase!=GamePhase.Battle||(state.Action!=HeroAction.LeftPunch&&state.Action!=HeroAction.RightPunch))return false;
            int number=state.Punches+(state.ActionAge<Battle.PunchHitSeconds?1:0);
            return number>0&&number%5==0;
        }
        static float Smooth(float value){value=Math.Max(0,Math.Min(1,value));return value*value*(3-2*value);}
        public static float Reach(float age)=>age<Battle.PunchHitSeconds?Smooth(age/Battle.PunchHitSeconds):1-Smooth((age-.15f)/(Battle.PunchSeconds-.15f));
        public static float Blend(float age)=>Smooth(age/.045f)*(1-Smooth((age-.24f)/(Battle.PunchSeconds-.24f)));
    }
}
