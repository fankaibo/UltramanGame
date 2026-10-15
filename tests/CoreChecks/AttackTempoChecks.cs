using System;
using UltramanGame.Core;

static class AttackTempoChecks
{
    static Battle Started(int health=50)
    {
        var b=new Battle(health);b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
        for(int i=0;i<240;i++)b.Tick(.02f,new PlayerInput{Tracking=true});
        b.GiveInstructionTime(20);return b;
    }
    static PlayerInput Recognize(int rate,float duration,bool right)
    {
        var r=new GestureRecognizer();PlayerInput accepted=default;long seq=0,stamp=700000;
        int neutral=rate,steps=(int)Math.Ceiling(duration*rate);
        for(int f=0;f<neutral+steps+rate;f++)
        {
            var p=new PosePoint[33];for(int i=0;i<33;i++)p[i]=new PosePoint(.5f,.5f){z=-.1f};
            p[11]=new PosePoint(.65f,.35f){z=-.1f};p[12]=new PosePoint(.35f,.35f){z=-.1f};
            p[13]=new PosePoint(.68f,.50f){z=-.12f};p[14]=new PosePoint(.32f,.50f){z=-.12f};
            p[15]=new PosePoint(.58f,.44f){z=-.20f};p[16]=new PosePoint(.42f,.44f){z=-.20f};
            float t=Math.Max(0,Math.Min(1,(f-neutral)/(float)steps));int hand=right?16:15;
            p[hand].x+=(right?-.04f:.04f)*t;p[hand].y-=.04f*t;p[hand].z-=.28f*t;
            stamp+=(long)Math.Round(1000f/rate);
            var frame=new PoseFrame{schema=1,streamId="tempo",source="synthetic",sequence=++seq,capturedMs=stamp,tracked=true,points=p};
            var input=r.Update(frame,stamp,false,false);if(input.LeftPunch||input.RightPunch)accepted=input;
        }
        return accepted;
    }
    public static void Run(Action<bool,string> check)
    {
        foreach(int rate in new[]{15,30,60})foreach(bool right in new[]{false,true})
        {
            int failures=0;
            foreach(float warningAt in new[]{.28f,.48f,.70f,.9f,1.15f})
            {
                var r=new GestureRecognizer();long stamp=700000,sequence=0;int stray=0;PlayerInput last=default;
                for(int f=0;f<(2+warningAt)*rate;f++)
                {
                    float age=f/(float)rate;bool warning=age>=1+warningAt;
                    var p=new PosePoint[33];for(int i=0;i<33;i++)p[i]=new PosePoint(.5f,.5f){z=-.1f};
                    p[11]=new PosePoint(.65f,.35f){z=-.1f};p[12]=new PosePoint(.35f,.35f){z=-.1f};
                    p[13]=new PosePoint(.68f,.50f){z=-.12f};p[14]=new PosePoint(.32f,.50f){z=-.12f};
                    p[15]=new PosePoint(.58f,.44f){z=-.20f};p[16]=new PosePoint(.42f,.44f){z=-.20f};
                    float t=Math.Max(0,Math.Min(1,(age-1)/.14f));int hand=right?16:15;
                    p[hand].x+=(right?-.04f:.04f)*t;p[hand].y-=.04f*t;p[hand].z-=.28f*t;
                    if(warning)
                    {
                        p[11].y=p[12].y=.3f;p[11].z=p[12].z=0;
                        p[13].y=p[14].y=.48f;p[15]=new PosePoint(.55f,.32f){z=-.1f};p[16]=new PosePoint(.45f,.32f){z=-.1f};
                        if(age<1+warningAt+.7f)p[right?15:16].z=-.44f;
                    }
                    stamp+=(long)Math.Round(1000f/rate);
                    last=r.Update(new PoseFrame{schema=1,streamId="warning-tempo",source="synthetic",sequence=++sequence,capturedMs=stamp,tracked=true,points=p},stamp,false,false,warning);
                    if(warning&&(last.LeftPunch||last.RightPunch))stray++;
                }
                if(stray!=0||!last.Shield)failures++;
            }
            check(failures==0,$"{rate} Hz hand {right} forward punch yields to compact warning guard with initial depth bias ({failures} failures)");
        }
        foreach(int rate in new[]{15,30,60})foreach(bool right in new[]{false,true})
        {
            var slow=Recognize(rate,.65f,right);var fast=Recognize(rate,.14f,right);
            check(slow.RangedAttack&&fast.RangedAttack&&slow.RightPunch==right&&fast.RightPunch==right,
                $"{rate} Hz camera trajectory routes the correct hand to a remote skill ({right})");
            check(fast.AttackSpeed>slow.AttackSpeed*1.15f&&slow.AttackSpeed>=AttackTempo.Min&&fast.AttackSpeed<=AttackTempo.Max,
                $"{rate} Hz observed fast/slow wrist trajectories produce different bounded tempo ({slow.AttackSpeed:F2}/{fast.AttackSpeed:F2})");
        }
        foreach(int rate in new[]{15,30,60})foreach(float speed in new[]{.65f,1f,1.7f})foreach(bool ranged in new[]{false,true})
        {
            var b=Started();float dt=1f/rate,contact=-1,end=-1;
            for(int f=0;f<rate*2;f++)
            {
                b.Tick(dt,new PlayerInput{Tracking=true,LeftPunch=f==0,AttackSpeed=speed,RangedAttack=ranged});
                if(b.Punches>0&&contact<0)contact=(f+1)*dt;
                if(b.Action==HeroAction.None&&f>0&&end<0)end=(f+1)*dt;
            }
            float expectedHit=(ranged?AttackTempo.RangedHitSeconds:Battle.PunchHitSeconds)/speed;
            float expectedEnd=(ranged?AttackTempo.RangedSeconds:Battle.PunchSeconds)/speed;
            check(b.Punches==1&&b.EnemyHealth==49&&b.Energy==1,$"{rate} Hz {speed}x ranged={ranged} awards exactly one hit and energy");
            check(Math.Abs(contact-expectedHit)<=dt+.001f&&Math.Abs(end-expectedEnd)<=dt+.001f,
                $"{rate} Hz {speed}x ranged={ranged} contact and recovery use the same tempo");
        }
        foreach(bool afterLaunch in new[]{false,true})
        {
            var b=Started();b.Tick(0,new PlayerInput{Tracking=true,RightPunch=true,RangedAttack=true});
            for(int i=0;i<(afterLaunch?15:5);i++)b.Tick(.01f,new PlayerInput{Tracking=true});
            for(int i=0;i<100;i++)b.Tick(.01f,new PlayerInput{Tracking=true,GuardIntent=true,Shield=true});
            check(b.Shield&&b.Punches==(afterLaunch?1:0),$"defense takes ownership immediately; released shot survives={afterLaunch}");
        }
        {
            var b=Started();b.Tick(.1f,new PlayerInput{Tracking=true,LeftPunch=true,RangedAttack=true});b.Tick(.05f,new PlayerInput{Tracking=true});
            b.Pause();for(int i=0;i<180;i++)b.Tick(.01f,new PlayerInput{Tracking=true});
            check(b.Punches==0&&!b.Shot.Active,"pausing removes unresolved remote shots without ghost damage");
        }
        {
            var b=Started();b.Tick(.1f,new PlayerInput{Tracking=true,LeftPunch=true,RangedAttack=true,AttackSpeed=.65f});
            b.Tick(.1f,new PlayerInput{Tracking=true});
            b.Tick(.01f,new PlayerInput{Tracking=true,RightPunch=true,RangedAttack=true,AttackSpeed=1.7f});
            bool next=false;for(int i=0;i<150;i++)
            {b.Tick(.01f,new PlayerInput{Tracking=true});if(b.Action==HeroAction.RightPunch){next=true;check(b.IsRangedPunch&&b.AttackSpeed==1.7f,"buffer keeps next hand skill and tempo");break;}}
            check(next,"fast remote input can queue while a slow shot retracts");
        }
        {
            var b=Started(10);
            for(int hit=0;hit<10;hit++)
            {
                b.Tick(.01f,new PlayerInput{Tracking=true,RightPunch=true,RangedAttack=true,AttackSpeed=1.7f});
                for(int f=0;f<60;f++)b.Tick(.01f,new PlayerInput{Tracking=true});
            }
            int wins=0;while(b.TryCue(out var cue))if(cue==GameCue.Victory)wins++;
            check(b.Phase==GamePhase.Victory&&b.EnemyHealth==0&&b.Punches==10&&wins==1,
                "last remote hit finishes its recovery then enters victory exactly once");
            check(!b.Shot.Flying,"victory cannot leave a damage-bearing remote shot");
        }
        foreach(float bad in new[]{0,-1,float.NaN,float.PositiveInfinity})
            check(AttackTempo.Clamp(bad)==1,"invalid/default tempo uses normal speed");
    }
}
