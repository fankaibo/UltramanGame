namespace UltramanGame.Core
{
    // The landing beats are shared by foot posing and ground contact effects.
    public static class MonsterStepMotion
    {
        // The contact still lands at .40 s. Withdrawal continues into the
        // existing recovery phase, leaving time to transfer the creature's
        // weight before the second foot lifts.
        public const float LandingSeconds=.40f;
        public const float ReturnStartSeconds=.80f;
        public const float ReturnLandingSeconds=1.38f;
        // Ranged variants occupy the even slots. Explicitly alternate the two
        // remaining claw slots; the opposite foot supports the striking arm.
        public static bool ClawLeft(int attackCount)=>attackCount%6==5||attackCount%2==0;
        public static bool LeadLeft(int attackCount)=>!ClawLeft(attackCount);
        public const float ApproachLanding=.22f,TrailReturnStart=1.13f,TrailReturnLanding=1.68f;
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
