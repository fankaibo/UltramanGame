using System;

namespace UltramanGame.Core
{
    [Serializable] public struct PosePoint
    {
        public float x, y, z, visibility;
        public PosePoint(float x, float y, float visibility = 1) { this.x=x; this.y=y; z=0; this.visibility=visibility; }
    }

    [Serializable] public sealed class PoseFrame
    {
        public int schema;
        public string source;
        public string streamId;
        public long sequence, capturedMs;
        public bool tracked;
        public PosePoint[] points;
    }

    public static class PoseQuality
    {
        public const int FreshnessMs = 350;
        static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        // A fresh 'no person' frame still proves the service is working.
        public static bool Fresh(PoseFrame frame,long nowMs)
            => frame!=null && frame.schema==1 && !string.IsNullOrEmpty(frame.streamId) &&
                frame.streamId.Length<=64 && frame.sequence>=1 && frame.capturedMs>0 &&
                nowMs-frame.capturedMs<=FreshnessMs && frame.capturedMs-nowMs<=50;
        static bool FrameValid(PoseFrame frame, long nowMs)
        {
            if (!Fresh(frame,nowMs) || !frame.tracked || frame.points == null || frame.points.Length != 33) return false;
            foreach (var p in frame.points)
                if (!Finite(p.x) || !Finite(p.y) || !Finite(p.z) || !Finite(p.visibility) || p.visibility<0 || p.visibility>1) return false;
            return true;
        }
        public static bool Reliable(PosePoint p,float confidence=.55f)
            => p.visibility>=confidence && p.x>=0 && p.x<=1 && p.y>=0 && p.y<=1;
        // Presence depends on the torso. An obscured elbow is not a missing player.
        public static bool Present(PoseFrame frame,long nowMs)
            => FrameValid(frame,nowMs) && Reliable(frame.points[11],.45f) && Reliable(frame.points[12],.45f);
        public static bool Valid(PoseFrame frame,long nowMs)
        {
            if(!Present(frame,nowMs)) return false;
            for(int i=11;i<=16;i++) if(!Reliable(frame.points[i])) return false;
            return true;
        }
        public static float Distance(PosePoint a, PosePoint b)
        { float dx=a.x-b.x,dy=a.y-b.y; return (float)Math.Sqrt(dx*dx+dy*dy); }
    }

    public sealed class PlayerPresence
    {
        public const int GraceMs=600;
        long lastSeen;
        string stream;
        public void Reset() { lastSeen=0;stream=null; }
        public bool Update(PoseFrame frame,long nowMs)
        {
            if(frame!=null && frame.streamId!=stream) { Reset();stream=frame.streamId; }
            // Capture time, not render time: replaying one old frame cannot prolong presence.
            if(PoseQuality.Present(frame,nowMs)) lastSeen=Math.Max(lastSeen,frame.capturedMs);
            return lastSeen>0 && nowMs>=lastSeen-50 && nowMs-lastSeen<=GraceMs;
        }
    }

    public struct PlayerInput
    {
        public bool Tracking, Transform, LeftPunch, RightPunch, Shield, Beam;
        // Continuous ownership while confirming a pose, not an awarded action.
        // The battle queue must respect this before Shield/Beam can fire.
        public bool GuardIntent, BeamIntent;
    }

    // New frames only. Each gesture requires its own visible joints; missing joints never attack.
    public sealed class GestureRecognizer
    {
        string stream;
        long lastSequence, lastStamp;
        readonly PosePoint[] smoothed = new PosePoint[33];
        readonly bool[] wasReliable = new bool[33];
        readonly PosePoint[] beamPoints = new PosePoint[33];
        readonly bool[] beamReliable = new bool[33];
        readonly PunchMotion leftMotion=new PunchMotion(),rightMotion=new PunchMotion();
        bool beamFired, beamArmed, transformFired, guardAnchored, hadWristPair;
        PosePoint guardLeft,guardRight;
        float beamHold,beamGap,beamRelease,transformHold,shieldHold,shieldGap,steady,wristLossAge;
        public bool ForwardPunch { get; private set; }
        public int Difficulty {get;set;}
        int Level=>Math.Max(0,Math.Min(2,Difficulty));
        float TransformHold=>.45f+Level*.12f;
        float ShieldHold=>.10f+Level*.14f;
        float BeamHold=>BeamHoldSeconds+Level*.20f;
        public float TransformProgress => Math.Min(1,transformHold/TransformHold);
        public const float BeamHoldSeconds=.65f, BeamGapSeconds=.32f;
        public float BeamProgress => Math.Min(1,beamHold/BeamHold);
        public float ShieldProgress => Math.Min(1,shieldHold/ShieldHold);
        public bool BeamNeedsRelease {get;private set;}

        public void Reset()
        {
            stream=null; lastSequence=lastStamp=0;
            ClearGestures();
        }
        void ClearGestures()
        {
            leftMotion.Reset();rightMotion.Reset();beamFired=beamArmed=transformFired=ForwardPunch=false;
            guardAnchored=hadWristPair=false;wristLossAge=0;
            BeamNeedsRelease=false;
            beamHold=beamGap=beamRelease=transformHold=shieldHold=shieldGap=steady=0;
        }
        public PlayerInput Update(PoseFrame frame,long nowMs,bool beamAvailable=true,bool transformAvailable=true)
        {
            ForwardPunch=false;
            if (!PoseQuality.Present(frame,nowMs)) { Reset(); return default; }
            if (stream==frame.streamId && frame.sequence<=lastSequence) return default;
            // A delayed but still-fresh camera packet is not a new gesture
            // stream. Under TV/Unity load several packets can arrive more than
            // 250 ms apart; clearing beamHold there made a held finisher turn
            // into a shield. PoseQuality.Present above already rejects truly
            // stale frames, while a stream change or non-monotonic timestamp is
            // the reliable boundary for resetting gesture state.
            bool resetStream=stream!=frame.streamId || lastStamp==0 || frame.capturedMs<=lastStamp;
            long gapMs=resetStream?0:frame.capturedMs-lastStamp;
            float dt=resetStream ? 0 : Math.Min(.1f,gapMs/1000f);
            if (resetStream) ClearGestures();
            stream=frame.streamId; lastSequence=frame.sequence; lastStamp=frame.capturedMs;
            bool delayed=gapMs>250;
            float alpha=resetStream||delayed?1:(float)(1-Math.Exp(-dt/.035));
            for (int i=0;i<33;i++)
            {
                bool reliable=PoseQuality.Reliable(frame.points[i]);
                if(reliable)
                {
                    float blend=resetStream||!wasReliable[i]?1:alpha;
                    smoothed[i].x += (frame.points[i].x-smoothed[i].x)*blend;
                    smoothed[i].y += (frame.points[i].y-smoothed[i].y)*blend;
                    smoothed[i].z += (frame.points[i].z-smoothed[i].z)*blend;
                }
                wasReliable[i]=reliable;
                // Beam-only tolerance: a partly occluded wrist need not alter working punch/guard input.
                bool beamVisible=PoseQuality.Reliable(frame.points[i],.45f);
                if(beamVisible)
                {
                    float blend=resetStream||!beamReliable[i]?1:alpha;
                    beamPoints[i].x+=(frame.points[i].x-beamPoints[i].x)*blend;
                    beamPoints[i].y+=(frame.points[i].y-beamPoints[i].y)*blend;
                    beamPoints[i].z+=(frame.points[i].z-beamPoints[i].z)*blend;
                }
                beamReliable[i]=beamVisible;
            }
            var l=smoothed[11]; var r=smoothed[12];
            var lw=smoothed[15]; var rw=smoothed[16];
            float dx=l.x-r.x,dy=l.y-r.y,dz=l.z-r.z;
            float scale=Math.Max(.08f,(float)Math.Sqrt(dx*dx+dy*dy+dz*dz)),cx=(l.x+r.x)/2,sy=(l.y+r.y)/2;
            steady+=dt;
            var input=new PlayerInput { Tracking=true };
            bool shouldersReady=wasReliable[11]&&wasReliable[12];
            bool leftReady=shouldersReady&&wasReliable[15];
            bool rightReady=shouldersReady&&wasReliable[16];
            bool wristsReady=shouldersReady&&wasReliable[15]&&wasReliable[16];
            bool raised=wristsReady && lw.y<sy-.30f*scale && rw.y<sy-.30f*scale;
            bool beamWristsReady=beamReliable[11]&&beamReliable[12]&&beamReliable[15]&&beamReliable[16];
            if(beamWristsReady){hadWristPair=true;wristLossAge=0;}else wristLossAge+=dt;
            var bl=beamPoints[11];var br=beamPoints[12];var blw=beamPoints[15];var brw=beamPoints[16];
            float bdx=bl.x-br.x,bdy=bl.y-br.y,bdz=bl.z-br.z;
            // Guard and L-shape geometry use image-plane shoulders; noisy inferred depth must not shrink their target.
            float bs=Math.Max(.08f,(float)Math.Sqrt(bdx*bdx+bdy*bdy)),bcx=(bl.x+br.x)/2,bsy=(bl.y+br.y)/2;
            var fl=frame.points[11];var fr=frame.points[12];var flw=frame.points[15];var frw=frame.points[16];
            float fs=Math.Max(.08f,PoseQuality.Distance(fl,fr)),fcx=(fl.x+fr.x)/2,fsy=(fl.y+fr.y)/2;
            // A recognised L owns estimated depth from its first confirmed
            // frame. Waiting 100 ms to protect depth stranded early charges;
            // keep the stricter image-plane shape until the normal hold begins.
            bool beamShape=beamWristsReady && (BeamArms(bl,blw,brw,bcx,bsy,bs,preserveDepth:beamHold>0) || BeamArms(br,brw,blw,bcx,bsy,bs,preserveDepth:beamHold>0) ||
                ForwardPalms(bl,br,blw,brw,bsy,bs) ||
                // The first complete camera pose reserves the action before
                // smoothing catches up from hands-down or a previous punch.
                // Raw entry uses strict shape/depth; the usual timer still
                // requires a sustained pose before it can fire.
                BeamArms(fl,flw,frw,fcx,fsy,fs)||BeamArms(fr,frw,flw,fcx,fsy,fs)||ForwardPalms(fl,fr,flw,frw,fsy,fs)||
                ForearmBeam(flw,frame.points[13],frw,frame.points[14],fcx,fsy,fs)||
                ForearmBeam(frw,frame.points[14],flw,frame.points[13],fcx,fsy,fs));
            // Once intent is established, keep a wider pose envelope. Inferred
            // wrist depth often jumps while the child is holding an L; it must
            // not convert the same held action into a new forward punch.
            bool beamMaintained=beamHold>=.10f&&beamWristsReady &&
                (BeamArms(bl,blw,brw,bcx,bsy,bs,true)||BeamArms(br,brw,blw,bcx,bsy,bs,true)||ForwardPalms(bl,br,blw,brw,bsy,bs,true));
            bool beam=beamAvailable&&(beamShape||beamMaintained);
            float leftDepth=(bl.z-blw.z)/bs,rightDepth=(br.z-brw.z)/bs;
            var leftOffset=GuardOffset(fl,flw,fs);var rightOffset=GuardOffset(fr,frw,fs);
            bool rawGuard=beamWristsReady&&!(transformAvailable&&raised)&&GuardArms(flw,frw,fcx,fsy,fs);
            bool guardShape=rawGuard||beamWristsReady&&!(transformAvailable&&raised)&&GuardArms(blw,brw,bcx,bsy,bs);
            // Acquiring a shield still uses the original shape and hold. Once
            // established, a small visible drift at its boundary must not give
            // a noisy wrist-depth trajectory back to the punch recognizer.
            // Deliberate reach, a beam pose, lowered hands and tracking loss
            // retain their existing exit rules below.
            if(shieldHold>=ShieldHold&&beamWristsReady&&!(transformAvailable&&raised))
                guardShape|=GuardArms(flw,frw,fcx,fsy,fs,true)||GuardArms(blw,brw,bcx,bsy,bs,true);
            // Acquire the reference before the shield's hold timer. Otherwise a
            // static depth bias could forbid defense forever at first entry.
            // An accepted extended punch must retract before it can reacquire
            // this reference, or the same held fist would turn into a shield.
            float side=l.x>=r.x?1:-1;
            bool leftReturned=!leftMotion.HoldingStrike||leftMotion.RetractedForGuard(fl,flw,scale,side);
            bool rightReturned=!rightMotion.HoldingStrike||rightMotion.RetractedForGuard(fr,frw,scale,-side);
            if(rawGuard&&!guardAnchored&&leftReturned&&rightReturned)
            {guardLeft=leftOffset;guardRight=rightOffset;guardAnchored=true;}
            bool leftCommitted=leftDepth>.90f&&leftDepth-rightDepth>.70f &&
                (!guardAnchored||PoseQuality.Distance(leftOffset,guardLeft)>.16f);
            bool rightCommitted=rightDepth>.90f&&rightDepth-leftDepth>.70f &&
                (!guardAnchored||PoseQuality.Distance(rightOffset,guardRight)>.16f);
            if (steady<.25f) return input;
            transformHold=transformAvailable&&raised?transformHold+dt:0;
            if (wristsReady && !raised) transformFired=false;
            if (transformHold>=TransformHold && !transformFired) { input.Transform=true; transformFired=true; }
            // A release/guard must be observed before a beam. Holding a pose while energy fills cannot auto-fire it.
            if(beamWristsReady&&!beamShape&&!beamMaintained)beamRelease+=dt;else beamRelease=0;
            // Rearming a finished beam and tolerating an unfinished hold have
            // different time limits. A 250 ms release must not clear a charge
            // still inside its 320 ms uncertainty grace period.
            if(beamRelease>=.25f && (beamHold<=0||beamFired||beamGap>BeamGapSeconds))
            {beamFired=false;beamArmed=true;beamHold=0;}
            if(!beamAvailable) {beamArmed=beamRelease>=.25f;beamHold=0;}
            if(beam&&beamArmed&&!beamFired) {beamHold+=dt;beamGap=0;} else
            {beamGap+=dt;if(beamGap>BeamGapSeconds || !beamAvailable)beamHold=0;}
            if (beam && beamArmed && beamHold>=BeamHold && !beamFired)
            { input.Beam=true;beamFired=true;beamArmed=false; }
            BeamNeedsRelease=beam&&!beamArmed&&!beamFired;
            // Reserve the action during a brief uncertain interval. Missing
            // wrists pause progress; they neither advance it nor enable attacks.
            bool beamReserved=beamAvailable&&(beam||beamHold>0);
            bool shield=guardShape&&!beamReserved&&!leftCommitted&&!rightCommitted;
            if(shield) {shieldHold+=dt;shieldGap=0;}
            else
            {
                shieldGap+=dt;
                // Only bridge short wrist occlusion after a real guard, never an observed different action.
                if(beamWristsReady||shieldGap>.20f)shieldHold=0;
            }
            input.Shield=shieldHold>=ShieldHold;
            // A partly confirmed guard already owns the gesture. Its existing
            // 200 ms wrist-occlusion grace pauses confirmation; it must also
            // prevent the visible hand's depth noise from becoming a punch.
            input.GuardIntent=shield||shieldHold>0;
            input.BeamIntent=beamReserved;
            if(input.Shield)
            {
                // Follow an arm that is still retracting into its guard, but
                // do not move the anchor forward with an outgoing punch.
                if(!guardAnchored||leftOffset.z<guardLeft.z)guardLeft=leftOffset;
                if(!guardAnchored||rightOffset.z<guardRight.z)guardRight=rightOffset;
                guardAnchored=true;
            }
            if(!guardShape&&shieldGap>.20f)guardAnchored=false;
            // A held shield already bridges a brief hidden wrist. Preserve
            // that ownership too: the other wrist's noisy depth must not
            // become a punch merely because two-hand geometry is unavailable.
            // Discard those trajectories so they cannot fire when grace ends.
            // The receiver may skip the first complete pose and deliver its
            // overlapping wrists first. Briefly discard motion across a new
            // wrist-visibility loss, even before guard intent can be known.
            // This never awards defense; a persistently visible single arm
            // can still punch after the transition with a fresh trajectory.
            if(!beamWristsReady&&(input.GuardIntent||hadWristPair&&wristLossAge<=.20f))
            {
                leftMotion.Reset();rightMotion.Reset();
                return input;
            }
            if (beamReserved || (raised&&transformAvailable))
            {
                guardAnchored=false;
                leftMotion.Reset();rightMotion.Reset();
                return input;
            }
            bool left=leftReady&&leftMotion.Update(l,lw,wasReliable[13],scale,side,frame.capturedMs,dt,Level);
            bool right=rightReady&&rightMotion.Update(r,rw,wasReliable[14],scale,-side,frame.capturedMs,dt,Level);
            if(!leftReady)leftMotion.Reset();if(!rightReady)rightMotion.Reset();
            // Chest/face defense owns small asymmetric depth movement. Leaving
            // it requires an unmistakable single-arm reach plus motion history.
            input.LeftPunch=left&&(!guardShape || leftMotion.ForwardStrike&&leftCommitted);
            input.RightPunch=right&&(!guardShape || rightMotion.ForwardStrike&&rightCommitted);
            if(left&&!input.LeftPunch)leftMotion.RejectCandidate();
            if(right&&!input.RightPunch)rightMotion.RejectCandidate();
            if(input.LeftPunch&&input.RightPunch)
            {
                // One frame can contain two noisy wrist trajectories. Keep the
                // stronger arm so a child never gets a double or ambiguous punch.
                if(leftMotion.LastScore>=rightMotion.LastScore) input.RightPunch=false;
                else input.LeftPunch=false;
            }
            ForwardPunch=input.LeftPunch&&leftMotion.ForwardStrike || input.RightPunch&&rightMotion.ForwardStrike;
            if(input.LeftPunch||input.RightPunch) {input.Shield=false;shieldHold=0;guardAnchored=false;}
            return input;
        }
        static PosePoint GuardOffset(PosePoint shoulder,PosePoint wrist,float scale)
            =>new PosePoint((wrist.x-shoulder.x)/scale,(wrist.y-shoulder.y)/scale){z=(shoulder.z-wrist.z)/scale};
        static bool GuardArms(PosePoint left,PosePoint right,float cx,float sy,float scale,bool holding=false)
            =>Math.Abs(left.x-cx)<(holding?1.05f:.95f)*scale&&Math.Abs(right.x-cx)<(holding?1.05f:.95f)*scale&&
                Math.Abs(left.x-right.x)<(holding?1.75f:1.55f)*scale&&Math.Abs(left.y-right.y)<(holding?.85f:.70f)*scale&&
                left.y>sy-(holding?.90f:.80f)*scale&&right.y>sy-(holding?.90f:.80f)*scale&&
                left.y<sy+(holding?1.10f:1f)*scale&&right.y<sy+(holding?1.10f:1f)*scale;
        static bool BeamArms(PosePoint shoulder,PosePoint high,PosePoint low,float cx,float sy,float scale,bool holding=false,bool preserveDepth=false)
        {
            // The hands describe the intent. Exact right angles and two unoccluded elbows are unnecessary.
            // A deeply extended single fist is a punch, even when the other wrist is lower like an L.
            return shoulder.z-high.z<(holding||preserveDepth?1.6f:.9f)*scale && BeamHandShape(high,low,cx,sy,scale,holding);
        }
        static bool BeamHandShape(PosePoint high,PosePoint low,float cx,float sy,float scale,bool holding=false)
            =>low.y-high.y>(holding?.28f:.45f)*scale && high.y<sy+(holding?.25f:.10f)*scale && high.y>sy-1.3f*scale &&
                low.y>sy+(holding?.08f:.20f)*scale && low.y<sy+1.1f*scale && Math.Abs(high.x-cx)<(holding?1.1f:.90f)*scale &&
                Math.Abs(low.x-cx)<(holding?1.05f:.85f)*scale && Math.Abs(high.x-low.x)<(holding?1.15f:.90f)*scale;
        static bool ForearmBeam(PosePoint high,PosePoint highElbow,PosePoint low,PosePoint lowElbow,float cx,float sy,float scale)
        {
            // Monocular depth may be biased from the very first held frame.
            // Two visible forearms provide independent image-plane evidence:
            // one rises above its elbow/shoulder, the other crosses the chest.
            // This is an additional entry route, not an elbow requirement for
            // the existing wrist-only gesture or its occlusion tolerance.
            if(!PoseQuality.Reliable(highElbow,.55f)||!PoseQuality.Reliable(lowElbow,.55f)||
                !BeamHandShape(high,low,cx,sy,scale)||high.y>=sy-.10f*scale)return false;
            float rise=highElbow.y-high.y,across=Math.Abs(low.x-lowElbow.x);
            bool crossesCenter=Math.Abs(low.x-cx)<Math.Abs(lowElbow.x-cx);
            return rise>.30f*scale&&Math.Abs(high.x-highElbow.x)<rise*.70f+.10f*scale&&
                across>.25f*scale&&Math.Abs(low.y-lowElbow.y)<across*.45f+.10f*scale&&crossesCenter;
        }
        static bool ForwardPalms(PosePoint l,PosePoint r,PosePoint lw,PosePoint rw,float sy,float scale,bool holding=false)
        {
            float separation=Math.Abs(lw.x-rw.x)/scale;
            return (l.z-lw.z)>(holding?.30f:.55f)*scale && (r.z-rw.z)>(holding?.30f:.55f)*scale && separation>(holding?.50f:.60f) && separation<2.1f &&
                Math.Abs(lw.y-rw.y)<.7f*scale && lw.y>sy-.45f*scale && rw.y>sy-.45f*scale &&
                lw.y<sy+1f*scale && rw.y<sy+1f*scale;
        }
    }
}
