namespace UltramanGame.Core
{
    // The landing beats are shared by foot posing and ground contact effects.
    public static class MonsterStepMotion
    {
        public const float LandingSeconds=.36f;
        public const float ReturnStartSeconds=.60f;
        public const float ReturnLandingSeconds=.99f;
        public static bool LeadLeft(int attackCount)=>attackCount%2!=0;
    }
}
