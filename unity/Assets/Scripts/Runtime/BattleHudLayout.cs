using UnityEngine;

namespace UltramanGame.Runtime
{
    // Virtual 1280 x 720 coordinates shared by the HUD and framing review.
    public static class BattleHudLayout
    {
        public static Rect HeroPlate => new Rect(24, 12, 424, 58);
        public static Rect EnemyPlate => new Rect(832, 12, 424, 58);
        public static Rect Energy => new Rect(94, 48, 330, 12);
        public static Rect EnemyHealth => new Rect(850, 48, 332, 12);
        public static Rect Guide => new Rect(370, 662, 540, 49);
        public static Rect BeamTitle => new Rect(420, 667, 440, 40);
    }
}
