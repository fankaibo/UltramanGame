using System;

namespace UltramanGame.Core
{
    // Coil only the next fist the player has actually queued. Carry that pose
    // through the one-frame handoff, then release it before contact.
    public sealed class HeroPunchLink
    {
        Battle observed;
        bool releasing;
        float releaseFrom;
        public HeroAction Side {get;private set;}
        public float Weight {get;private set;}
        public float Sign=>Side==HeroAction.LeftPunch?-1:1;
        static float Smooth(float value){float t=Math.Max(0,Math.Min(1,value));return t*t*(3-2*t);}
        static float Move(float from,float to,float max)=>from<to?Math.Min(to,from+max):Math.Max(to,from-max);
        public void Clear(){Side=HeroAction.None;Weight=releaseFrom=0;releasing=false;}
        public void Tick(Battle state,float dt,bool enabled=true)
        {
            if(!ReferenceEquals(observed,state)){Clear();observed=state;}
            if(!enabled||state.Phase!=GamePhase.Battle||state.Shield||state.Action==HeroAction.Beam||state.Action==HeroAction.Hurt)
            {Clear();return;}
            var next=state.BufferedPunch;
            if(next!=HeroAction.None&&next!=state.Action)
            {
                if(Side!=next){Side=next;Weight=0;}
                releasing=false;
                float target=Smooth((state.ActionAge-(Battle.PunchSeconds-.18f))/.16f);
                Weight=Move(Weight,target,dt*10);
            }
            else if(state.Action==Side&&Side!=HeroAction.None&&Weight>0)
            {
                if(!releasing){releasing=true;releaseFrom=Weight;}
                Weight=releaseFrom*(1-Smooth(state.ActionAge/Battle.PunchHitSeconds));
            }
            else Weight=Move(Weight,0,dt*8);
            if(Weight<=0)Clear();
        }
    }
}
