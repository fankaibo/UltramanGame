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
        // Camera pose estimates commonly spend one or two packets between the
        // chest guard and the L-shaped finisher. Keep a short intent latch so
        // that those packets cannot turn into a punch or erase a charge.
        bool guardLocked, beamLocked;
        float guardLockAge, guardLostAge, beamLockAge, beamLostAge;
        PosePoint guardLeft,guardRight;
        float beamHold,beamGap,beamRelease,beamShapeGrace,transformHold,shieldHold,shieldGap,steady,wristLossAge;
        public bool ForwardPunch { get; private set; }
        public int Difficulty {get;set;}
        int Level=>Math.Max(0,Math.Min(2,Difficulty));
        float TransformHold=>.45f+Level*.12f;
        // Chest defense should confirm quickly enough for a four-year-old, while
        // the challenge setting still asks for a deliberate hold.
        // Defense is a child safety action: confirm a readable chest guard
        // sooner than a finisher, while higher difficulty still asks for a
        // deliberate hold instead of a one-frame accidental pose.
        // Keep the child-friendly quick confirmation used by the standard
        // profile; the shape envelope and punch trajectory remain the actual
        // defense/attack distinction.
        float ShieldHold=>.06f+Level*.10f;
        // The finisher should reward a readable hold, not punish a child for
        // one noisy camera packet.  Standard difficulty now needs about .70s
        // of a clear pose, with a longer grace window for temporary shape
        // loss.  The recognizer still requires an explicit release before it
        // can fire a second beam.
        float BeamHold=>BeamHoldSeconds+Level*.10f;
        public float TransformProgress => Math.Min(1,transformHold/TransformHold);
        public const float BeamHoldSeconds=.60f, BeamGapSeconds=1.45f;
        // A laptop camera can lose stable depth for several packets while the
        // child is still visibly holding the finisher. Keep ownership longer
        // than the charge gap so this uncertainty cannot become a punch.
        const float BeamOwnershipGraceSeconds=1.80f;
        // Keep a confirmed chest guard through a longer front-camera shape
        // wobble.  A deliberate reach still exits through the committed
        // trajectory checks below.
        const float GuardOwnershipGraceSeconds=.90f;
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
            guardLocked=beamLocked=false;guardLockAge=guardLostAge=beamLockAge=beamLostAge=0;
            BeamNeedsRelease=false;
            beamHold=beamGap=beamRelease=beamShapeGrace=transformHold=shieldHold=shieldGap=steady=0;
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
            beamShapeGrace=Math.Max(0,beamShapeGrace-dt);
            stream=frame.streamId; lastSequence=frame.sequence; lastStamp=frame.capturedMs;
            bool delayed=gapMs>250;
            float alpha=resetStream||delayed?1:(float)(1-Math.Exp(-dt/.035));
            bool pairedBefore=beamReliable[11]&&beamReliable[12]&&beamReliable[15]&&beamReliable[16];
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
            bool firstWristLoss=!resetStream&&pairedBefore&&!beamWristsReady;
            if(beamWristsReady){hadWristPair=true;wristLossAge=0;}
            else wristLossAge=firstWristLoss?0:wristLossAge+dt;
            var bl=beamPoints[11];var br=beamPoints[12];var blw=beamPoints[15];var brw=beamPoints[16];
            float bdx=bl.x-br.x,bdy=bl.y-br.y,bdz=bl.z-br.z;
            // Guard and L-shape geometry use image-plane shoulders; noisy inferred depth must not shrink their target.
            float bs=Math.Max(.08f,(float)Math.Sqrt(bdx*bdx+bdy*bdy)),bcx=(bl.x+br.x)/2,bsy=(bl.y+br.y)/2;
            var fl=frame.points[11];var fr=frame.points[12];var flw=frame.points[15];var frw=frame.points[16];
            float fs=Math.Max(.08f,PoseQuality.Distance(fl,fr)),fcx=(fl.x+fr.x)/2,fsy=(fl.y+fr.y)/2;
            float leftDepth=(bl.z-blw.z)/bs,rightDepth=(br.z-brw.z)/bs;
            float rawLeftDepth=(fl.z-flw.z)/fs,rawRightDepth=(fr.z-frw.z)/fs;
            // A single wildly jumping wrist depth is the camera's common
            // false-positive during a held L. It may reserve the action, but
            // it must not advance the charge until the estimate settles.
            // Keep a clearly asymmetric depth jump from advancing the charge;
            // the ownership grace below still pauses and protects the gesture.
            bool beamDepthStable=Math.Abs(leftDepth-rightDepth)<2.20f;
            // A recognised L owns estimated depth from its first confirmed
            // frame. Waiting 100 ms to protect depth stranded early charges;
            // keep the stricter image-plane shape until the normal hold begins.
            bool beamShape=beamWristsReady && beamDepthStable && (BeamArms(bl,blw,brw,bcx,bsy,bs,preserveDepth:beamHold>0) || BeamArms(br,brw,blw,bcx,bsy,bs,preserveDepth:beamHold>0) ||
                ForwardPalms(bl,br,blw,brw,bsy,bs) ||
                ForwardPushEntry(bl,br,blw,brw,bsy,bs) ||
                // The first complete camera pose reserves the action before
                // smoothing catches up from hands-down or a previous punch.
                // Raw entry uses strict shape/depth; the usual timer still
                // requires a sustained pose before it can fire.
                BeamArms(fl,flw,frw,fcx,fsy,fs)||BeamArms(fr,frw,flw,fcx,fsy,fs)||ForwardPalms(fl,fr,flw,frw,fsy,fs)||
                ForwardPushEntry(fl,fr,flw,frw,fsy,fs)||
                ForearmBeam(flw,frame.points[13],frw,frame.points[14],fcx,fsy,fs)||
                ForearmBeam(frw,frame.points[14],flw,frame.points[13],fcx,fsy,fs));
            // Depth is the least stable signal on a laptop webcam. Keep a
            // separate two-dimensional entry shape so a real L reserves the
            // finisher before one noisy depth sample can become a punch. The
            // charge clock below still requires stable depth.
            bool beamEntryShape=beamWristsReady &&
                (BeamEntryShape(bl,blw,brw,beamPoints[13],bcx,bsy,bs)||BeamEntryShape(br,brw,blw,beamPoints[14],bcx,bsy,bs)||
                 ForwardPushEntry(bl,br,blw,brw,bsy,bs)||
                 BeamEntryShape(fl,flw,frw,frame.points[13],fcx,fsy,fs)||BeamEntryShape(fr,frw,flw,frame.points[14],fcx,fsy,fs));
            // The first few L-shape packets are where MediaPipe most often
            // swaps a wrist depth or drops an elbow. Remember a positively
            // observed beam shape before a charge exists, so that packet
            // cannot immediately become a punch while the child settles.
            if(beamAvailable&&(beamShape||beamEntryShape))beamShapeGrace=BeamOwnershipGraceSeconds;
            bool rawGuard=beamWristsReady&&!(transformAvailable&&raised)&&GuardArms(flw,frw,fcx,fsy,fs);
            bool guardShape=rawGuard||beamWristsReady&&!(transformAvailable&&raised)&&GuardArms(blw,brw,bcx,bsy,bs);
            // Front cameras often spread the wrists a little farther apart
            // than the strict chest envelope, especially for a small child.
            // Accept that readable chest/face guard as an entry pose, while
            // keeping the old envelope for acquisition and keeping any L
            // shape or forward-palms pose owned by the finisher path.
            bool guardEntry=beamWristsReady&&!(transformAvailable&&raised)&&!beamShape&&!ForwardPalms(fl,fr,flw,frw,fsy,fs)&&
                Math.Abs(fl.z-flw.z)<1.15f*fs&&Math.Abs(fr.z-frw.z)<1.15f*fs&&
                GuardEntryArms(flw,frw,fcx,fsy,fs);
            guardShape|=guardEntry;
            // A visible L is a stronger finisher signal than a loose chest
            // envelope. Do not acquire the guard latch from the same packet.
            bool guardCandidate=guardShape&&!beamShape;
            if(guardCandidate)
            {
                guardLockAge=Math.Min(.5f,guardLockAge+dt);guardLostAge=0;
                if(guardLockAge>=.10f)guardLocked=true;
            }
            else if(guardLocked)
            {
                guardLostAge+=dt;
                // A clear single-arm reach is handled below and exits the
                // latch immediately; ordinary camera wobble gets 280 ms.
                if(guardLostAge>GuardOwnershipGraceSeconds){guardLocked=false;guardLockAge=0;}
            }
            // Acquire defense with the stricter chest shape so a moving punch
            // cannot create a false shield. Once it has been held for the
            // confirmation interval, the wider envelope below absorbs jitter.
            bool guardEnvelope=guardShape;
            // Once intent is established, keep a wider pose envelope. Inferred
            // wrist depth often jumps while the child is holding an L; it must
            // not convert the same held action into a new forward punch.
            bool rawBeamReleasePose=beamWristsReady&&!raised&&
                flw.y>fsy+.16f*fs&&frw.y>fsy+.16f*fs;
            bool beamMaintained=beamHold>=.10f&&beamWristsReady&&!rawBeamReleasePose &&
                (BeamImageShape(blw,brw,bcx,bsy,bs,true)||BeamImageShape(brw,blw,bcx,bsy,bs,true)||
                 ForwardImageShape(blw,brw,bsy,bs,true));
            // Once the child has held a valid finisher for a few packets,
            // retain ownership through the common false-negative where one
            // arm is briefly read as a forward punch. A neutral, lowered
            // release or a deliberate chest guard still ends the gesture.
            bool beamReleasePose=rawBeamReleasePose||beamWristsReady&&!raised&&
                blw.y>bsy+.16f*bs&&brw.y>bsy+.16f*bs;
            if(beamShape||beamEntryShape)
            {
                beamLockAge=Math.Min(.6f,beamLockAge+dt);beamLostAge=0;
                if(beamLockAge>=.12f)beamLocked=true;
            }
            else if(beamLocked)
            {
                beamLostAge+=dt;
                // A deliberate low-hand release is the only immediate cancel;
                // otherwise tolerate a short estimator wobble during charge.
                if(beamReleasePose||beamLostAge>BeamOwnershipGraceSeconds){beamLocked=false;beamLockAge=0;}
            }
            bool beamLatch=beamLocked&&beamWristsReady&&!raised&&!beamReleasePose&&beamLostAge<=BeamOwnershipGraceSeconds;
            // After a finisher has started, its ownership survives a short
            // frame where the estimator resembles a chest guard.  Without
            // this exception a depth jump can hand the same pose to the
            // punch recognizer and the child's charge appears to reset.
            bool beamGrace=(beamHold>=.10f||beamShapeGrace>0)&&beamWristsReady&&!raised&&
                (!guardEnvelope||beamHold>=.10f)&&!beamReleasePose;
            // Grace owns the gesture and blocks punch/guard handoff, but it
            // pauses the charge clock. Only a positively observed beam shape
            // advances progress; this prevents a noisy frame from speeding up
            // the finisher while still protecting the in-progress hold.
            // The latch reserves the finisher against punch/guard handoff, but
            // only a positively observed shape advances its charge clock.
            // Once a finisher has accumulated a little charge, keep ownership
            // through a short camera wobble. The previous code reserved the
            // input but still advanced beamGap, so a child could see the
            // gesture held while its progress silently expired and a depth
            // spike could be read as a punch on the next frame.
            bool beamIntent=beamShape||beamEntryShape||beamMaintained||beamLatch||beamGrace;
            // Only stable depth advances the charge. An entry/maintained image
            // shape still owns the gesture and pauses the timer.
            bool beam=beamAvailable&&beamDepthStable&&(beamShape||beamMaintained);
            var leftOffset=GuardOffset(fl,flw,fs);var rightOffset=GuardOffset(fr,frw,fs);
            // Acquiring a shield still uses the original shape and hold. Once
            // established, a small visible drift at its boundary must not give
            // a noisy wrist-depth trajectory back to the punch recognizer.
            // Deliberate reach, a beam pose, lowered hands and tracking loss
            // retain their existing exit rules below.
            if(shieldHold>=ShieldHold&&beamWristsReady&&!(transformAvailable&&raised))
            {
                bool heldGuard=GuardArms(flw,frw,fcx,fsy,fs,true)||GuardArms(blw,brw,bcx,bsy,bs,true);
                guardShape|=heldGuard;guardEnvelope|=heldGuard;
            }
            // Acquire the reference before the shield's hold timer. Otherwise a
            // static depth bias could forbid defense forever at first entry.
            // An accepted extended punch must retract before it can reacquire
            // this reference, or the same held fist would turn into a shield.
            float side=l.x>=r.x?1:-1;
            bool leftReturned=!leftMotion.HoldingStrike||leftMotion.RetractedForGuard(fl,flw,scale,side);
            bool rightReturned=!rightMotion.HoldingStrike||rightMotion.RetractedForGuard(fr,frw,scale,-side);
            float observedHandSpread=Math.Abs(flw.x-frw.x)/fs;
            bool compactGuard=observedHandSpread<.65f;
            if(rawGuard&&(!guardAnchored||!guardLocked&&compactGuard)&&leftReturned&&rightReturned)
            {guardLeft=leftOffset;guardRight=rightOffset;guardAnchored=true;}
            bool leftOutward=(flw.x-fl.x)*side>=-.03f*fs&&flw.y<fsy-.04f*fs;
            bool rightOutward=(frw.x-fr.x)*-side>=-.03f*fs&&frw.y<fsy-.04f*fs;
            bool leftCommitted=(leftDepth>1.00f||rawLeftDepth>1.00f)&&(leftDepth-rightDepth>.75f||rawLeftDepth-rawRightDepth>.75f) &&
                (!guardAnchored||PoseQuality.Distance(leftOffset,guardLeft)>.16f||leftOutward);
            bool rightCommitted=(rightDepth>1.00f||rawRightDepth>1.00f)&&(rightDepth-leftDepth>.75f||rawRightDepth-rawLeftDepth>.75f) &&
                (!guardAnchored||PoseQuality.Distance(rightOffset,guardRight)>.16f||rightOutward);
            bool guardMoved=guardAnchored&&
                // A confirmed shield may widen a little while one wrist is
                // read in front of the other. The separate committed-reach
                // checks below still release it on a deliberate extension;
                // this threshold only prevents camera wobble from stealing
                // defense ownership.
                (GuardImageDistance(leftOffset,guardLeft)>.80f||GuardImageDistance(rightOffset,guardRight)>.80f);
            // Depth alone is not a guard exit. Require the hand to leave its
            // chest anchor (or visibly point outward) before a noisy depth
            // estimate is allowed to hand ownership to the punch recognizer.
            // A wide, symmetric guard can be farther from an old punch anchor;
            // use the pair's spread as a second test so that shape noise does
            // not release defense while a one-arm reach still can.
            float guardHandSpread=observedHandSpread;
            bool guardBreakSpread=guardHandSpread<1.45f;
            bool leftBreakingCandidate=leftCommitted&&
                guardBreakSpread&&(GuardImageDistance(leftOffset,guardLeft)>.16f||leftOutward);
            bool rightBreakingCandidate=rightCommitted&&
                guardBreakSpread&&(GuardImageDistance(rightOffset,guardRight)>.16f||rightOutward);
            if(leftBreakingCandidate||rightBreakingCandidate||guardMoved)
            {
                // A clear one-arm reach is the intentional exit from a held
                // shield; only camera wobble remains protected by the latch.
                guardLocked=false;guardLockAge=0;guardLostAge=.29f;
            }
            if (steady<.25f) return input;
            transformHold=transformAvailable&&raised?transformHold+dt:0;
            if (wristsReady && !raised) transformFired=false;
            if (transformHold>=TransformHold && !transformFired) { input.Transform=true; transformFired=true; }
            // A release/guard must be observed before a beam. Holding a pose while energy fills cannot auto-fire it.
            if(beamWristsReady&&!beamIntent)beamRelease+=dt;else beamRelease=0;
            // Rearming a finished beam and tolerating an unfinished hold have
            // different time limits. A 250 ms release must not clear a charge
            // still inside its half-second uncertainty grace period.
            if(beamRelease>=.25f && (beamHold<=0||beamFired||beamGap>BeamGapSeconds))
            {beamFired=false;beamArmed=true;beamHold=0;}
            if(!beamAvailable) {beamArmed=beamRelease>=.25f;beamHold=0;}
            if(beam&&beamArmed&&!beamFired) {beamHold+=dt;beamGap=0;} else
            {
                // A recognised L that temporarily loses depth stability is
                // still the same held gesture. Pause its clock instead of
                // resetting it and allowing the next frame to be a punch.
                if(beamReleasePose)
                {
                    // Hands deliberately lowered are an explicit release,
                    // even though ordinary estimator wobble gets the longer
                    // grace interval above.
                    beamHold=0;beamGap=0;
                }
                else if(beamIntent)beamGap=0;
                else {beamGap+=dt;if(beamGap>BeamGapSeconds || !beamAvailable)beamHold=0;}
            }
            if (beam && beamArmed && beamHold>=BeamHold && !beamFired)
            { input.Beam=true;beamFired=true;beamArmed=false; }
            BeamNeedsRelease=beam&&!beamArmed&&!beamFired;
            // Reserve the action during a brief uncertain interval. Missing
            // wrists pause progress; they neither advance it nor enable attacks.
            bool beamRetain=beamHold>=.10f&&beamWristsReady&&!raised&&!beamReleasePose;
            bool beamReserved=beamAvailable&&(beamIntent||beamHold>0||beamGrace||beamRetain);
            // Once the chest guard has been held, retain ownership through a
            // brief depth/visibility wobble. A beam latch still has priority.
            bool guardedEnvelope=guardEnvelope||guardLocked&&guardLostAge<=GuardOwnershipGraceSeconds;
            bool shield=guardedEnvelope&&!beamReserved&&!leftBreakingCandidate&&!rightBreakingCandidate&&!guardMoved;
            if(shield) {shieldHold+=dt;shieldGap=0;}
            else
            {
                // A latest-packet receiver can skip visible samples before an
                // overlap. Do not count that unknown preceding interval as
                // observed occlusion and spend the grace before it begins.
                shieldGap=firstWristLoss?0:shieldGap+dt;
                // Only bridge short wrist occlusion after a real guard, never an observed different action.
                if(beamWristsReady||shieldGap>.20f)shieldHold=0;
            }
            input.Shield=shieldHold>=ShieldHold;
            // A partly confirmed guard already owns the gesture. Its existing
            // 200 ms wrist-occlusion grace pauses confirmation; it must also
            // prevent the visible hand's depth noise from becoming a punch.
            input.GuardIntent=shield||shieldHold>0||guardLocked&&guardLostAge<=GuardOwnershipGraceSeconds&&!beamReserved;
            input.BeamIntent=beamReserved;
            if(input.Shield)
            {
                // Follow an arm that is still retracting into its guard, but
                // do not move the anchor forward with an outgoing punch.
                if(!guardAnchored||leftOffset.z<guardLeft.z)guardLeft=leftOffset;
                if(!guardAnchored||rightOffset.z<guardRight.z)guardRight=rightOffset;
                guardAnchored=true;
            }
            if(!guardEnvelope&&shieldGap>.20f)guardAnchored=false;
            if(!beamWristsReady&&wristLossAge>.20f)
            {
                // A real camera loss is different from one noisy packet: do
                // not let either intent latch claim a pose without wrists.
                guardLocked=false;guardLockAge=0;guardLostAge=.29f;
                beamLocked=false;beamLockAge=0;beamLostAge=.37f;
                input.Shield=false;input.GuardIntent=false;input.BeamIntent=false;
            }
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
            // While a strict chest guard is already confirmed, discard a
            // stale strike candidate from the preceding punch. The guard is
            // released on the first clear reach, so a deliberate next punch
            // still starts normally rather than being swallowed.
            // A visible chest guard owns the gesture from its first frame.
            // Previously punch history could win during latch acquisition,
            // making a child see “attack” before the shield confirmation.
            bool guardAcquiring=guardShape&&guardLockAge<.12f;
            bool shieldOwnsGesture=(guardAcquiring||shieldHold>0||guardLocked&&guardLostAge<=GuardOwnershipGraceSeconds)&&guardedEnvelope&&!leftBreakingCandidate&&!rightBreakingCandidate&&!guardMoved;
            // A guard-shaped frame must leave the chest by a visible amount
            // before a noisy depth spike can turn it into an attack.
            bool leftBreakingGuard=leftCommitted&&(!guardAnchored||PoseQuality.Distance(leftOffset,guardLeft)>.16f);
            bool rightBreakingGuard=rightCommitted&&(!guardAnchored||PoseQuality.Distance(rightOffset,guardRight)>.16f);
            input.LeftPunch=left&&!shieldOwnsGesture&&(!guardShape || leftMotion.ForwardStrike&&(leftBreakingGuard||leftOutward));
            input.RightPunch=right&&!shieldOwnsGesture&&(!guardShape || rightMotion.ForwardStrike&&(rightBreakingGuard||rightOutward));
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
            if(input.LeftPunch||input.RightPunch)
            {
                input.Shield=false;shieldHold=0;guardAnchored=false;
                // Only a confirmed, visible reach can break the guard latch.
                // This prevents one noisy depth packet from stealing defense.
                if(leftCommitted||rightCommitted){guardLocked=false;guardLockAge=0;guardLostAge=.29f;}
            }
            return input;
        }
        static PosePoint GuardOffset(PosePoint shoulder,PosePoint wrist,float scale)
            =>new PosePoint((wrist.x-shoulder.x)/scale,(wrist.y-shoulder.y)/scale){z=(shoulder.z-wrist.z)/scale};
        static float GuardImageDistance(PosePoint a,PosePoint b)
        {float x=a.x-b.x,y=a.y-b.y;return (float)Math.Sqrt(x*x+y*y);}
        static bool GuardArms(PosePoint left,PosePoint right,float cx,float sy,float scale,bool holding=false)
            =>Math.Abs(left.x-cx)<(holding?1.14f:1.06f)*scale&&Math.Abs(right.x-cx)<(holding?1.14f:1.06f)*scale&&
                // Keep the acquisition envelope centered on the chest, but
                // allow natural child-sized asymmetry and crossed forearms.
                // A fully extended fist still exits through left/right
                // committed motion below, so this does not turn a punch into
                // a shield merely because its idle hand is near the torso.
                Math.Abs(left.x-right.x)<(holding?1.95f:1.58f)*scale&&Math.Abs(left.y-right.y)<(holding?.95f:.82f)*scale&&
                left.y>sy-(holding?.98f:.88f)*scale&&right.y>sy-(holding?.98f:.88f)*scale&&
                left.y<sy+(holding?1.10f:1f)*scale&&right.y<sy+(holding?1.10f:1f)*scale;
        static bool GuardEntryArms(PosePoint left,PosePoint right,float cx,float sy,float scale)
            =>Math.Abs(left.x-cx)<1.34f*scale&&Math.Abs(right.x-cx)<1.34f*scale&&
                Math.Abs(left.x-right.x)<2.16f*scale&&Math.Abs(left.y-right.y)<1.12f*scale&&
                left.y>sy-.98f*scale&&right.y>sy-.98f*scale&&
                left.y<sy+1.08f*scale&&right.y<sy+1.08f*scale&&
                Math.Min(left.y,right.y)<sy+.52f*scale;
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
        static bool BeamImageShape(PosePoint high,PosePoint low,float cx,float sy,float scale,bool holding=false)
            =>BeamHandShape(high,low,cx,sy,scale,holding);
        static bool BeamEntryShape(PosePoint shoulder,PosePoint high,PosePoint low,PosePoint elbow,float cx,float sy,float scale)
        {
            if(!BeamEntryHandShape(high,low,cx,sy,scale))return false;
            // A one-arm forward punch can look like an L in the image plane.
            // Accept a large depth offset only when the high forearm visibly
            // rises from its elbow, which is the stable distinction in that
            // ambiguous packet. If the elbow is hidden, only a small depth
            // offset is accepted so a deep single-arm punch cannot reserve it.
            float depth=(shoulder.z-high.z)/scale;
            if(PoseQuality.Reliable(elbow,.45f))
                return elbow.y-high.y>.28f*scale&&Math.Abs(high.x-elbow.x)<.75f*scale;
            return depth<.75f;
        }
        static bool BeamEntryHandShape(PosePoint high,PosePoint low,float cx,float sy,float scale)
            =>low.y-high.y>.40f*scale && high.y<sy+.20f*scale && high.y>sy-1.35f*scale &&
              low.y>sy+.08f*scale && low.y<sy+1.15f*scale && Math.Abs(high.x-cx)<1.02f*scale &&
              Math.Abs(low.x-cx)<1.02f*scale && Math.Abs(high.x-low.x)<1.20f*scale;
        static bool ForwardImageShape(PosePoint left,PosePoint right,float sy,float scale,bool holding=false)
            =>Math.Abs(left.y-right.y)<.78f*scale && left.y>sy-(holding?.55f:.45f)*scale &&
              left.y<sy+(holding?1.10f:1f)*scale && Math.Abs(left.x-right.x)>(holding?.42f:.52f)*scale &&
              Math.Abs(left.x-right.x)<2.1f*scale;
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
        static bool ForwardPushEntry(PosePoint l,PosePoint r,PosePoint lw,PosePoint rw,float sy,float scale)
        {
            // A child pushing both hands forward often produces a weaker but
            // symmetric depth signal than the strict palm test. Require both
            // wrists to clear the shoulders and keep a visibly wide, level
            // pair so an ordinary chest guard cannot claim this route.
            float separation=Math.Abs(lw.x-rw.x)/scale;
            return (l.z-lw.z)>.42f*scale&&(r.z-rw.z)>.42f*scale&&
                separation>.62f&&separation<2.1f&&Math.Abs(lw.y-rw.y)<.65f*scale&&
                lw.y>sy-.35f*scale&&rw.y>sy-.35f*scale&&lw.y<sy+1f*scale&&rw.y<sy+1f*scale;
        }
    }
}
