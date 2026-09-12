using System;

namespace UltramanGame.Core
{
    // Monotonic deadlines follow actual spoken guidance. Only a fresh, visible
    // person can start AND finish the five-second countdown.
    public sealed class GuidedPhoto
    {
        readonly PhotoCountdown countdown=new PhotoCountdown();
        public PhotoStage Stage {get;private set;}
        public double ReadyAt {get;private set;}
        public bool Interrupted {get;private set;}
        double visibleSince=-1;
        public void Open(double now,double voiceSeconds)
        {Stage=PhotoStage.Framing;ReadyAt=now+Math.Max(0,voiceSeconds)+3;visibleSince=-1;Interrupted=false;countdown.Cancel();}
        public int Remaining(double now)=>countdown.Remaining(now);
        public bool Tick(double now,bool freshPerson)
        {
            Interrupted=false;
            if(Stage==PhotoStage.Framing)
            {
                if(!freshPerson)visibleSince=-1;
                else if(visibleSince<0)visibleSince=now;
                if(freshPerson&&visibleSince>=0&&now-visibleSince>=1&&now>=ReadyAt)
                {countdown.Begin(now);Stage=PhotoStage.Countdown;}
            }
            else if(Stage==PhotoStage.Countdown)
            {
                if(!freshPerson)
                {countdown.Cancel();Stage=PhotoStage.Framing;visibleSince=-1;ReadyAt=now+3;Interrupted=true;}
                else if(countdown.TakeShot(now)) {Stage=PhotoStage.Review;return true;}
            }
            return false;
        }
        public void DelayUntil(double time) {ReadyAt=Math.Max(ReadyAt,time);}
        public void Close() {Stage=PhotoStage.Closed;countdown.Cancel();visibleSince=-1;}
    }

    public enum PhotoChoice {None,Retake,PlayAgain}
    public sealed class PhotoChoiceGesture
    {
        string stream;
        long sequence,stamp;
        double neutral,held;
        bool armed;
        PhotoChoice candidate;
        public float Progress=>(float)Math.Min(1,held/(candidate==PhotoChoice.PlayAgain?1.2:2));
        public PhotoChoice Candidate=>candidate;
        public void Reset() {stream=null;sequence=stamp=0;neutral=held=0;armed=false;candidate=PhotoChoice.None;}
        public PhotoChoice Update(PoseFrame frame,long now)
        {
            if(!PoseQuality.Present(frame,now)) {Reset();return PhotoChoice.None;}
            if(stream==frame.streamId&&frame.sequence<=sequence)return PhotoChoice.None;
            if(stream!=frame.streamId||frame.capturedMs-stamp>250||frame.capturedMs<=stamp)
            {armed=false;neutral=held=0;candidate=PhotoChoice.None;}
            double dt=stamp>0?Math.Max(0,Math.Min(.1,(frame.capturedMs-stamp)/1000.0)):0;
            stream=frame.streamId;sequence=frame.sequence;stamp=frame.capturedMs;
            var p=frame.points;
            if(!PoseQuality.Reliable(p[15],.45f)||!PoseQuality.Reliable(p[16],.45f))
            {held=neutral=0;candidate=PhotoChoice.None;return PhotoChoice.None;}
            float width=Math.Max(.08f,PoseQuality.Distance(p[11],p[12]));
            float shoulders=(p[11].y+p[12].y)/2;
            bool left=p[15].y<shoulders-.30f*width,right=p[16].y<shoulders-.30f*width;
            bool down=p[15].y>shoulders-.05f*width&&p[16].y>shoulders-.05f*width;
            if(!armed) {neutral=down?neutral+dt:0;if(neutral>=.4)armed=true;return PhotoChoice.None;}
            PhotoChoice next=left&&right?PhotoChoice.PlayAgain:left!=right?PhotoChoice.Retake:PhotoChoice.None;
            if(next!=candidate)held=0;
            candidate=next;held=next==PhotoChoice.None?0:held+dt;
            if(held<(next==PhotoChoice.PlayAgain?1.2:2)||next==PhotoChoice.None)return PhotoChoice.None;
            Reset();return next;
        }
    }
}
