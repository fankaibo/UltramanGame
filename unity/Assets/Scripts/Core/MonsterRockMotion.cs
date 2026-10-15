using System;

namespace UltramanGame.Core
{
    public static class MonsterRockMotion
    {
        public const float Launch=.22f,Contact=.78f;
        public static bool Variant(int attack)=>attack>0&&attack%6==2;
        public static bool Active(Battle state)=>state.Phase==GamePhase.Battle&&state.Enemy!=EnemyPhase.Rest&&
            Variant(state.EnemyAttackCount+(state.Enemy==EnemyPhase.Windup?1:0));
        public static float Smooth(float t){t=Math.Max(0,Math.Min(1,t));return t*t*(3-2*t);}
        public static float Prepare(Battle state)=>state.Enemy==EnemyPhase.Windup?Smooth((state.EnemyAge-state.WarningDuration+1.6f)/1.6f):
            state.Enemy==EnemyPhase.Attack?1-Smooth((state.EnemyAge-.35f)/.55f):0;
        public static float Travel(float age)=>Math.Max(0,Math.Min(1,(age-Launch)/(Contact-Launch)));
        public static float Arc(float age)=>(float)Math.Sin(Travel(age)*Math.PI)*.65f;
    }
}
