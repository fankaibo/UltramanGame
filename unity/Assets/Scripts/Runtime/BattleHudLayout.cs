using UnityEngine;

namespace UltramanGame.Runtime
{
    // Virtual 1280 x 720 coordinates shared by the HUD and framing review.
    public static class BattleHudLayout
    {
        public static Rect HeroPlate => new Rect(24, 10, 332, 50);
        public static Rect EnemyPlate => new Rect(924, 10, 332, 50);
        public static Rect Combo => new Rect(28, 242, 140, 80);
        public static Rect Damage => new Rect(928, 69, 54, 24);
        public static Rect Warning => new Rect(988, 67, 268, 25);
        public static Rect Energy => new Rect(74, 42, 260, 9);
        public static Rect EnemyHealth => new Rect(946, 42, 260, 9);
        // Keep the instruction rail below the planted feet in the tighter
        // arcade lens while leaving the sentence readable from a TV.
        public static Rect Guide => new Rect(370, 698, 540, 18);
        public static Rect BeamTitle => new Rect(420, 698, 440, 18);
    }
}
