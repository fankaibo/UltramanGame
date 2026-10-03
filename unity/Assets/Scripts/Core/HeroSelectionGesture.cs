using System;

namespace UltramanGame.Core
{
    // A single deliberate side wave changes the waiting-screen hero.  The
    // gesture is intentionally separate from GestureRecognizer: both hands
    // raised must remain the transform pose, while one hand held high and
    // outside its shoulder owns only selection.
    public sealed class HeroSelectionGesture
    {
        const float HoldSeconds=.28f, CooldownSeconds=.62f;
        long lastSequence,lastStamp;
        string stream;
        float hold,cooldown;
        int direction;
        bool armed=true;
        public float Progress=>Math.Min(1,hold/HoldSeconds);
        public bool Armed=>armed;
        public void Consume(){armed=false;hold=0;direction=0;cooldown=CooldownSeconds;}
        public void Reset(){lastSequence=lastStamp=0;stream=null;hold=cooldown=0;direction=0;armed=true;}
        static bool Reliable(PosePoint p)=>PoseQuality.Reliable(p,.50f);
        public int Update(PoseFrame frame,long nowMs)
        {
            if(frame==null||!PoseQuality.Valid(frame,nowMs))
            {hold=0;direction=0;cooldown=Math.Max(0,cooldown-.033f);return 0;}
            if(stream!=frame.streamId){stream=frame.streamId;lastSequence=0;lastStamp=0;hold=0;direction=0;armed=true;}
            if(frame.sequence<=lastSequence)return 0;
            float dt=lastStamp>0?Math.Max(0,Math.Min(.12f,(frame.capturedMs-lastStamp)/1000f)):.033f;
            lastSequence=frame.sequence;lastStamp=frame.capturedMs;
            cooldown=Math.Max(0,cooldown-dt);
            var p=frame.points;
            var ls=p[11];var rs=p[12];var lw=p[15];var rw=p[16];
            bool leftRaised=Reliable(lw)&&lw.x>ls.x+.18f&&lw.y<ls.y-.25f;
            bool rightRaised=Reliable(rw)&&rw.x<rs.x-.18f&&rw.y<rs.y-.25f;
            // A transform/beam pose has both wrists in the upper envelope. It
            // must never be interpreted as two quick selection events.
            if(leftRaised&&rightRaised){hold=0;direction=0;return 0;}
            bool neutral=Reliable(lw)&&Reliable(rw)&&lw.y>ls.y-.08f&&rw.y>rs.y-.08f;
            if(neutral){armed=true;hold=0;direction=0;return 0;}
            int next=leftRaised?-1:rightRaised?1:0;
            if(next==0||!armed||cooldown>0){hold=0;direction=0;return 0;}
            if(direction!=next){direction=next;hold=0;}
            hold+=dt;
            if(hold>=HoldSeconds){Consume();return next;}
            return 0;
        }
    }
}
