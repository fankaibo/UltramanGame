using System;
using UltramanGame.Core;

static class MonsterRushChecks
{
    public static void Run(Action<bool,string> check)
    {
        foreach(float range in new[]{0,.2f,CombatSpacing.StandingDistance-CombatSpacing.OriginalDistance})
        {
            var contact=MonsterStepMotion.Foot(Battle.EnemyHitSeconds,range,true);
            check(Math.Abs(contact.Travel-(.75f+range))<.00001f&&Math.Abs(contact.Lift)<.00001f,"claw contact has lead foot planted at full range "+range);
            foreach(bool lead in new[]{false,true})
            {
                var end=MonsterStepMotion.Foot(MonsterStepMotion.TrailReturnLanding,range,lead);
                check(Math.Abs(end.Travel)<.00001f&&Math.Abs(end.Lift)<.00001f,"rush feet return within existing recovery "+range+lead);
                var first=MonsterStepMotion.Foot(0,range,lead);
                check(first.Travel==0&&first.Lift==0,"rush begins at resting feet "+range+lead);
                float last=-1;bool stationary=true;
                for(float t=lead?.42f:.24f;t<(lead?.62f:.70f);t+=.01f)
                {
                    var sample=MonsterStepMotion.Foot(t,range,lead);
                    stationary&=Math.Abs(sample.Lift)<.00001f&&(last<0||Math.Abs(sample.Travel-last)<.00001f);last=sample.Travel;
                }
                check(stationary,"grounded rush foot does not translate "+range+lead);
            }
            foreach(int hz in new[]{15,30,60})
            {
                bool finite=true;for(int f=0;f<hz*2;f++)foreach(bool lead in new[]{false,true})
                {var p=MonsterStepMotion.Foot(f/(float)hz,range,lead);finite&=!float.IsNaN(p.Travel)&&p.Lift>=-.00001f&&p.Lift<=.201f&&p.Travel>=-.00001f&&p.Travel<=range+.75001f;}
                check(finite,"rush paths remain bounded above floor "+range+"/"+hz);
            }
        }
        foreach(int hz in new[]{15,30,60})foreach(string mode in new[]{"complete","hit","pause","new-round","preview"})
        {
            var b=new Battle();var motion=new MonsterRetreatMotion();float dt=1f/hz;
            b.Tick(dt,new PlayerInput{Tracking=true,Transform=true});int budget=hz*40;
            while(!(b.Enemy==EnemyPhase.Recover&&b.EnemyAge>.10f)&&budget-->0)
            {b.Tick(dt,new PlayerInput{Tracking=true,Shield=true});motion.Observe(b);}
            check(budget>0&&motion.Age>Battle.EnemyAttackSeconds,"retreat crosses attack to recovery "+hz+mode);
            if(mode=="pause")b.Pause();
            if(mode=="new-round")b=new Battle();
            // An enabled=false preview also must release the presentation channel.
            if(mode=="preview")motion.Observe(b,false);
            bool sawHit=false,valid=true;int hits=b.HitSequence;
            for(int frame=0;frame<hz;frame++)
            {
                b.Tick(dt,new PlayerInput{Tracking=true,LeftPunch=mode=="hit"&&frame==0,Shield=mode!="hit"});
                motion.Observe(b);sawHit|=b.HitSequence>hits;
                valid&=mode=="complete"||mode=="hit"?motion.Age>=Battle.EnemyAttackSeconds:motion.Age<0;
            }
            check(valid,"retreat continuation or cancellation stays consistent "+hz+mode);
            if(mode=="complete")check(MonsterStepMotion.Foot(motion.Age,1.4f,true).Travel==0&&MonsterStepMotion.Foot(motion.Age,1.4f,false).Travel==0,"withdrawal finishes inside recover "+hz);
            if(mode=="hit")check(sawHit,"counterattack actually contacts during retreat "+hz);
        }
        // Near-distance lunge remains the established .75-unit single step.
        check(MonsterStepMotion.Foot(.22f,0,false).Travel==0&&MonsterStepMotion.Foot(.10f,0,false).Lift==0,
            "near melee does not invent a trailing approach step");
    }
}
