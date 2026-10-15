using System;

namespace UltramanGame.Core
{
    public static class CombatSpacing
    {
        public const float OriginalDistance=2.70115f,StandingDistance=4.10f;
        public static float Approach(float distance)=>Math.Max(0,distance-OriginalDistance);
    }
    public static class HeroArsenal
    {
        public static bool Sluggers(string id)=>id=="Zero";
        public static bool Blade(string id,Battle state)=>id=="Mebius"&&state.Phase==GamePhase.Battle&&
            state.Action==HeroAction.LeftPunch&&!state.IsRangedPunch&&!ComboStrikeMotion.Active(state);
        public static string RangedName(string id)=>Sluggers(id)?"赛罗头镖":"光弹";
    }
}
