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
                var end=MonsterStepMotion.Foot(Battle.EnemyAttackSeconds,range,lead);
                check(Math.Abs(end.Travel)<.00001f&&Math.Abs(end.Lift)<.00001f,"rush feet return before action ends "+range+lead);
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
        // Near-distance lunge remains the established .75-unit single step.
        check(MonsterStepMotion.Foot(.22f,0,false).Travel==0&&MonsterStepMotion.Foot(.10f,0,false).Lift==0,
            "near melee does not invent a trailing approach step");
    }
}
