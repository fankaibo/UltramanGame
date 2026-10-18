namespace UltramanGame.Core
{
    // Presentation only: a completed claw can keep stepping through Recover.
    // Ordinary recoil owns the torso while feet finish their step. Pause,
    // beam or a new round cancels the remaining travel.
    public sealed class MonsterRetreatMotion
    {
        Battle observed;
        int attack;
        bool available;
        public float Age { get; private set; }=-1;
        public void Cancel(){available=false;Age=-1;}
        public void Observe(Battle state,bool enabled=true)
        {
            if(!ReferenceEquals(observed,state)){observed=state;available=false;attack=-1;}
            Age=-1;
            if(!enabled||state.Phase!=GamePhase.Battle||state.Action==HeroAction.Beam||
                MonsterSlamMotion.Variant(state.EnemyAttackCount)||MonsterRayMotion.Variant(state.EnemyAttackCount)||MonsterRockMotion.Variant(state.EnemyAttackCount))
            {available=false;return;}
            if(state.Enemy==EnemyPhase.Attack)
            {available=true;attack=state.EnemyAttackCount;Age=state.EnemyAge;}
            else if(state.Enemy==EnemyPhase.Recover&&available&&attack==state.EnemyAttackCount)
            {
                Age=Battle.EnemyAttackSeconds+state.EnemyAge;
            }
            else available=false;
        }
    }
}
