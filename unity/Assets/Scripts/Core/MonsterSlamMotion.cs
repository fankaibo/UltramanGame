using System;

namespace UltramanGame.Core
{
    // Every third telegraphed attack uses a ground strike. It observes Battle;
    // shield inputs, warning time and the damage deadline remain unchanged.
    public static class MonsterSlamMotion
    {
        public const float GroundSeconds=.28f;
        public static bool Variant(int attack)=>attack>0&&attack%3==0;
        public static bool Active(Battle state)=>state.Phase==GamePhase.Battle&&
            Variant(state.EnemyAttackCount+(state.Enemy==EnemyPhase.Windup?1:0))&&state.Enemy!=EnemyPhase.Rest;
        static float Smooth(float t){t=Math.Max(0,Math.Min(1,t));return t*t*(3-2*t);}
        public static float Prepare(Battle state)=>state.Enemy==EnemyPhase.Windup?Smooth((state.EnemyAge-state.WarningDuration+1.35f)/1.35f):
            state.Enemy==EnemyPhase.Attack?1:1-Smooth(state.EnemyAge/.25f);
        public static float Down(float age)=>age<GroundSeconds?Smooth(age/GroundSeconds):age<.50f?1:1-Smooth((age-.50f)/(Battle.EnemyAttackSeconds-.50f));
        public static float Travel(float age)=>age<GroundSeconds?-.16f+.40f*Smooth(age/GroundSeconds):.24f*Down(age);
    }
}
