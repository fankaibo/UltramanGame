using System;

namespace UltramanGame.Core
{
    // Shared by the action clock, skeletal sampling, camera and landing cue.
    public static class KnockdownMotion
    {
        public const float LandingSeconds=.30f, RiseSeconds=.68f, BraceSeconds=1.12f, Duration=1.65f;
        static float Ease(float t) { t=Math.Max(0,Math.Min(1,t));return t*t*(3-2*t); }
        public static float Weight(float age)
        {
            if(age<LandingSeconds)return Ease(age/LandingSeconds);
            if(age<RiseSeconds)return 1;
            if(age<BraceSeconds)return 1-.62f*Ease((age-RiseSeconds)/(BraceSeconds-RiseSeconds));
            return .38f*(1-Ease((age-BraceSeconds)/(Duration-BraceSeconds)));
        }
        // Reuse the authored arm/hip recoil, allowing it to settle on the
        // ground before sampling its recovery. All five heroes share this clip.
        public static float ClipSeconds(float age)
        {
            if(age<LandingSeconds)return .20f*Ease(age/LandingSeconds);
            if(age<RiseSeconds)return .20f+.025f*Ease((age-LandingSeconds)/(RiseSeconds-LandingSeconds));
            if(age<BraceSeconds)return .225f+.125f*Ease((age-RiseSeconds)/(BraceSeconds-RiseSeconds));
            return .35f+.20f*Ease((age-BraceSeconds)/(Duration-BraceSeconds));
        }
    }
}
