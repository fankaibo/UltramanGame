using UnityEngine;

namespace UltramanGame.Runtime
{
    // Virtual 1280 x 720 coordinates shared by the HUD and framing review.
    public static class BattleHudLayout
    {
        public static Rect Guide => new Rect(370, 662, 540, 49);
        public static Rect BeamTitle => new Rect(420, 667, 440, 40);
    }
}
