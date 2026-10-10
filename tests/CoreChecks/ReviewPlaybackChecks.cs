using System;
using UltramanGame.Core;

static class ReviewPlaybackChecks
{
    public static void Run(Action<bool,string> check)
    {
        foreach(bool cycle in new[]{false,true})
        {
            var b=new Battle();var playback=new ReviewPlayback(clawCycle:cycle);
            int firstPunchAttack=0;
            for(int frame=0;frame<60*150&&b.Phase!=GamePhase.Victory;frame++)
            {
                b.Tick(1f/60,playback.Next(b,1f/60));
                if(firstPunchAttack==0&&b.Punches>0)firstPunchAttack=b.EnemyAttackCount;
                while(b.TryCue(out var cue)){}
            }
            check(firstPunchAttack==(cycle?5:2),"review waits for requested monster cycle "+cycle);
            check(b.Phase==GamePhase.Victory&&b.Punches==32&&b.HitsTaken==1&&b.Blocks>=(cycle?4:1),"cycle preserves full battle outcome "+cycle);
        }
        foreach(int hz in new[]{15,30,60})
        {
            var b=new Battle();var review=new ReviewPlayback(linkedPunches:true,guardHandoff:true);
            int heldFrames=0,beams=0;bool immediate=false;
            for(int frame=0;frame<hz*140&&b.Phase!=GamePhase.Victory;frame++)
            {
                var input=review.Next(b,1f/hz);b.Tick(1f/hz,input);
                if(input.GuardIntent){heldFrames++;immediate|=b.Shield&&b.Action==HeroAction.None&&b.Punches==1;}
                while(b.TryCue(out var cue))if(cue==GameCue.Beam)beams++;
            }
            check(immediate&&heldFrames>=hz*.25f,"review exercises real mid-melee shield ownership "+hz);
            check(review.GuardHandoffObserved,"review completes the opposite counterpunch "+hz);
            check(b.Phase==GamePhase.Victory&&b.Punches==32&&beams==2&&b.EnemyHealth==0,"review guard handoff preserves full-round balance "+hz);
            check(b.HitsTaken==1&&b.Blocks>=1&&review.Interrupted,"review retains hurt block and tracking-pause coverage "+hz);
        }
    }
}
