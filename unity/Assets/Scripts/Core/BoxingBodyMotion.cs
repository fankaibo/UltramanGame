using System;

namespace UltramanGame.Core
{
    public static class BoxingBodyMotion
    {
        // ActionAge already includes the player's accepted speed. Map the
        // contact and recovery independently so fast punches are not sped twice.
        static float Phase(float age,bool ranged)
        {
            float hit=Battle.PunchHitSeconds,end=ranged?AttackTempo.RangedSeconds:Battle.PunchSeconds;
            return age<=hit?age/hit/3:1f/3+(age-hit)/(end-hit)*2/3;
        }
        static float Sample(float[] values,float phase)
        {
            if(float.IsNaN(phase)||phase<=0||phase>=1)return 0;
            float at=phase*(values.Length-1);int i=(int)at;float t=at-i;
            t=t*t*(3-2*t);
            return values[i]+(values[i+1]-values[i])*t;
        }
        public static float Hip(float age,bool ranged)=>Sample(BoxingBodySamples.Hip,Phase(age,ranged));
        public static float Chest(float age,bool ranged)=>Sample(BoxingBodySamples.Chest,Phase(age,ranged));
    }
}
