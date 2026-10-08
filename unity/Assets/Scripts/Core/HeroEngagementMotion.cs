using System;

namespace UltramanGame.Core
{
    // A melee exchange owns the approach, rather than replaying the entire
    // approach/retreat for every fist. This layer never submits game inputs.
    public sealed class HeroEngagementMotion
    {
        public const float HoldSeconds=.48f,RetreatSeconds=.56f;
        Battle observed;
        int sequence=-1;
        float enterFrom,hold,returnAge,returnFrom,finishAge;
        bool returning,finishing,leftLast,paused;
        public float Weight {get;private set;}
        public float LeftFoot {get;private set;}
        public float RightFoot {get;private set;}
        public float LeftLift {get;private set;}
        public float RightLift {get;private set;}
        public bool Retreating=>returning&&Weight>.001f;
        public bool Active=>Weight>.001f||returning;
        static float Clamp(float t)=>Math.Max(0,Math.Min(1,t));
        static float Smooth(float t){t=Clamp(t);return t*t*(3-2*t);}
        public void Clear()
        {Weight=LeftFoot=RightFoot=LeftLift=RightLift=enterFrom=hold=returnAge=returnFrom=finishAge=0;sequence=-1;returning=finishing=paused=false;}
        void Feet(float progress)
        {
            float first=Clamp(progress/.60f),second=Clamp((progress-.40f)/.60f);
            float near=returnFrom*(1-Smooth(first)),far=returnFrom*(1-Smooth(second));
            float a=(float)Math.Sin(first*Math.PI),b=(float)Math.Sin(second*Math.PI);
            LeftFoot=leftLast?far:near;RightFoot=leftLast?near:far;
            LeftLift=leftLast?b:a;RightLift=leftLast?a:b;
        }
        public void Tick(Battle state,float dt,bool enabled=true)
        {
            if(!ReferenceEquals(observed,state)){Clear();observed=state;}
            if(!enabled){Clear();return;}
            if(state.Phase==GamePhase.Paused){paused=true;return;}
            if(state.Phase!=GamePhase.Battle){Clear();return;}
            if(paused){paused=false;hold=0;returning=false;}
            dt=Math.Max(0,Math.Min(.1f,dt));
            LeftLift=RightLift=0;
            if(state.Finishing)
            {
                if(!finishing){finishing=true;returnFrom=Weight;finishAge=state.ActionAge;}
                float end=state.Action==HeroAction.Beam?Battle.BeamSeconds:state.AttackDuration;
                float t=Clamp((state.ActionAge-finishAge)/Math.Max(.01f,end-finishAge));
                returning=true;Weight=returnFrom*(1-Smooth(t));Feet(t);return;
            }
            finishing=false;
            if(state.Action==HeroAction.Hurt||state.Action==HeroAction.Beam)
            {returning=false;hold=HoldSeconds;LeftFoot=RightFoot=Weight;return;}
            if(state.IsPunch&&!state.IsRangedPunch)
            {
                if(sequence!=state.AttackSequence){sequence=state.AttackSequence;enterFrom=Weight;leftLast=state.Action==HeroAction.LeftPunch;}
                returning=false;hold=HoldSeconds;
                Weight=enterFrom+(1-enterFrom)*Smooth(state.ActionAge/Battle.PunchHitSeconds);
                LeftFoot=RightFoot=Weight;return;
            }
            if(state.Shield||state.IsRangedPunch)hold=0;else hold=Math.Max(0,hold-dt);
            if(hold>0){LeftFoot=RightFoot=Weight;return;}
            if(Weight<=.00001f){Clear();return;}
            if(!returning){returning=true;returnAge=0;returnFrom=Weight;}
            returnAge+=dt;float progress=Clamp(returnAge/RetreatSeconds);
            Weight=returnFrom*(1-Smooth(progress));Feet(progress);
            if(progress>=1)Clear();
        }
    }
}
