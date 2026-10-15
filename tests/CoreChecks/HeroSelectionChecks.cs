using System;
using UltramanGame.Core;

// Waiting-page input: unilateral waves must not start, and raising hands must
// survive camera depth noise without bypassing the post-photo release latch.
static class HeroSelectionChecks
{
    public static void Run(Action<bool,string> check)
    {
        foreach(int hz in new[]{15,30,60})
        {
            long stamp=10000,sequence=0;
            var g=new GestureRecognizer();
            int Feed(string kind,float seconds,bool available=true)
            {
                int fired=0;
                for(int i=0;i<(int)(seconds*hz);i++)
                {
                    stamp+=(long)Math.Round(1000f/hz);
                    var p=new PosePoint[33];for(int j=0;j<33;j++)p[j]=new PosePoint(.5f,.5f);
                    p[11]=new PosePoint(.65f,.4f);p[12]=new PosePoint(.35f,.4f);
                    p[13]=new PosePoint(.7f,.4f);p[14]=new PosePoint(.3f,.4f);
                    p[15]=new PosePoint(.8f,.65f);p[16]=new PosePoint(.2f,.65f);
                    if(kind=="left"||kind=="raised"||kind=="uncertain"||kind=="depth"||kind=="low-confidence-raised")p[15].y=.15f;
                    if(kind=="right"||kind=="raised"||kind=="uncertain"||kind=="depth"||kind=="low-confidence-raised")p[16].y=.15f;
                    if(kind=="depth")
                    {p[11].z=.7f;p[12].z=-.7f;p[15].visibility=p[16].visibility=.5f;}
                    if(kind=="low-confidence-raised")
                    {p[15].visibility=p[16].visibility=.36f;}
                    if(kind=="uncertain")p[15].visibility=.1f;
                    if(kind=="chest")p[15].y=p[16].y=.39f;
                    var f=new PoseFrame{schema=1,streamId="start",sequence=++sequence,capturedMs=stamp,tracked=true,points=p};
                    if(g.Update(f,stamp,false,available).Transform)fired++;
                }
                return fired;
            }
            check(Feed("left",1)==0&&Feed("right",1)==0,"single hand cannot start at "+hz);
            check(Feed("depth",1.2f)==1,"visible raised hands start despite noisy depth / moderate confidence at "+hz);
            check(Feed("depth",1)==0,"held raised hands trigger once at "+hz);
            g.Reset();
            check(Feed("low-confidence-raised",1.2f)==1,"raised hands near the camera edge start at "+hz);
            g.Reset(true);
            check(Feed("raised",1)==0,"held photo gesture cannot skip waiting at "+hz);
            check(Feed("uncertain",.15f)==0&&Feed("raised",1)==0,"uncertain wrists do not release replay at "+hz);
            Feed("neutral",.4f);
            check(Feed("raised",1)==1,"release then raise restarts at "+hz);
            g.Reset();Feed("chest",1);
            check(g.TransformProgress==0,"chest guard is not a start pose at "+hz);
            g.Reset();Feed("raised",.35f);Feed("uncertain",.1f);
            check(Feed("raised",.35f)==1,"brief wrist uncertainty pauses rather than erases start at "+hz);
            g.Reset();
            check(Feed("raised",.7f,false)==0&&Feed("raised",1)==1,"selection settling cannot consume transform before input is available at "+hz);
        }
    }
}
