using System;
using UltramanGame.Core;

static class MotionChecks
{
    sealed class Trial
    {
        public readonly GestureRecognizer Recognizer=new GestureRecognizer();
        public int Left,Right,Beams,Transforms,Forward;
        public PlayerInput Last;
        public string Stream="motion";
        public readonly int Fps;
        long sequence,stamp=500000;
        public Trial(int fps=30) {Fps=fps;}
        public PoseFrame Frame(PosePoint[] points)
        {stamp+=(long)Math.Round(1000.0/Fps);return new PoseFrame {schema=1,source="synthetic",streamId=Stream,sequence=++sequence,capturedMs=stamp,tracked=true,points=points};}
        public void Receive(PoseFrame frame,bool beam=false,bool transform=false)
        {
            Last=Recognizer.Update(frame,frame.capturedMs,beam,transform);
            if(Last.LeftPunch)Left++;if(Last.RightPunch)Right++;if(Last.Beam)Beams++;if(Last.Transform)Transforms++;
            if((Last.LeftPunch||Last.RightPunch)&&Recognizer.ForwardPunch)Forward++;
        }
        public void Hold(PosePoint[] points,float seconds=.6f,bool beam=false,bool transform=false)
        {for(int i=0;i<(int)Math.Ceiling(seconds*Fps);i++)Receive(Frame(points),beam,transform);}
        public void Move(PosePoint[] from,PosePoint[] to,float seconds=.22f,bool beam=false)
        {
            int n=(int)Math.Ceiling(seconds*Fps);
            for(int k=1;k<=n;k++)
            {
                var p=(PosePoint[])to.Clone();float t=k/(float)n;
                for(int i=0;i<33;i++) {p[i].x=from[i].x+(to[i].x-from[i].x)*t;p[i].y=from[i].y+(to[i].y-from[i].y)*t;p[i].z=from[i].z+(to[i].z-from[i].z)*t;}
                Receive(Frame(p),beam);
            }
        }
    }
    static PosePoint Point(float x,float y,float z=-.1f) => new PosePoint(x,y) {z=z};
    static PosePoint[] Guard()
    {
        var p=new PosePoint[33];for(int i=0;i<33;i++)p[i]=Point(.5f,.5f);
        p[11]=Point(.65f,.35f);p[12]=Point(.35f,.35f);
        p[13]=Point(.68f,.50f,-.12f);p[14]=Point(.32f,.50f,-.12f);
        p[15]=Point(.58f,.44f,-.20f);p[16]=Point(.42f,.44f,-.20f);
        return p;
    }
    static PosePoint[] Forward(bool right=false)
    {var p=Guard();p[right?16:15]=Point(right?.38f:.62f,.40f,-.48f);return p;}
    static PosePoint[] Beam()
    {var p=Guard();p[15]=Point(.60f,.30f,-.18f);p[16]=Point(.44f,.46f,-.20f);return p;}
    static PosePoint[] Push()
    {var p=Guard();p[15]=Point(.66f,.42f,-.47f);p[16]=Point(.34f,.42f,-.47f);return p;}
    static PosePoint[] Scale(PosePoint[] source,float factor)
    {var p=(PosePoint[])source.Clone();for(int i=0;i<33;i++) {p[i].x=.5f+(p[i].x-.5f)*factor;p[i].y=.5f+(p[i].y-.5f)*factor;p[i].z*=factor;}return p;}
    public static void Run(Action<bool,string> check)
    {
        foreach(int fps in new[]{15,30,60})
        {
            var t=new Trial(fps);t.Hold(Guard());t.Move(Guard(),Forward());t.Hold(Forward(),1.5f);
            check(t.Left==1&&t.Right==0&&t.Forward==1&&!t.Last.Shield,$"forward camera punch works at {fps} fps without repeating or becoming a held shield");
            t.Move(Forward(),Guard());t.Hold(Guard(),.15f);t.Move(Guard(),Forward());t.Hold(Forward());
            check(t.Left==2,$"forward punch rearms after a relaxed return at {fps} fps");
        }
        var trial=new Trial();trial.Hold(Guard());trial.Move(Guard(),Forward(true));trial.Hold(Forward(true));
        check(trial.Right==1&&trial.Left==0,"right-hand forward punch is independent");
        trial=new Trial();var highPunch=Forward();highPunch[15].y=.33f;
        trial.Hold(Guard(),beam:true);trial.Move(Guard(),highPunch,beam:true);trial.Hold(highPunch,1,true);
        check(trial.Left==1&&trial.Beams==0,"forward punch at shoulder height is not intercepted by a loose beam at full energy");
        trial=new Trial();var hidden=Forward();hidden[13].visibility=.1f;trial.Hold(Guard());trial.Move(Guard(),hidden);trial.Hold(hidden);
        check(trial.Left==1,"visible wrist can punch forward when its elbow is hidden");
        trial=new Trial();trial.Hold(Scale(Guard(),.55f));trial.Move(Scale(Guard(),.55f),Scale(Forward(),.55f));trial.Hold(Scale(Forward(),.55f));
        check(trial.Left==1,"smaller on-screen body uses relative motion without precise reach");
        trial=new Trial();var up=Guard();up[15]=Point(.59f,.08f,-.2f);var down=Guard();down[15]=Point(.59f,.72f,-.2f);
        trial.Hold(Guard());for(int i=0;i<3;i++){trial.Move(Guard(),up);trial.Hold(up);trial.Move(up,down);trial.Hold(down);trial.Move(down,Guard());}
        check(trial.Left==0&&trial.Right==0,"raising and lowering a hand cannot substitute for forward punches");
        trial=new Trial();trial.Hold(Guard());trial.Receive(trial.Frame(Forward()));trial.Hold(Guard());
        check(trial.Left==0,"one-frame depth spike cannot punch");
        trial=new Trial();trial.Hold(Guard());var noisy=Guard();
        for(int i=0;i<120;i++){noisy=Guard();noisy[15].z+=(i%2==0?.025f:-.025f);trial.Receive(trial.Frame(noisy));}
        check(trial.Left==0,"small alternating depth noise cannot punch");
        trial=new Trial();trial.Hold(Guard());var translated=Guard();
        for(int i=0;i<33;i++){translated[i].x+=.04f;translated[i].y-=.05f;translated[i].z-=.4f;}
        trial.Move(Guard(),translated);trial.Hold(translated);
        check(trial.Left==0&&trial.Right==0,"moving the whole torso toward the camera does not punch");
        trial=new Trial();trial.Hold(Guard());var missing=Forward();missing[15].visibility=.1f;
        trial.Move(Guard(),missing);trial.Hold(missing);trial.Hold(Forward());
        check(trial.Left==0,"missing wrist and reacquisition of an already extended hand cannot punch");
        trial=new Trial();trial.Hold(Guard());trial.Stream="new-camera";trial.Hold(Forward());
        check(trial.Left==0,"new camera stream cannot finish the previous stream's punch");
        trial=new Trial();trial.Hold(Guard());var repeated=trial.Frame(Forward());
        for(int i=0;i<30;i++)trial.Receive(repeated);trial.Hold(Guard());
        check(trial.Left==0,"replaying one depth sample cannot supply temporal confirmation");
        trial=new Trial();var loose=Beam();loose[13].visibility=loose[14].visibility=.1f;
        trial.Hold(Guard());trial.Hold(loose,1,true);
        check(trial.Beams==1&&trial.Left==0&&trial.Right==0,"loose L gesture works without exact elbow angles or visible elbows");
        trial=new Trial();trial.Hold(Guard());trial.Move(Guard(),Push(),beam:true);trial.Hold(Push(),1,true);
        check(trial.Beams==1&&trial.Left==0&&trial.Right==0,"two-hand forward push provides one easy beam at full energy");
        trial=new Trial();trial.Hold(Guard(),beam:true);trial.Hold(Beam(),.22f,true);var gap=Beam();gap[15].visibility=.1f;
        trial.Hold(gap,.066f,true);check(trial.Beams==0,"occlusion cannot fire a partly charged beam");
        trial.Hold(Beam(),.2f,true);
        check(trial.Beams==1,"brief unreliable frames preserve but do not advance beam progress");
        trial.Hold(Guard(),.099f,true);trial.Hold(Beam(),1,true);
        check(trial.Beams==1,"short pose wobble cannot rearm a held beam");
        trial.Hold(Guard(),.4f,true);trial.Hold(Beam(),.8f,true);
        check(trial.Beams==2,"clearly releasing the pose rearms the next beam");
        trial=new Trial();trial.Hold(Guard());trial.Hold(Beam(),1,false);
        check(trial.Beams==0&&trial.Recognizer.BeamProgress==0,"energy gate prevents unavailable beam charging");
        trial=new Trial();trial.Hold(Guard(),2,true);
        check(trial.Last.Shield&&trial.Beams==0&&trial.Left==0&&trial.Right==0,"held chest guard remains defense at full energy");
        var close=Guard();close[15].z=close[16].z=-.5f;trial.Move(Guard(),close,beam:true);trial.Hold(close,1,true);
        check(trial.Beams==0,"close crossed hands do not become the two-hand beam");
        trial=new Trial();var raised=Guard();raised[15].y=raised[16].y=.05f;
        trial.Hold(raised,1,transform:false);check(trial.Transforms==0,"battle phase does not generate transform gestures");
        foreach(int fps in new[]{15,30,60})
        {
            trial=new Trial(fps);var childL=Beam();childL[15].y=.41f;childL[16].y=.49f;
            trial.Hold(Guard());trial.Hold(childL,1,true);
            check(trial.Beams==1,$"lower, short-arm L pose fires once at {fps} fps");
            trial=new Trial(fps);var shallowPush=Push();shallowPush[15].z=shallowPush[16].z=-.29f;
            trial.Hold(Guard());trial.Move(Guard(),shallowPush,beam:true);trial.Hold(shallowPush,1,true);
            check(trial.Beams==1&&trial.Left==0&&trial.Right==0,$"modest two-hand reach fires without full extension at {fps} fps");
        }
        trial=new Trial();var soft=Beam();soft[15].visibility=soft[16].visibility=.48f;
        trial.Hold(Guard());trial.Hold(soft,1,true);
        check(trial.Beams==1&&trial.Left==0,"partly obscured but observable wrists can finish a held beam");
        trial=new Trial();trial.Hold(Guard());trial.Hold(Beam(),.20f,true);
        gap=Beam();gap[15].visibility=.1f;trial.Hold(gap,.20f,true);
        check(trial.Beams==0,"longer allowed pose gap cannot itself complete the beam");
        trial.Hold(Beam(),.25f,true);trial.Hold(gap,.20f,true);trial.Hold(Beam(),1,true);
        check(trial.Beams==1,"child pose wobble retains progress without repeating the beam");
        trial=new Trial();trial.Hold(Guard());trial.Hold(Beam(),.20f,true);trial.Hold(gap,.40f,true);trial.Hold(Beam(),.10f,true);
        check(trial.Beams==0,"long missing-wrist gap still discards incomplete beam progress");
    }
}
