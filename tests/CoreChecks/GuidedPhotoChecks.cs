using System;
using UltramanGame.Core;

static class GuidedPhotoChecks
{
    public static void Run(Action<bool,string> check)
    {
        var f=new GuidedPhoto();f.Open(100,8);
        f.Tick(100,true);f.Tick(110.99,true);
        check(f.Stage==PhotoStage.Framing,"automatic photo waits for speech and three reaction seconds");
        check(!f.Tick(111,true)&&f.Stage==PhotoStage.Countdown&&f.Remaining(111)==5,"automatic photo begins countdown without a button");
        check(!f.Tick(115.99,true)&&f.Tick(116,true)&&f.Stage==PhotoStage.Review,"automatic photo takes one fresh shot after five seconds");
        check(!f.Tick(150,true)&&f.Stage==PhotoStage.Review,"automatic review cannot loop captures");
        f.Open(0,0);f.Tick(0,true);f.Tick(3,true);f.Tick(5,false);
        check(f.Stage==PhotoStage.Framing&&f.Interrupted,"camera loss anywhere in countdown cancels the shot");
        check(!f.Tick(8,true)&&!f.Tick(8.99,true),"recovered person first stays visible for a second");
        f.Tick(9,true);check(f.Remaining(9)==5&&!f.Tick(13.99,true)&&f.Tick(14,true),"recovered photo gets a full new countdown");
        f.Open(0,0);f.Tick(0,true);f.DelayUntil(20);f.Tick(19,true);
        check(f.Stage==PhotoStage.Framing,"spoken retry extends preparation deadline");
        f.Close();check(!f.Tick(99,true)&&f.Stage==PhotoStage.Closed,"closed automatic session cannot capture");
        var g=new PhotoChoiceGesture();long stamp=10000;long sequence=0;PoseFrame lastFrame=null;
        PhotoChoice Feed(bool left,bool right,int count,bool fresh=true,bool selecting=true,bool wrists=true)
        {
            var result=PhotoChoice.None;
            for(int j=0;j<count;j++)
            {
                stamp+=50;var p=new PosePoint[33];for(int i=0;i<33;i++)p[i]=new PosePoint(.5f,.5f);
                p[11]=new PosePoint(.65f,.3f);p[12]=new PosePoint(.35f,.3f);
                p[15]=new PosePoint(.67f,left?.1f:.6f);p[16]=new PosePoint(.33f,right?.1f:.6f);
                if(!wrists)p[15].visibility=p[16].visibility=0;
                var frame=new PoseFrame{schema=1,streamId="test",sequence=++sequence,capturedMs=stamp,tracked=true,points=p};
                lastFrame=frame;
                var value=g.Update(frame,stamp+(fresh?0:800),selecting);if(value!=PhotoChoice.None)result=value;
            }
            return result;
        }
        check(Feed(true,true,40)==PhotoChoice.None,"held victory pose cannot start another round");
        Feed(false,false,12);check(Feed(true,false,20)==PhotoChoice.None,"short single-arm motion does not retake");
        check(Feed(true,false,24)==PhotoChoice.Retake,"deliberate single raised arm selects retake");
        Feed(false,false,12);check(Feed(true,true,30)==PhotoChoice.PlayAgain,"two raised arms select another round");
        Feed(false,false,12);check(Feed(true,true,40,false)==PhotoChoice.None,"stale camera frames cannot select another round");
        g.Reset();Feed(false,false,20,selecting:false,wrists:false);
        check(g.Armed,"lowered wrists outside laptop crop arm the menu during narration");
        check(Feed(true,true,40,selecting:false)==PhotoChoice.None,"narration still prevents early selection");
        check(Feed(true,true,30)==PhotoChoice.PlayAgain,"raising hands after narrated release starts the next round without a second release");
        g.Reset();Feed(false,false,20,selecting:false);stamp+=300;
        Feed(true,true,20,selecting:false);
        check(g.Armed,"brief frame-delivery gap preserves the already observed hands-down release");
        check(Feed(true,true,30)==PhotoChoice.PlayAgain,"raised hands remain usable after a brief gap during narration");
        g.Reset();Feed(false,false,12);Feed(true,true,10);
        check(g.Update(lastFrame,stamp+450)==PhotoChoice.None&&g.Progress==0&&g.Armed,
            "brief stale interval clears hold time but retains prior release");
        stamp+=450;
        check(Feed(true,true,12)==PhotoChoice.None,"recovery cannot count missing time toward a selection");
        check(Feed(true,true,18)==PhotoChoice.PlayAgain,"fresh held pose can select after delivery recovers");
        g.Reset();Feed(false,false,12);g.Update(lastFrame,stamp+1500);stamp+=1500;
        check(!g.Armed&&Feed(true,true,40)==PhotoChoice.None,"long camera loss requires another release before selecting");
        g.Reset();Feed(false,false,12);lastFrame.streamId="replacement";lastFrame.sequence=1;
        g.Update(lastFrame,stamp);
        check(!g.Armed,"replacement camera stream cannot reuse an earlier release");
    }
}
