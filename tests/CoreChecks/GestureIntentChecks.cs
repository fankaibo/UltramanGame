using System;
using UltramanGame.Core;

static class GestureIntentChecks
{
    static PosePoint P(float x,float y,float z=-.1f)=>new PosePoint(x,y){z=z};
    static PosePoint[] Guard()
    {
        var p=new PosePoint[33];for(int i=0;i<33;i++)p[i]=P(.5f,.5f);
        p[11]=P(.65f,.35f);p[12]=P(.35f,.35f);p[13]=P(.68f,.50f,-.12f);p[14]=P(.32f,.50f,-.12f);
        p[15]=P(.58f,.44f,-.20f);p[16]=P(.42f,.44f,-.20f);return p;
    }
    static PosePoint[] Beam(){var p=Guard();p[15]=P(.60f,.30f,-.18f);p[16]=P(.44f,.46f,-.20f);return p;}
    sealed class Trial
    {
        public readonly GestureRecognizer R=new GestureRecognizer{Difficulty=1};
        public PlayerInput Last;public int Punches,Beams;readonly int fps;long time=900000,sequence;
        public Trial(int fps){this.fps=fps;}
        public void Hold(PosePoint[] points,float seconds,bool beam=false)
        {
            for(int i=0;i<Math.Ceiling(seconds*fps);i++)
            {
                time+=(long)Math.Round(1000.0/fps);
                Last=R.Update(new PoseFrame{schema=1,tracked=true,streamId="intent",sequence=++sequence,capturedMs=time,points=points},time,beam,false);
                if(Last.LeftPunch||Last.RightPunch)Punches++;if(Last.Beam)Beams++;
            }
        }
    }
    public static void Run(Action<bool,string> check)
    {
        foreach(int fps in new[]{15,30,60})
        {
            var t=new Trial(fps);t.Hold(Guard(),.7f);
            var noisyGuard=Guard();noisyGuard[15].z=-.34f;
            for(int n=0;n<4;n++){t.Hold(noisyGuard,.14f);t.Hold(Guard(),.16f);}
            check(t.Punches==0&&t.Last.Shield,$"held defense survives unequal estimated wrist depth without attacking at {fps} fps");
            foreach(int hand in new[]{15,16})
            {
                t=new Trial(fps);t.Hold(Guard(),.7f);
                var depthJump=Guard();depthJump[hand].z=-.49f;
                // Keep the same arm shape while moving the entire torso too.
                for(int j=0;j<depthJump.Length;j++){depthJump[j].x+=.04f;depthJump[j].y-=.02f;}
                t.Hold(depthJump,.20f);
                check(t.Last.Shield&&t.Punches==0,$"depth-only jump cannot release guard hand {hand} at {fps} fps");
                t.Hold(Guard(),.4f);
                var reach=Guard();reach[hand]=P(hand==15?.64f:.36f,.40f,-.49f);t.Hold(reach,.20f);
                check(t.Punches==1&&!t.Last.Shield,$"visible forward reach can leave guard hand {hand} at {fps} fps");
            }
            t=new Trial(fps);var face=Guard();face[15].y=face[16].y=.22f;t.Hold(face,.8f);
            check(t.Last.Shield&&t.Punches==0,$"hands protecting face remain defense during battle at {fps} fps");
            t=new Trial(fps);t.Hold(Guard(),.7f,true);t.Hold(Beam(),.3f,true);
            var wobble=Beam();wobble[15].z=-.46f;t.Hold(wobble,.16f,true);t.Hold(Beam(),.55f,true);
            check(t.Beams==1&&t.Punches==0,$"L-pose depth wobble cannot steal charging as a punch at {fps} fps");
            t.Hold(Beam(),1,true);check(t.Beams==1,$"held finisher fires only once after depth wobble at {fps} fps");
            t=new Trial(fps);t.Hold(Guard(),.7f,true);t.Hold(Beam(),.45f,true);
            float progress=t.R.BeamProgress;
            var uncertain=Beam();uncertain[15].z=-.9f;uncertain[16].z=-.1f;t.Hold(uncertain,.26f,true);
            check(t.R.BeamProgress>=progress&&t.Beams==0&&t.Punches==0,$"visible pose uncertainty within the grace window pauses rather than resets charge at {fps} fps");
            t.Hold(Beam(),.65f,true);
            check(t.Beams==1&&t.Punches==0,$"finisher completes after a visible short pose uncertainty at {fps} fps");
            t=new Trial(fps);t.Hold(Guard(),.7f,true);t.Hold(Beam(),.3f,true);
            var missing=Beam();missing[15].visibility=.1f;t.Hold(missing,.26f,true);
            check(t.Beams==0&&t.R.BeamProgress>0&&t.Punches==0,$"brief hidden wrist pauses charge without clearing or attacking at {fps} fps");
            t.Hold(Beam(),.7f,true);check(t.Beams==1&&t.Punches==0,$"charge resumes after a brief overlap at {fps} fps");
            t=new Trial(fps);t.Hold(Guard(),.7f,true);t.Hold(Beam(),.3f,true);
            var down=Guard();down[15].y=down[16].y=.78f;t.Hold(down,.55f,true);
            check(t.Beams==0&&t.R.BeamProgress==0,$"putting hands down cancels the reserved beam at {fps} fps");
            var punch=Guard();punch[15]=P(.62f,.40f,-.48f);t.Hold(Guard(),.4f);t.Hold(punch,.6f);
            check(t.Punches==1,$"a deliberate fresh punch still works after cancellation at {fps} fps");
        }
    }
}
