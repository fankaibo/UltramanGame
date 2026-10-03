using System;

namespace UltramanGame.Core
{
    // Shared presentation beats; combat and automatic photo timing are unchanged.
    public static class VictoryMotion
    {
        public const float StaggerSeconds=.24f, LandingSeconds=1.1f;
        public const float TurnStartSeconds=1.3f, TurnSeconds=1.4f;
        public const float FadeStartSeconds=2.65f, FadeSeconds=.9f;
        static float Smooth(float t){t=Math.Max(0,Math.Min(1,t));return t*t*(3-2*t);}
        public static float Collapse(float age)=>Smooth((age-StaggerSeconds)/(LandingSeconds-StaggerSeconds));
        public static float Turn(float age)=>Math.Max(0,Math.Min(1,(age-TurnStartSeconds)/TurnSeconds));
        public static float Opacity(float age)=>1-Smooth((age-FadeStartSeconds)/FadeSeconds);
    }
}
