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
        bool beamFired, transformFired;
        float beamHold,beamGap,beamRelease,transformHold,shieldHold,steady;
        public bool ForwardPunch { get; private set; }
        public float TransformProgress => Math.Min(1,transformHold/.45f);
        public const float BeamHoldSeconds=.30f, BeamGapSeconds=.25f;
        public float BeamProgress => Math.Min(1,beamHold/BeamHoldSeconds);

        public void Reset()
        {
            stream=null; lastSequence=lastStamp=0;
            ClearGestures();
        }
        void ClearGestures()
        {
            leftMotion.Reset();rightMotion.Reset();beamFired=transformFired=ForwardPunch=false;
            beamHold=beamGap=beamRelease=transformHold=shieldHold=steady=0;
        }
        public PlayerInput Update(PoseFrame frame,long nowMs,bool beamAvailable=true,bool transformAvailable=true)
        {
            ForwardPunch=false;
            if (!PoseQuality.Present(frame,nowMs)) { Reset(); return default; }
            if (stream==frame.streamId && frame.sequence<=lastSequence) return default;
            bool fresh=stream!=frame.streamId || lastStamp==0 || frame.capturedMs-lastStamp>250 || frame.capturedMs<=lastStamp;
            float dt=fresh ? 0 : Math.Min(.1f,(frame.capturedMs-lastStamp)/1000f);
            if (fresh) ClearGestures();
            stream=frame.streamId; lastSequence=frame.sequence; lastStamp=frame.capturedMs;
            float alpha=fresh?1:(float)(1-Math.Exp(-dt/.035));
            for (int i=0;i<33;i++)
            {
                bool reliable=PoseQuality.Reliable(frame.points[i]);
                if(reliable)
                {
                    float blend=fresh||!wasReliable[i]?1:alpha;
                    smoothed[i].x += (frame.points[i].x-smoothed[i].x)*blend;
                    smoothed[i].y += (frame.points[i].y-smoothed[i].y)*blend;
                    smoothed[i].z += (frame.points[i].z-smoothed[i].z)*blend;
                }
                wasReliable[i]=reliable;
                // Beam-only tolerance: a partly occluded wrist need not alter working punch/guard input.
                bool beamVisible=PoseQuality.Reliable(frame.points[i],.45f);
                if(beamVisible)
                {
                    float blend=fresh||!beamReliable[i]?1:alpha;
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
            var bl=beamPoints[11];var br=beamPoints[12];var blw=beamPoints[15];var brw=beamPoints[16];
            float bdx=bl.x-br.x,bdy=bl.y-br.y,bdz=bl.z-br.z;
            float bs=Math.Max(.08f,(float)Math.Sqrt(bdx*bdx+bdy*bdy+bdz*bdz)),bcx=(bl.x+br.x)/2,bsy=(bl.y+br.y)/2;
            bool beamShape=beamWristsReady && (BeamArms(bl,blw,brw,bcx,bsy,bs) || BeamArms(br,brw,blw,bcx,bsy,bs) ||
                ForwardPalms(bl,br,blw,brw,bsy,bs));
            bool beam=beamAvailable&&beamShape;
            bool shield=wristsReady && !beam && !raised && Math.Abs((l.z-lw.z)-(r.z-rw.z))<.75f*scale &&
                Math.Abs(lw.x-cx)<.65f*scale && Math.Abs(rw.x-cx)<.65f*scale &&
                Math.Abs(lw.x-rw.x)<.65f*scale && lw.y>sy-.25f*scale && rw.y>sy-.25f*scale &&
                lw.y<sy+.85f*scale && rw.y<sy+.85f*scale;
            if (steady<.25f) return input;
            transformHold=transformAvailable&&raised?transformHold+dt:0;
            if (wristsReady && !raised) transformFired=false;
            if (transformHold>=.45f && !transformFired) { input.Transform=true; transformFired=true; }
            if(beam) {beamHold+=dt;beamGap=0;} else
            {beamGap+=dt;if(beamGap>BeamGapSeconds || !beamAvailable)beamHold=0;}
            // A brief imperfect pose pauses progress; it neither adds charge nor rearms a held beam.
            if(beamWristsReady&&!beamShape)beamRelease+=dt;else beamRelease=0;
            if(beamRelease>=.35f)beamFired=false;
            if (beam && beamHold>=BeamHoldSeconds && !beamFired) { input.Beam=true; beamFired=true; }
            shieldHold=shield?shieldHold+dt:0;
            input.Shield=shieldHold>=.08f;
            if (beam || raised)
            {
                leftMotion.Reset();rightMotion.Reset();
                return input;
            }
            float side=l.x>=r.x?1:-1;
            bool left=leftReady&&leftMotion.Update(l,lw,wasReliable[13],scale,side,frame.capturedMs,dt);
            bool right=rightReady&&rightMotion.Update(r,rw,wasReliable[14],scale,-side,frame.capturedMs,dt);
            if(!leftReady)leftMotion.Reset();if(!rightReady)rightMotion.Reset();
            // A forward punch can begin in a guard. Do not confuse its foreshortened arm with a held shield.
            float depthDifference=((l.z-lw.z)-(r.z-rw.z))/scale;
            input.LeftPunch=left&&(!shield || leftMotion.ForwardStrike&&depthDifference>.25f);
            input.RightPunch=right&&(!shield || rightMotion.ForwardStrike&&depthDifference<-.25f);
            ForwardPunch=input.LeftPunch&&leftMotion.ForwardStrike || input.RightPunch&&rightMotion.ForwardStrike;
            if(input.LeftPunch||input.RightPunch) {input.Shield=false;shieldHold=0;}
            return input;
        }
        static bool BeamArms(PosePoint shoulder,PosePoint high,PosePoint low,float cx,float sy,float scale)
        {
            // The hands describe the intent. Exact right angles and two unoccluded elbows are unnecessary.
            // A deeply extended single fist is a punch, even when the other wrist is lower like an L.
            return shoulder.z-high.z<.9f*scale && low.y-high.y>.22f*scale && high.y<sy+.45f*scale && high.y>sy-1.3f*scale &&
                low.y>sy-.25f*scale && low.y<sy+1.1f*scale && Math.Abs(high.x-cx)<1.25f*scale &&
                Math.Abs(low.x-cx)<.85f*scale && Math.Abs(high.x-low.x)<1.35f*scale;
        }
        static bool ForwardPalms(PosePoint l,PosePoint r,PosePoint lw,PosePoint rw,float sy,float scale)
        {
            float separation=Math.Abs(lw.x-rw.x)/scale;
            return (l.z-lw.z)>.55f*scale && (r.z-rw.z)>.55f*scale && separation>.60f && separation<2.1f &&
                Math.Abs(lw.y-rw.y)<.7f*scale && lw.y>sy-.45f*scale && rw.y>sy-.45f*scale &&
                lw.y<sy+1f*scale && rw.y<sy+1f*scale;
        }
    }
}
