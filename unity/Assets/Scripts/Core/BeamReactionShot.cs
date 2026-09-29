using System;

namespace UltramanGame.Core
{
    // A cut after contact, followed by the original two-actor shot during fade.
    // This samples the combat clock; it never pauses or extends the beam action.
    public static class BeamReactionShot
    {
        public const float Start=.56f,End=1.28f;
        public static bool Active(Battle state)=>state.Phase==GamePhase.Battle&&state.Action==HeroAction.Beam&&
            state.ActionAge>=Start&&state.ActionAge<End;
        public static float Progress(float age)=>Math.Max(0,Math.Min(1,(age-Start)/(End-Start)));
    }
}
