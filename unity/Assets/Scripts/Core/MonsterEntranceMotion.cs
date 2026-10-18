using System;

namespace UltramanGame.Core
{
    // One opening clock owns the feet, roar, camera and transition to play.
    public static class MonsterEntranceMotion
    {
        public const float Start=2.2f,LeftStart=2.27f,LeftLanding=2.70f,RightStart=2.74f,RightLanding=3.14f;
        public const float RoarStart=3.12f,CameraEnd=4.10f,End=4.40f,Distance=.48f;
        static float Clamp(float x)=>Math.Max(0,Math.Min(1,x));
        static float Smooth(float x){x=Clamp(x);return x*x*(3-2*x);}
        static float Step(float age,bool left)=>Clamp((age-(left?LeftStart:RightStart))/((left?LeftLanding:RightLanding)-(left?LeftStart:RightStart)));
        public static bool Closeup(float age)=>age>=Start&&age<CameraEnd;
        public static float FootTravel(float age,bool left)=>-Distance*(1-Smooth(Step(age,left)));
        public static float FootLift(float age,bool left)=>(float)Math.Sin(Step(age,left)*Math.PI)*.15f;
        public static float Travel(float age)=>(FootTravel(age,true)+FootTravel(age,false))*.5f;
        public static float Roar(float age)=>Smooth((age-3.00f)/.24f)*(1-Smooth((age-3.55f)/.43f));
    }
}
