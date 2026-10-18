using System;

namespace UltramanGame.Core
{
    // The existing two-second recovery owns this reset gesture. It neither
    // extends the opening nor asks the child to wait before counterattacking.
    public static class MonsterRecoveryMotion
    {
        static float Smooth(float t){t=Math.Max(0,Math.Min(1,t));return t*t*(3-2*t);}
        public static float Weight(float age)=>Smooth((age-.10f)/.30f)*(1-Smooth((age-1.25f)/.65f));
        public static float HeadTurn(float age)=>(float)Math.Sin((age-.10f)*Math.PI*2/1.65f)*9*Weight(age);
        public static float Chest(float age)=>(float)Math.Sin(Math.Min(1,Math.Max(0,age/1.9f))*Math.PI)*4*Weight(age);
    }
}
