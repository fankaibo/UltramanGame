using System;
using UltramanGame.Core;

static class GuardBeamChecks
{
    static PosePoint P(float x,float y,float z=-.1f,float v=1)=>new PosePoint(x,y,v){z=z};
    static PosePoint[] Body(float ly=.44f,float ry=.44f,float lx=.65f,float rx=.35f)
    {
        var p=new PosePoint[33];for(int i=0;i<p.Length;i++)p[i]=P(.5f,.5f);
        p[11]=P(.65f,.35f);p[12]=P(.35f,.35f);
        p[15]=P(lx,ly,-.2f);p[16]=P(rx,ry,-.2f);return p;
    }
    sealed class Trial
    {
        public readonly GestureRecognizer R=new GestureRecognizer();public PlayerInput Last;public int Beams;
        long stamp=800000,seq;readonly int step;
        public Trial(int fps=30){step=(int)Math.Round(1000.0/fps);}
        public void Hold(PosePoint[] p,float seconds,bool enabled=true)
        {
            for(int i=0;i<Math.Ceiling(seconds*1000/step);i++)
            {
                stamp+=step;Last=R.Update(new PoseFrame{schema=1,tracked=true,streamId="guard",sequence=++seq,capturedMs=stamp,points=p},stamp,enabled,false);
                if(Last.Beam)Beams++;
            }
        }
    }
    public static void Run(Action<bool,string> check)
    {
        foreach(int fps in new[]{15,30,60})
        {
            foreach(var pose in new[]{Body(),Body(.26f,.28f),Body(.43f,.48f,.39f,.61f),Body(.48f,.55f,.58f,.42f)})
            {
                var t=new Trial(fps);t.Hold(pose,1.2f);
                check(t.Last.Shield&&t.Beams==0,$"wide, face-height, crossed or uneven chest guard stays defense at {fps} fps");
            }
        }
        var lowVisibility=Body();lowVisibility[15].visibility=lowVisibility[16].visibility=.48f;
        var trial=new Trial();trial.Hold(lowVisibility,.8f);check(trial.Last.Shield,"partially visible wrists can guard without precise elbows");
        var hidden=Body();hidden[15].visibility=.1f;
        trial.Hold(hidden,.1f);check(trial.Last.Shield,"brief wrist overlap does not drop an established shield");
        trial.Hold(hidden,.25f);check(!trial.Last.Shield,"prolonged missing wrist cannot hold a shield forever");
        var l=Body(.27f,.47f,.56f,.44f);trial=new Trial();trial.Hold(Body(),.6f);trial.Hold(l,.3f);
        check(trial.Beams==0&&trial.R.BeamProgress>0,"brief L-shaped movement only shows confirmation progress");
        trial.Hold(l,.5f);check(trial.Beams==1,"deliberate held L releases one beam");
        trial=new Trial();trial.Hold(l,1,false);trial.Hold(l,1,true);
        check(trial.Beams==0&&trial.R.BeamNeedsRelease,"pre-held pose does not auto-fire and explains that hands must first return");
        trial.Hold(Body(),.5f);trial.Hold(l,1,true);check(trial.Beams==1,"release and deliberate new pose confirm the next beam");
        trial=new Trial();trial.Hold(Body(),.6f);trial.Hold(l,1,false);
        check(trial.Beams==0&&trial.Last.Shield,"during an enemy warning an L-like guard remains defense");
        trial=new Trial();trial.Hold(Body(.72f,.72f),1.2f);check(!trial.Last.Shield&&trial.Beams==0,"hands hanging down are neither defense nor a beam");
        trial=new Trial();var raised=Body();raised[15].y=raised[16].y=.05f;trial.Hold(raised,1.2f);check(!trial.Last.Shield&&trial.Beams==0,"raised hands cannot be mistaken for a chest guard");
        var b=new Battle();b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
        for(int i=0;i<1000&&b.Enemy!=EnemyPhase.Attack;i++)b.Tick(.02f,new PlayerInput{Tracking=true});
        for(int i=0;i<9;i++)b.Tick(.02f,new PlayerInput{Tracking=true});
        b.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});
        for(int i=0;i<7;i++)b.Tick(.02f,new PlayerInput{Tracking=true});
        b.Tick(.02f,new PlayerInput{Tracking=true,Shield=true});
        for(int i=0;i<10;i++)b.Tick(.02f,new PlayerInput{Tracking=true,Shield=true});
        check(b.Blocks==1&&b.HitsTaken==0,"guard immediately interrupts punch recovery before enemy impact");
    }
}
