using System;

namespace UltramanGame.Core
{
    // Presentation-only anticipation. This never produces a gesture or writes
    // battle state; accepted attacks retain their authored timing and contact.
    public sealed class LiveReadyPose
    {
        Battle observed;
        string stream;
        long sequence=-1;
        float lx,ly,rx,ry,txl,tyl,txr,tyr;
        public float X(bool left)=>left?lx:rx;
        public float Y(bool left)=>left?ly:ry;
        public static bool Ready(Battle state)=>state!=null&&
            (state.Phase==GamePhase.Waiting||state.Phase==GamePhase.Battle&&state.Action==HeroAction.None&&!state.Shield);
        static float Clamp(float value,float min,float max)=>Math.Max(min,Math.Min(max,value));
        public void Tick(PoseFrame frame,long nowMs,PlayerInput input,Battle state,float dt,bool enabled)
        {
            if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<=0)return;
            if(!ReferenceEquals(observed,state))
            {observed=state;stream=null;sequence=-1;lx=ly=rx=ry=txl=tyl=txr=tyr=0;}
            // A confirmation hold is still preparation: keep showing the
            // hands until the actual shield/beam animation takes ownership.
            bool valid=enabled&&input.Tracking&&Ready(state)&&PoseQuality.Present(frame,nowMs);
            if(valid)
            {
                if(frame.streamId!=stream){stream=frame.streamId;sequence=-1;txl=tyl=txr=tyr=0;}
                if(frame.sequence>sequence)
                {
                    sequence=frame.sequence;
                    float width=PoseQuality.Distance(frame.points[11],frame.points[12]);
                    Target(frame.points[11],frame.points[15],width,out txl,out tyl);
                    Target(frame.points[12],frame.points[16],width,out txr,out tyr);
                }
            }
            else
            {
                txl=tyl=txr=tyr=0;
                // Re-enable from a fresh sample, including a frame retained
                // across input-mode changes; stale frames still fail freshness.
                sequence=-1;
            }
            float blend=1-(float)Math.Exp(-Math.Min(dt,.1f)/.10f);
            lx+=(txl-lx)*blend;ly+=(tyl-ly)*blend;
            rx+=(txr-rx)*blend;ry+=(tyr-ry)*blend;
        }
        static void Target(PosePoint shoulder,PosePoint wrist,float width,out float x,out float y)
        {
            x=y=0;
            if(width<.08f||!PoseQuality.Reliable(wrist))return;
            // Camera x is opposite the fighter's anatomical lateral axis.
            // Shoulder-width normalization also works with a seated/half-body
            // player; uncertain camera depth is intentionally not used.
            x=-.22f*Clamp((wrist.x-shoulder.x)/width,-1.3f,1.3f);
            y=.36f*Clamp((shoulder.y-wrist.y)/width+.90f,-.25f,1.25f);
        }
    }
}
