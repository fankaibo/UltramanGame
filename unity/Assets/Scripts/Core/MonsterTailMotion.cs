using System;

namespace UltramanGame.Core
{
    // Cumulative heading along the tail, not a rotation to add at every joint.
    // The tip follows later and farther than the base; segment lengths stay fixed.
    public static class MonsterTailMotion
    {
        static float Smooth(float t){t=Math.Max(0,Math.Min(1,t));return t*t*(3-2*t);}
        public static float AttackSweep(float clock,float along,bool left)
        {
            along=Math.Max(0,Math.Min(1,along));float delay=along*.28f;
            float load=Smooth((clock+.70f-delay)/.55f)*(1-Smooth((clock-.02f-delay)/.42f));
            float release=Smooth((clock-.04f-delay)/.48f)*(1-Smooth((clock-.72f-delay)/.85f));
            return (left?-1:1)*(3+along*18)*(-.48f*load+release);
        }
        public static float Yaw(Battle state,float clock,float along,float hitAge,float hitSide)
        {
            if(state.Phase!=GamePhase.Battle&&state.Phase!=GamePhase.Waiting)return 0;
            along=Math.Max(0,Math.Min(1,along));
            float idle=(float)Math.Sin(clock*1.35f-along*2.1f)*(2.5f+along*7);
            float sweep=0;
            if(state.Phase==GamePhase.Battle&&state.Enemy!=EnemyPhase.Rest)
            {
                float time=state.Enemy==EnemyPhase.Windup?state.EnemyAge-state.WarningDuration:
                    state.Enemy==EnemyPhase.Attack?state.EnemyAge:Battle.EnemyAttackSeconds+state.EnemyAge;
                int number=state.EnemyAttackCount+(state.Enemy==EnemyPhase.Windup?1:0);
                // Broad claw/throw turns have a counter-sweep; the frontal ray
                // and two-handed slam keep a smaller balancing motion.
                float strength=MonsterRayMotion.Active(state)||MonsterSlamMotion.Active(state)?.40f:1;
                sweep=AttackSweep(time,along,MonsterStepMotion.ClawLeft(number))*strength;
            }
            float impactTime=hitAge-along*.12f;
            float recoil=impactTime<0?0:(float)(Math.Sin(Math.Min(1,impactTime/.70f)*Math.PI)*Math.Exp(-impactTime*2.6f));
            return Math.Max(-32,Math.Min(32,idle+sweep-hitSide*recoil*along*11));
        }
    }
}
