using System;

namespace UltramanGame.Core
{
    // Visual direction of an already accepted cast. Never recognizes an action,
    // changes its speed, or redirects its damage target. One snapshot per cast
    // prevents a recovering wrist or a noisy frame from steering a flying shot.
    public sealed class RangedCastPose
    {
        Battle observed;
        int sequence=-1;
        public float Elevation {get;private set;}
        public void Tick(PoseFrame frame,long nowMs,PlayerInput input,Battle state,float dt,bool enabled)
        {
            if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<=0)return;
            if(!ReferenceEquals(observed,state)) {observed=state;sequence=-1;Elevation=0;}
            if(state==null||state.Phase!=GamePhase.Battle||!state.IsRangedPunch||state.Shield)
            {sequence=-1;Elevation=0;return;}
            if(sequence==state.AttackSequence)return;
            sequence=state.AttackSequence;Elevation=0;
            if(!enabled||!input.Tracking||!PoseQuality.Present(frame,nowMs))return;
            bool left=state.Action==HeroAction.LeftPunch;
            var shoulder=frame.points[left?11:12];var wrist=frame.points[left?15:16];
            float width=PoseQuality.Distance(frame.points[11],frame.points[12]);
            if(width<.08f||!PoseQuality.Reliable(wrist))return;
            // Use image-plane geometry, never uncertain monocular depth.
            // Ordinary chest-height punches remain almost horizontal; higher
            // preparations extend upwards with a soft elbow, not into the face.
            Elevation=Math.Max(-.10f,Math.Min(.45f,(shoulder.y-wrist.y)/width*.60f+.15f));
        }
    }
}
