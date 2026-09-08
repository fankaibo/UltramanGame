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
        public string streamId;
        public long sequence, capturedMs;
        public bool tracked;
        public PosePoint[] points;
    }

    public static class PoseQuality
    {
        public const int FreshnessMs = 350;
        static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        public static bool Valid(PoseFrame frame, long nowMs)
        {
            if (frame == null || frame.schema != 1 || string.IsNullOrEmpty(frame.streamId) ||
                frame.streamId.Length > 64 || frame.sequence < 1 || !frame.tracked ||
                frame.points == null || frame.points.Length != 33 || frame.capturedMs <= 0 ||
                nowMs-frame.capturedMs > FreshnessMs || frame.capturedMs-nowMs > 50) return false;
            foreach (var p in frame.points)
                if (!Finite(p.x) || !Finite(p.y) || !Finite(p.z) || !Finite(p.visibility) || p.visibility<0 || p.visibility>1) return false;
            for (int i=11; i<=16; i++)
            {
                var p=frame.points[i];
                if (p.visibility < .55f || p.x < 0 || p.x > 1 || p.y < 0 || p.y > 1) return false;
            }
            return Distance(frame.points[11],frame.points[12]) > .08f;
        }
        public static float Distance(PosePoint a, PosePoint b)
        { float dx=a.x-b.x,dy=a.y-b.y; return (float)Math.Sqrt(dx*dx+dy*dy); }
    }

    public struct PlayerInput
    {
        public bool Tracking, Transform, LeftPunch, RightPunch, Shield, Beam;
    }

    // This detector consumes NEW frames only. Invalid tracking resets every held gesture.
    public sealed class GestureRecognizer
    {
        string stream;
        long lastSequence, lastStamp;
        readonly PosePoint[] smoothed = new PosePoint[33];
        bool leftArmed, rightArmed, beamFired, transformFired;
        float beamHold, transformHold, shieldHold, steady;

        public void Reset()
        {
            stream=null; lastSequence=lastStamp=0;
            ClearGestures();
        }
        void ClearGestures()
        {
            leftArmed=rightArmed=beamFired=transformFired=false;
            beamHold=transformHold=shieldHold=steady=0;
        }
        public PlayerInput Update(PoseFrame frame, long nowMs)
        {
            if (!PoseQuality.Valid(frame,nowMs)) { Reset(); return default; }
            if (stream==frame.streamId && frame.sequence<=lastSequence) return default;
            bool fresh=stream!=frame.streamId || lastStamp==0 || frame.capturedMs-lastStamp>250 || frame.capturedMs<=lastStamp;
            float dt=fresh ? 0 : Math.Min(.1f,(frame.capturedMs-lastStamp)/1000f);
            if (fresh) ClearGestures();
            stream=frame.streamId; lastSequence=frame.sequence; lastStamp=frame.capturedMs;
            float alpha=fresh?1:(float)(1-Math.Exp(-dt/.055));
            for (int i=0;i<33;i++)
            {
                smoothed[i].x += (frame.points[i].x-smoothed[i].x)*alpha;
                smoothed[i].y += (frame.points[i].y-smoothed[i].y)*alpha;
            }
            var l=smoothed[11]; var r=smoothed[12];
            var le=smoothed[13]; var re=smoothed[14];
            var lw=smoothed[15]; var rw=smoothed[16];
            float scale=PoseQuality.Distance(l,r), cx=(l.x+r.x)/2, sy=(l.y+r.y)/2;
            steady+=dt;
            var input=new PlayerInput { Tracking=true };
            bool raised=lw.y<sy-.45f*scale && rw.y<sy-.45f*scale;
            bool beam=BeamArm(le,lw,re,rw,scale) || BeamArm(re,rw,le,lw,scale);
            bool shield=!beam && !raised && Math.Abs(lw.x-cx)<.55f*scale && Math.Abs(rw.x-cx)<.55f*scale &&
                lw.y>sy-.25f*scale && rw.y>sy-.25f*scale && lw.y<sy+.7f*scale && rw.y<sy+.7f*scale &&
                le.y>lw.y+.1f*scale && re.y>rw.y+.1f*scale;
            if (steady<.5f) return input;
            transformHold=raised?transformHold+dt:0;
            if (!raised) transformFired=false;
            if (transformHold>=.6f && !transformFired) { input.Transform=true; transformFired=true; }
            beamHold=beam?beamHold+dt:0;
            if (!beam) beamFired=false;
            if (beamHold>=.45f && !beamFired) { input.Beam=true; beamFired=true; }
            shieldHold=shield?shieldHold+dt:0;
            input.Shield=shieldHold>=.15f;
            if (beam || shield || raised)
            {
                leftArmed=rightArmed=false;
                return input;
            }
            input.LeftPunch=Punch(l,le,lw,scale,ref leftArmed);
            input.RightPunch=Punch(r,re,rw,scale,ref rightArmed);
            return input;
        }
        static bool BeamArm(PosePoint ve,PosePoint vw,PosePoint he,PosePoint hw,float scale)
        {
            float vx=Math.Abs(vw.x-ve.x),vy=ve.y-vw.y;
            float hx=Math.Abs(hw.x-he.x),hy=Math.Abs(hw.y-he.y);
            return vy>.45f*scale && vy>vx*1.7f && hx>.5f*scale && hx>hy*1.7f &&
                Math.Abs(vw.x-hw.x)<.45f*scale && hw.y>=vw.y && hw.y<=ve.y+.2f*scale;
        }
        static bool Punch(PosePoint shoulder,PosePoint elbow,PosePoint wrist,float scale,ref bool armed)
        {
            float distance=PoseQuality.Distance(shoulder,wrist)/scale;
            if (distance<.86f || wrist.y>shoulder.y+.8f*scale) armed=true;
            float ax=shoulder.x-elbow.x,ay=shoulder.y-elbow.y,bx=wrist.x-elbow.x,by=wrist.y-elbow.y;
            float denominator=(float)Math.Sqrt((ax*ax+ay*ay)*(bx*bx+by*by));
            float cosine=denominator>1e-6f?(ax*bx+ay*by)/denominator:1;
            bool extended=distance>1.0f && cosine<-.65f && wrist.y<shoulder.y+.6f*scale;
            if (!armed || !extended) return false;
            armed=false;
            return true;
        }
    }
}
