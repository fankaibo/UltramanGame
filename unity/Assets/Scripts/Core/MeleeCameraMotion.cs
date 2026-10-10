using System;

namespace UltramanGame.Core
{
    // An accepted string of fists shares its composition. Contact shake stays
    // separate; the lens no longer returns home and reverses orbit every fist.
    public sealed class MeleeCameraMotion
    {
        Battle observed;
        int sequence,count;
        float weight,hold,age;
        public int Side {get;private set;}
        public float Focus=>weight*weight*(3-2*weight);
        public float Travel=>Math.Min(1,age/2);
        public void Clear(){weight=hold=age=0;Side=count=0;sequence=observed?.AttackSequence??0;}
        public void Tick(Battle state,float dt,bool suppressed=false)
        {
            if(!ReferenceEquals(observed,state)){observed=state;Clear();sequence=0;}
            if(suppressed||state.Phase!=GamePhase.Battle||state.Action==HeroAction.Beam||state.Action==HeroAction.Hurt)
            {Clear();return;}
            if(dt<=0)return;
            bool available=!state.Shield&&!state.IsRangedPunch&&
                (state.Enemy==EnemyPhase.Rest||state.Enemy==EnemyPhase.Recover);
            if(state.IsPunch&&state.AttackSequence!=sequence)
            {
                sequence=state.AttackSequence;
                if(available)
                {
                    if(weight<.001f){Side=state.Action==HeroAction.LeftPunch?-1:1;age=0;count=0;}
                    count++;hold=.48f;
                }
            }
            if(!available){hold=0;count=0;}
            else if(state.IsPunch&&count>0)hold=.48f;
            else hold=Math.Max(0,hold-dt);
            float desired=hold>0?(count>1?1:.65f):0;
            float tau=desired>weight?.16f:available?.30f:.12f;
            weight=desired+(weight-desired)*(float)Math.Exp(-dt/tau);
            if(hold<=0&&weight<.0001f)Clear();else age+=dt;
        }
    }
}
