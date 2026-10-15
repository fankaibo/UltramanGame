using System;

namespace UltramanGame.Core
{
    public static class TransformationMotion
    {
        public const float CloseupStart=.26f, CloseupEnd=1.66f;
        public static bool Closeup(float age)=>age>=CloseupStart&&age<CloseupEnd;
        static float Smooth(float t){t=Math.Max(0,Math.Min(1,t));return t*t*(3-2*t);}
        public static float Radiance(float age)=>Smooth(age/.38f)*(1-Smooth((age-1.45f)/.65f));
    }
}
