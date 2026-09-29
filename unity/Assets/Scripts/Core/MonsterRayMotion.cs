using System;

namespace UltramanGame.Core
{
    // Fourth attack in each six-attack cycle. The existing battle still owns
    // warnings, shield input and its single contact deadline.
    public static class MonsterRayMotion
    {
        public const float LaunchSeconds=.16f,FadeSeconds=.70f,EndSeconds=.92f;
        public static bool Variant(int attack)=>attack>0&&attack%6==4;
        public static bool Active(Battle state)=>state.Phase==GamePhase.Battle&&state.Enemy!=EnemyPhase.Rest&&
            Variant(state.EnemyAttackCount+(state.Enemy==EnemyPhase.Windup?1:0));
        static float Clamp(float x)=>Math.Max(0,Math.Min(1,x));
        static float Smooth(float x){x=Clamp(x);return x*x*(3-2*x);}
        public static float Prepare(Battle state)=>state.Enemy==EnemyPhase.Windup?Smooth((state.EnemyAge-state.WarningDuration+1.5f)/1.5f):
            state.Enemy==EnemyPhase.Attack?1-Smooth((state.EnemyAge-FadeSeconds)/.35f):0;
        public static float Travel(float age)=>Clamp((age-LaunchSeconds)/(Battle.EnemyHitSeconds-LaunchSeconds));
        public static float Power(float age)=>Smooth((age-LaunchSeconds)/.07f)*(1-Smooth((age-FadeSeconds)/(EndSeconds-FadeSeconds)));
        public static float Recoil(float age)=>Smooth((age-LaunchSeconds)/.09f)*(1-Smooth((age-.5f)/.45f));
    }
}
