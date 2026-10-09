namespace UltramanGame.Core
{
    // The landing beats are shared by foot posing and ground contact effects.
    public static class MonsterStepMotion
    {
        // Give the lead foot a fraction longer to take the monster's weight.
        // The old .36/.60 split put the contact and recovery handoff close
        // together, so the ankle changed direction too abruptly on a 30 Hz
        // TV. The attack clock and contact deadline stay unchanged; only the
        // presentation curve is spread across the same 1.05 s rush.
        public const float LandingSeconds=.40f;
        public const float ReturnStartSeconds=.63f;
        public const float ReturnLandingSeconds=.99f;
        // Ranged variants occupy the even slots. Explicitly alternate the two
        // remaining claw slots; the opposite foot supports the striking arm.
        public static bool ClawLeft(int attackCount)=>attackCount%6==5||attackCount%2==0;
        public static bool LeadLeft(int attackCount)=>!ClawLeft(attackCount);
        public const float ApproachLanding=.22f,TrailReturnStart=.72f,TrailReturnLanding=1.03f;
        public readonly struct Step
        {
            public readonly float Travel,Lift;
            public Step(float travel,float lift){Travel=travel;Lift=lift;}
        }
        static float Clamp(float x)=>System.Math.Max(0,System.Math.Min(1,x));
        static float Smooth(float x){x=Clamp(x);return x*x*(3-2*x);}
        static float Arc(float t,float height)=>(float)System.Math.Sin(Clamp(t)*System.Math.PI)*height;
        public static float LeadReturnLanding(float approach)=>ReturnLandingSeconds-.10f*Clamp(approach/.4f);
        public static Step Foot(float age,float approach,bool lead)
        {
            approach=System.Math.Max(0,approach);float wide=Clamp(approach/.4f);
            if(!lead)
            {
                if(age<ApproachLanding){float t=age/ApproachLanding;return new Step(approach*Smooth(t),Arc(t,.20f*wide));}
                if(age<TrailReturnStart)return new Step(approach,0);
                float back=(age-TrailReturnStart)/(TrailReturnLanding-TrailReturnStart);
                return new Step(approach*(1-Smooth(back)),Arc(back,.16f*wide));
            }
            if(age<LandingSeconds)
            {
                float t=age/LandingSeconds;
                // The trailing foot takes the extra distance first, then the
                // opposite lead foot passes it and receives the striking load.
                return new Step(.75f*Smooth(t)+approach*Smooth((age-.08f)/.32f),Arc(t,.20f));
            }
            if(age<ReturnStartSeconds)return new Step(.75f+approach,0);
            float end=LeadReturnLanding(approach);
            float retreat=(age-ReturnStartSeconds)/(end-ReturnStartSeconds);
            return new Step((.75f+approach)*(1-Smooth(retreat)),Arc(retreat,.16f));
        }
    }
}
