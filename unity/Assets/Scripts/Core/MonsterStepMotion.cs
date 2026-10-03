namespace UltramanGame.Core
{
    // The landing beats are shared by foot posing and ground contact effects.
    public static class MonsterStepMotion
    {
        // Give the lead foot a fraction longer to take the monster's weight.
        // The old .36/.60 split put the contact and recovery handoff close
        // together, so the ankle changed direction too abruptly on a 30 Hz
        // TV. The attack clock and contact deadline stay unchanged; only the
        // presentation curve is spread across the same 1.05 s rush.
        public const float LandingSeconds=.40f;
        public const float ReturnStartSeconds=.63f;
        public const float ReturnLandingSeconds=.99f;
        public static bool LeadLeft(int attackCount)=>attackCount%2!=0;
    }
}
