using System;
using UltramanGame.Core;

static class HighHandGestureChecks
{
    static PosePoint P(float x,float y,float z=-.1f)=>new PosePoint(x,y){z=z};
    static PosePoint[] Guard(float height)
    {
        var p=new PosePoint[33];for(int i=0;i<p.Length;i++)p[i]=P(.5f,.5f);
        p[11]=P(.65f,.35f);p[12]=P(.35f,.35f);
        p[13]=P(.68f,.39f,-.12f);p[14]=P(.32f,.39f,-.12f);
        p[15]=P(.58f,height,-.20f);p[16]=P(.42f,height,-.20f);
        return p;
    }
    sealed class Trial
    {
        public readonly GestureRecognizer R=new GestureRecognizer{Difficulty=1};
        public PlayerInput Last;public int Left,Right,Beams;readonly int rate;long time=300000,sequence;
        public Trial(int rate){this.rate=rate;}
        public void Frame(PosePoint[] p,bool beam=false)
        {
            time+=(long)Math.Round(1000.0/rate);
            Last=R.Update(new PoseFrame{schema=1,tracked=true,streamId="high-hand",sequence=++sequence,capturedMs=time,points=p},time,beam,false);
            if(Last.LeftPunch)Left++;if(Last.RightPunch)Right++;if(Last.Beam)Beams++;
        }
        public void Hold(PosePoint[] p,float seconds,bool beam=false)
        {for(int i=0;i<(int)Math.Ceiling(seconds*rate);i++)Frame(p,beam);}
        public void Punch(float height,int hand,float duration,bool beam=false)
        {
            // Start beside the face, reach forward, hold, retract beside the
            // same face. No lowered-hand reset exists anywhere in this cycle.
            for(int i=0;i<(int)Math.Ceiling((duration+.85f)*rate);i++)
            {
                float t=i/(float)rate;
                float extension=Math.Min(1,t/duration);
                if(t>duration+.25f)extension=Math.Max(0,1-(t-duration-.25f)/.25f);
                var p=Guard(height);p[hand].x+=(hand==15?1:-1)*.08f*extension;p[hand].z-=.28f*extension;
                Frame(p,beam);
            }
        }
    }
    public static void Run(Action<bool,string> check)
    {
        foreach(int rate in new[]{15,30,60})foreach(float height in new[]{.20f,.14f})foreach(bool charged in new[]{false,true})
        {
            var t=new Trial(rate);t.Hold(Guard(height),.8f,charged);
            check(t.Last.Shield&&t.Left+t.Right==0,$"{rate} Hz face-height {height} guard starts without attack");
            for(int cycle=0;cycle<3;cycle++)
            {
                t.Punch(height,15,.18f,charged);t.Punch(height,16,.55f,charged);
                check(t.Left==cycle+1&&t.Right==cycle+1,$"{rate} Hz face-height {height} alternating round {cycle} requires no hands-down reset");
            }
            t.Hold(Guard(height),.8f);
            check(t.Last.Shield&&t.Left+t.Right==6,$"{rate} Hz face-height {height} returns directly to defense");
            t.Hold(Guard(height),.6f,true);
            var beam=Guard(height);beam[15]=P(.54f,.18f);beam[13]=P(.54f,.40f);
            beam[16]=P(.53f,.41f);beam[14]=P(.28f,.41f);
            t.Hold(beam,1.8f,true);
            check(t.Beams==1&&t.Left+t.Right==6,$"{rate} Hz face-height {height} guard to L completes without a punch interruption");
            t.Hold(beam,1.2f,true);
            check(t.Beams==1,$"{rate} Hz face-height {height} held L fires only once");
            var noise=new Trial(rate);noise.Hold(Guard(height),.8f,true);
            for(int n=0;n<8;n++)
            {
                var p=Guard(height);p[n%2==0?15:16].z=-.49f;
                noise.Hold(p,.13f,true);noise.Hold(Guard(height),.17f,true);
            }
            check(noise.Left+noise.Right+noise.Beams==0&&noise.Last.Shield,$"{rate} Hz high guard depth noise remains defense");
            var lift=new Trial(rate);
            for(int i=0;i<rate*5;i++)
            {
                float y=.44f-.36f*(float)(.5-.5*Math.Cos(i/(float)rate*Math.PI*2));
                lift.Frame(Guard(y),true);
            }
            check(lift.Left+lift.Right+lift.Beams==0,$"{rate} Hz raising and lowering both hands is never an attack");
            foreach(int hand in new[]{15,16})
            {
                var oneLift=new Trial(rate);oneLift.Hold(Guard(height),.8f);
                for(int i=0;i<rate*4;i++)
                {
                    var p=Guard(height);p[hand].y=.44f-.36f*(float)(.5-.5*Math.Cos(i/(float)rate*Math.PI*2));
                    oneLift.Frame(p);
                }
                check(oneLift.Left+oneLift.Right==0,$"{rate} Hz hand {hand} vertical-only motion is not a punch");
            }
        }
    }
}
