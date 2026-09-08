using System;

namespace UltramanGame.Core
{
    // A short, bounded history of shoulder-relative motion, not a template of one perfect pose.
    // Depth is the model's estimate, not a depth-camera measurement.
    public sealed class PunchMotion
    {
        struct Sample { public float X,Y,Depth;public long Stamp; }
        readonly Sample[] history=new Sample[24];
        int count,next,candidates;
        bool latched;
        float peakOut,peakDepth,releaseHold,candidateHold;
        long firedAt;
        public bool ForwardStrike { get; private set; }
        public void Reset()
        {count=next=candidates=0;latched=false;peakOut=peakDepth=releaseHold=candidateHold=0;firedAt=0;ForwardStrike=false;}
        public bool Update(PosePoint shoulder,PosePoint wrist,bool elbowVisible,float scale,float side,long stamp,float dt)
        {
            ForwardStrike=false;
            var current=new Sample {X=(wrist.x-shoulder.x)*side/scale,Y=(wrist.y-shoulder.y)/scale,
                Depth=(shoulder.z-wrist.z)/scale,Stamp=stamp};
            if(latched)
            {
                peakOut=Math.Max(peakOut,current.X);peakDepth=Math.Max(peakDepth,current.Depth);
                bool returned=peakOut-current.X>.28f || peakDepth-current.Depth>.25f;
                releaseHold=returned?releaseHold+dt:0;
                if(releaseHold>=.06f && stamp-firedAt>=250)
                {latched=false;count=next=0;candidates=0;candidateHold=0;}
                else return false;
            }
            bool forward=false,lateral=false;
            // Hands can be a little above/below the shoulder, but simply raising or dropping an arm is not a punch.
            if(current.Y>=-.45f && current.Y<=.78f)
            {
                for(int i=0;i<count;i++)
                {
                    var old=history[i];long age=stamp-old.Stamp;
                    if(age<60 || age>650 || old.Y<-.45f)continue;
                    float outTravel=current.X-old.X,depthTravel=current.Depth-old.Depth,vertical=Math.Abs(current.Y-old.Y);
                    forward|=depthTravel>=.32f && current.Depth>.35f && depthTravel>vertical*.8f &&
                        depthTravel>Math.Abs(outTravel)*.65f;
                    lateral|=elbowVisible && outTravel>=.42f && current.X>.62f && outTravel>vertical*.65f;
                }
            }
            history[next]=current;next=(next+1)%history.Length;count=Math.Min(count+1,history.Length);
            if(forward||lateral) {candidates++;candidateHold+=dt;}
            else {candidates=0;candidateHold=0;}
            // Two fresh observations reject a single bad depth estimate or one noisy wrist location.
            if(candidates<2 || candidateHold<.045f)return false;
            ForwardStrike=forward;latched=true;firedAt=stamp;peakOut=current.X;peakDepth=current.Depth;
            releaseHold=candidateHold=0;candidates=0;
            return true;
        }
    }
}
