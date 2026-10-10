using System;

namespace UltramanGame.Core
{
    // A volley is one shot: isolated casts make a small approach, successive
    // accepted casts keep the medium composition until the child stops.
    public sealed class RangedCameraMotion
    {
        public float Focus=>Smooth(weight);
        public float Travel=>Smooth(age/2.4f);
        public int Side {get;private set;}
        public int Casts {get;private set;}
        Battle observed;
        int sequence;
        float weight,hold,age;
        static float Smooth(float x){x=Math.Max(0,Math.Min(1,x));return x*x*(3-2*x);}
        void ResetValues(){weight=hold=age=0;Side=Casts=0;}
        public void Clear(){ResetValues();sequence=observed?.AttackSequence??0;}
        public void Tick(Battle state,float dt,bool suppressed=false)
        {
            if(!ReferenceEquals(observed,state)){observed=state;ResetValues();sequence=0;}
            if(suppressed||state.Phase!=GamePhase.Battle||state.Action==HeroAction.Beam||state.Action==HeroAction.Hurt)
            {Clear();return;}
            dt=Math.Max(0,dt);
            bool available=!state.Shield&&(state.Enemy==EnemyPhase.Rest||state.Enemy==EnemyPhase.Recover)&&(!state.IsPunch||state.IsRangedPunch);
            if(state.IsPunch&&state.AttackSequence!=sequence)
            {
                sequence=state.AttackSequence;
                if(available&&state.IsRangedPunch)
                {
                    if(hold<=0)
                    {
                        Casts=0;
                        // A new cast can arrive while the old lens is easing
                        // out. Keep its orbit until it has actually returned.
                        if(weight<.001f){age=0;Side=state.Action==HeroAction.LeftPunch?-1:1;}
                    }
                    Casts++;hold=.65f;
                }
            }
            if(!available){hold=0;Casts=0;}
            else if(state.IsRangedPunch&&sequence==state.AttackSequence&&Casts>0)hold=.65f;
            else hold=Math.Max(0,hold-dt);
            float desired=hold>0?(Casts>1?1:.45f):0;
            // Exact exponential response is independent of the render rate.
            // It changes only the lens; gestures and projectile clocks run on.
            float tau=desired>weight?.22f:available?.20f:.18f;
            weight=desired+(weight-desired)*(float)Math.Exp(-dt/tau);
            if(hold<=0&&weight<.0001f)ResetValues();
            else age+=dt;
        }
    }
}
