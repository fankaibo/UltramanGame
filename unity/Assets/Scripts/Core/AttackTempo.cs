using System;

namespace UltramanGame.Core
{
    public static class AttackTempo
    {
        public const float Min=.65f,Max=1.7f;
        public const float RangedLaunchSeconds=.12f,RangedHitSeconds=.30f,RangedSeconds=.42f;
        public static float Clamp(float speed)=>float.IsNaN(speed)||float.IsInfinity(speed)||speed<=0?1:Math.Max(Min,Math.Min(Max,speed));
        // Velocity is wrist travel per second, normalized by shoulder width.
        // Never speed up the whole world: guides, enemy warnings and held poses
        // retain their real-time windows. Each accepted hand owns its cadence.
        public static float FromVelocity(float velocity)=>Clamp(.55f+Math.Max(0,velocity)*.22f);
        public static float Travel(float age)=>Math.Max(0,Math.Min(1,(age-RangedLaunchSeconds)/(RangedHitSeconds-RangedLaunchSeconds)));
    }
}
