namespace UltramanGame.Core
{
    public enum BattleCaptionKind { Punch, Shield, Recovery, Beam, Resume }

    // Receipts expire with the action they describe, even if their text timer
    // is still running. This does not gate input or change combat timing.
    public static class BattleCaption
    {
        public static bool Visible(BattleCaptionKind kind,Battle battle)
        {
            if(battle.Phase!=GamePhase.Battle||battle.Finishing)return false;
            switch(kind)
            {
                case BattleCaptionKind.Shield:return battle.Shield&&!battle.IsPunch&&battle.Action!=HeroAction.Beam;
                case BattleCaptionKind.Recovery:return battle.Action==HeroAction.Hurt;
                case BattleCaptionKind.Beam:return battle.Action==HeroAction.Beam||battle.Energy>=Battle.MaxEnergy;
                default:return !battle.Shield&&battle.Action!=HeroAction.Hurt&&battle.Action!=HeroAction.Beam;
            }
        }
        public static string Gesture(BattleCaptionKind kind)=>kind==BattleCaptionKind.Shield?"shield":
            kind==BattleCaptionKind.Beam?"beam":kind==BattleCaptionKind.Punch?"punch":"neutral";
    }
}
