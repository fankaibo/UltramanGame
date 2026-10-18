using System;

namespace UltramanGame.Core
{
    // Presentation only. Damage and the next player input retain Battle's clocks.
    public static class MonsterRecoilMotion
    {
        public const float Duration=.52f,Peak=.10f,Release=.18f;
        static float Smooth(float t){t=Math.Max(0,Math.Min(1,t));return t*t*(3-2*t);}
        public static float Weight(float age)=>Smooth(age/Peak)*(1-Smooth((age-Release)/(Duration-Release)));
        public static float Carry(float age)=>1-Smooth(age/Peak);
    }
}
