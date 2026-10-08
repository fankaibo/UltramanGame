namespace UltramanGame.Core
{
    // Once released, a light bullet can finish its flight while the child
    // retracts or defends. Pausing/new rounds cancel unresolved projectiles.
    public sealed class RangedShot
    {
        public float Age {get;private set;}=10;
        public float Speed {get;private set;}=1;
        public int Sequence {get;private set;}
        public HeroAction Side {get;private set;}
        bool applied;
        public bool Flying=>Active&&!applied;
        public bool Active=>Age<AttackTempo.RangedHitSeconds+.055f;
        public void Launch(int sequence,HeroAction side,float age,float speed)
        {Sequence=sequence;Side=side;Age=age;Speed=AttackTempo.Clamp(speed);applied=false;}
        public void Clear(){Age=10;applied=true;}
        public bool Tick(float dt)
        {
            if(!Active)return false;
            Age+=dt*Speed;
            if(applied||Age<AttackTempo.RangedHitSeconds)return false;
            applied=true;return true;
        }
    }
}
