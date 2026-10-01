using System;
using UltramanGame.Core;

static class GestureIntentChecks
{
    static PosePoint P(float x,float y,float z=-.1f)=>new PosePoint(x,y){z=z};
    static PosePoint[] Guard()
    {
        var p=new PosePoint[33];for(int i=0;i<33;i++)p[i]=P(.5f,.5f);
        p[11]=P(.65f,.35f);p[12]=P(.35f,.35f);p[13]=P(.68f,.50f,-.12f);p[14]=P(.32f,.50f,-.12f);
        p[15]=P(.58f,.44f,-.20f);p[16]=P(.42f,.44f,-.20f);return p;
    }
    static PosePoint[] Beam(){var p=Guard();p[15]=P(.60f,.30f,-.18f);p[16]=P(.44f,.46f,-.20f);return p;}
    sealed class Trial
    {
        public readonly GestureRecognizer R=new GestureRecognizer{Difficulty=1};
        public PlayerInput Last;public int Punches,Beams;readonly int fps;long time=900000,sequence;
        public Trial(int fps){this.fps=fps;}
        public void SkipPackets(float seconds){time+=(long)Math.Round(seconds*1000);}
        public void Hold(PosePoint[] points,float seconds,bool beam=false)
        {
            for(int i=0;i<Math.Ceiling(seconds*fps);i++)
            {
                time+=(long)Math.Round(1000.0/fps);
                Last=R.Update(new PoseFrame{schema=1,tracked=true,streamId="intent",sequence=++sequence,capturedMs=time,points=points},time,beam,false);
                if(Last.LeftPunch||Last.RightPunch)Punches++;if(Last.Beam)Beams++;
            }
        }
    }
    public static void Run(Action<bool,string> check)
    {
        foreach(int fps in new[]{15,30,60})
        {
            var packetGap=new Trial(fps);packetGap.Hold(Guard(),.8f);packetGap.SkipPackets(.12f);
            var lateOverlap=Guard();lateOverlap[16].visibility=.1f;lateOverlap[15].z=-.49f;
            packetGap.Hold(lateOverlap,.16f);
            check(packetGap.Last.Shield&&packetGap.Punches==0,$"guard occlusion starts at first missing sample after skipped packets at {fps} fps");
            packetGap.Hold(lateOverlap,.25f);
            check(!packetGap.Last.Shield,$"long observed wrist loss still releases guard after skipped packets at {fps} fps");
            packetGap.Hold(Guard(),.5f);
            check(packetGap.Last.Shield&&packetGap.Punches==0,$"guard returns normally after packet-gap overlap at {fps} fps");
            var childWide=Guard();childWide[15].x=.76f;childWide[16].x=.24f;
            var wideEntry=new Trial(fps);wideEntry.Hold(childWide,.8f);
            check(wideEntry.Last.Shield&&wideEntry.Punches==0,$"slightly wider child chest guard still acquires defense at {fps} fps");
            foreach(int hand in new[]{15,16})
            {
                var switching=new Trial(fps);switching.Hold(Guard(),.8f);
                var lateral=Guard();lateral[hand]=P(hand==15?.98f:.02f,.36f,-.2f);
                lateral[hand==15?13:14]=P(hand==15?.80f:.20f,.38f,-.15f);
                switching.Hold(lateral,.2f);
                check(switching.Punches==1,$"lateral hand {hand} strike precedes guard transition at {fps} fps");
                var returnGuard=Guard();returnGuard[15].x=.73f;returnGuard[16].x=.27f;returnGuard[hand].z=-.49f;
                switching.Hold(returnGuard,1f/fps);
                check(switching.Last.GuardIntent,$"visible hand {hand} retraction reserves guard before old punch cooldown at {fps} fps");
                var switchOverlap=(PosePoint[])returnGuard.Clone();switchOverlap[hand==15?16:15].visibility=.1f;
                switching.Hold(switchOverlap,.12f);switching.Hold(returnGuard,.5f);
                check(switching.Last.Shield&&switching.Punches==1,$"retraction with initial overlap cannot produce another hand {hand} punch at {fps} fps");
                var acquiring=new Trial(fps);var rest=Guard();rest[15].y=rest[16].y=.75f;
                var skipped=new Trial(fps);skipped.Hold(rest,.8f);
                var firstOverlap=Guard();firstOverlap[hand].z=-.49f;firstOverlap[hand==15?16:15].visibility=.1f;
                // The player takes the latest packet; a rendering stall may
                // skip the first complete guard and receive the overlap first.
                skipped.Hold(firstOverlap,.12f);
                check(skipped.Punches==0&&!skipped.Last.Shield,$"first observed guard frame may hide a wrist without punching at {fps} fps");
                skipped.Hold(Guard(),.5f);
                check(skipped.Last.Shield&&skipped.Punches==0,$"guard acquires after skipped entry packets at {fps} fps");
                acquiring.Hold(rest,.8f);acquiring.Hold(Guard(),1f/fps);
                float pending=acquiring.R.ShieldProgress;
                check(pending>0&&pending<1,$"guard starts confirmation before acquisition overlap at {fps} fps");
                var overlap=Guard();overlap[hand].z=-.49f;overlap[hand==15?16:15].visibility=.1f;
                acquiring.Hold(overlap,.12f);
                check(acquiring.Last.GuardIntent&&!acquiring.Last.Shield&&acquiring.Punches==0,
                    $"partly confirmed guard owns input during hand {hand} overlap at {fps} fps");
                check(Math.Abs(acquiring.R.ShieldProgress-pending)<.001f,
                    $"occluded acquisition cannot earn shield confirmation at {fps} fps");
                acquiring.Hold(Guard(),.35f);
                check(acquiring.Last.Shield&&acquiring.Punches==0,$"guard completes after acquisition overlap at {fps} fps");
                acquiring=new Trial(fps);acquiring.Hold(rest,.8f);acquiring.Hold(Guard(),1f/fps);acquiring.Hold(overlap,.5f);
                check(!acquiring.Last.GuardIntent&&!acquiring.Last.Shield&&acquiring.Punches==0,
                    $"long acquisition overlap releases ownership without a stale punch at {fps} fps");

                var initial=new Trial(fps);initial.Hold(rest,.8f,true);
                var biased=Beam();biased[15].z=-.53f;
                if(hand==16)
                {
                    for(int a=11;a<=15;a+=2){var swap=biased[a];biased[a]=biased[a+1];biased[a+1]=swap;}
                }
                initial.Hold(biased,1.4f,true);
                check(initial.Beams==1&&initial.Punches==0,$"visible forearms acquire hand {hand} L despite initial depth bias at {fps} fps");
                initial.Hold(biased,1.2f,true);
                check(initial.Beams==1,$"depth-biased L still fires only once at {fps} fps");
                initial=new Trial(fps);initial.Hold(biased,1.4f,false);initial.Hold(biased,1.4f,true);
                check(initial.Beams==0&&initial.R.BeamNeedsRelease,$"pre-held depth-biased L must release before firing at {fps} fps");
                initial.Hold(rest,.5f,true);initial.Hold(biased,1.4f,true);
                check(initial.Beams==1,$"released depth-biased L can fire at {fps} fps");
                var horizontal=(PosePoint[])biased.Clone();horizontal[hand==15?13:14].y=horizontal[hand].y+.02f;
                var rejected=new Trial(fps);rejected.Hold(rest,.8f,true);rejected.Hold(horizontal,1.4f,true);
                check(rejected.Beams==0,$"forward horizontal arm does not borrow L depth tolerance at {fps} fps");
                var strike=Guard();strike[hand]=P(hand==15?.65f:.35f,.28f,-.53f);
                strike[hand==15?13:14]=P(hand==15?.66f:.34f,.29f,-.35f);
                strike[hand==15?16:15].y=.50f;
                rejected=new Trial(fps);rejected.Hold(Guard(),.8f,true);rejected.Hold(strike,1.4f,true);
                check(rejected.Beams==0&&rejected.Punches==1,
                    $"genuine hand {hand} forward strike still punches with full beam energy at {fps} fps");
                var hidden=(PosePoint[])biased.Clone();hidden[13].visibility=hidden[14].visibility=.1f;
                rejected=new Trial(fps);rejected.Hold(rest,.8f,true);rejected.Hold(hidden,1.4f,true);
                check(rejected.Beams==0,$"missing forearms cannot establish depth-biased L at {fps} fps");
            }
            foreach(int hand in new[]{15,16})
            {
                var edge=new Trial(fps);var wide=Guard();wide[15].x=.73f;wide[16].x=.27f;
                edge.Hold(wide,.8f);
                check(edge.Last.Shield,$"wide chest guard acquired before edge jitter at {fps} fps");
                var drift=(PosePoint[])wide.Clone();drift[15].x=.747f;drift[16].x=.253f;drift[hand].z=-.49f;
                var unconfirmed=new Trial(fps);unconfirmed.Hold(drift,.8f);
                check(!unconfirmed.Last.Shield,$"guard hold tolerance cannot acquire a new shield for hand {hand} at {fps} fps");
                edge.Hold(drift,.2f);
                check(edge.Last.Shield&&edge.Punches==0,$"small outward wrist drift keeps guard ownership despite hand {hand} depth noise at {fps} fps");
                edge.Hold(wide,.4f);
                var reach=(PosePoint[])wide.Clone();reach[hand]=P(hand==15?.64f:.36f,.40f,-.49f);
                edge.Hold(reach,.2f);
                check(edge.Punches==1&&!edge.Last.Shield,$"deliberate hand {hand} strike can still leave widened guard at {fps} fps");
                edge.Hold(Guard(),.5f);
                var lowered=Guard();lowered[15].y=lowered[16].y=.78f;edge.Hold(lowered,.4f);
                check(!edge.Last.Shield&&!edge.Last.GuardIntent,$"lowered hands leave guard tolerance immediately at {fps} fps");
                edge.Hold(wide,.6f,true);edge.Hold(Beam(),1.3f,true);
                check(edge.Beams==1&&!edge.Last.Shield,$"L-pose can take over an established wide guard at {fps} fps");
            }
            var transition=new Trial(fps);var relaxed=Guard();relaxed[15].y=relaxed[16].y=.72f;
            transition.Hold(relaxed,.7f,true);transition.Hold(Beam(),.5f/fps,true);
            var entryNoise=Beam();entryNoise[15].z=-.53f;transition.Hold(entryNoise,1.2f,true);
            check(transition.Beams==1&&transition.Punches==0,$"complete raw L owns transition before smoothing catches up at {fps} fps");
            foreach(int hand in new[]{15,16})
            {
                var entry=new Trial(fps);
                var firstGuard=Guard();firstGuard[hand].z=-.49f;
                entry.Hold(firstGuard,.8f);
                check(entry.Last.Shield&&entry.Punches==0,$"guard can acquire with an initially biased wrist depth for hand {hand} at {fps} fps");
                var reach=(PosePoint[])firstGuard.Clone();reach[hand]=P(hand==15?.65f:.35f,.39f,-.80f);
                entry.Hold(reach,.5f);
                check(entry.Punches==1&&!entry.Last.Shield,$"real reach can leave initially biased guard without becoming shield again at {fps} fps");
                entry.Hold(Guard(),.5f);
                check(entry.Last.Shield&&entry.Punches==1,$"retracting after a biased guard strike restores defense at {fps} fps");
            }
            var early=new Trial(fps);early.Hold(Guard(),.7f,true);
            for(int i=0;i<12&&early.R.BeamProgress==0;i++)early.Hold(Beam(),.5f/fps,true);
            check(early.R.BeamProgress>0&&early.R.BeamProgress<.15f,$"early beam intent acquired for startup noise at {fps} fps");
            var earlyNoise=Beam();earlyNoise[15].z=-.53f;
            early.Hold(earlyNoise,1.2f,true);
            check(early.Beams==1&&early.Punches==0,$"depth noise immediately after L-pose entry cannot strand finisher at {fps} fps");
            early=new Trial(fps);early.Hold(Guard(),.7f,true);
            for(int i=0;i<12&&early.R.BeamProgress==0;i++)early.Hold(Beam(),.5f/fps,true);
            early.Hold(Guard(),1.2f,true);
            check(early.Beams==0&&early.R.BeamProgress==0&&early.Last.Shield,$"early L cancelled into chest defense never completes a finisher at {fps} fps");
            var t=new Trial(fps);t.Hold(Guard(),.7f);
            var noisyGuard=Guard();noisyGuard[15].z=-.34f;
            for(int n=0;n<4;n++){t.Hold(noisyGuard,.14f);t.Hold(Guard(),.16f);}
            check(t.Punches==0&&t.Last.Shield,$"held defense survives unequal estimated wrist depth without attacking at {fps} fps");
            foreach(int hand in new[]{15,16})
            {
                t=new Trial(fps);t.Hold(Guard(),.7f);
                var depthJump=Guard();depthJump[hand].z=-.49f;
                // Keep the same arm shape while moving the entire torso too.
                for(int j=0;j<depthJump.Length;j++){depthJump[j].x+=.04f;depthJump[j].y-=.02f;}
                t.Hold(depthJump,.20f);
                check(t.Last.Shield&&t.Punches==0,$"depth-only jump cannot release guard hand {hand} at {fps} fps");
                t.Hold(Guard(),.4f);
                var reach=Guard();reach[hand]=P(hand==15?.64f:.36f,.40f,-.49f);t.Hold(reach,.20f);
                check(t.Punches==1&&!t.Last.Shield,$"visible forward reach can leave guard hand {hand} at {fps} fps");
            }
            t=new Trial(fps);var face=Guard();face[15].y=face[16].y=.22f;t.Hold(face,.8f);
            check(t.Last.Shield&&t.Punches==0,$"hands protecting face remain defense during battle at {fps} fps");
            foreach(int hand in new[]{15,16})
            {
                t=new Trial(fps);t.Hold(Guard(),.7f);
                var overlap=Guard();overlap[hand].z=-.49f;overlap[hand==15?16:15].visibility=.1f;
                t.Hold(overlap,.12f);
                check(t.Last.Shield&&t.Punches==0,$"brief hidden opposite wrist cannot turn guard into hand {hand} attack at {fps} fps");
                t.Hold(Guard(),.4f);
                check(t.Last.Shield&&t.Punches==0,$"guard recovers after opposite wrist overlap for hand {hand} at {fps} fps");
                t.Hold(overlap,.45f);
                check(!t.Last.Shield&&t.Punches==0,$"long wrist loss releases defense without replaying overlap motion at {fps} fps");
                t.Hold(Guard(),.4f);
                var reach=Guard();reach[hand]=P(hand==15?.64f:.36f,.40f,-.49f);t.Hold(reach,.20f);
                check(t.Punches==1&&!t.Last.Shield,$"fresh hand {hand} reach still attacks after guard overlap at {fps} fps");
            }
            t=new Trial(fps);t.Hold(Guard(),.7f,true);t.Hold(Beam(),.3f,true);
            var wobble=Beam();wobble[15].z=-.46f;t.Hold(wobble,.16f,true);t.Hold(Beam(),.55f,true);
            check(t.Beams==1&&t.Punches==0,$"L-pose depth wobble cannot steal charging as a punch at {fps} fps");
            t.Hold(Beam(),1,true);check(t.Beams==1,$"held finisher fires only once after depth wobble at {fps} fps");
            // A real webcam can hold an ambiguous depth estimate for longer
            // than one render hiccup.  Once charge has started this interval
            // must remain owned by the finisher instead of becoming a punch.
            t=new Trial(fps);t.Hold(Guard(),.7f,true);t.Hold(Beam(),.35f,true);
            var longUncertain=Beam();longUncertain[15].z=-.92f;longUncertain[16].z=-.08f;
            longUncertain[15].y=.34f;longUncertain[16].y=.48f;
            t.Hold(longUncertain,.72f,true);t.Hold(Beam(),.55f,true);
            check(t.Beams==1&&t.Punches==0,$"long finisher depth interruption stays owned at {fps} fps");
            t=new Trial(fps);t.Hold(Guard(),.7f,true);t.Hold(Beam(),.45f,true);
            float progress=t.R.BeamProgress;
            var uncertain=Beam();uncertain[15].z=-.9f;uncertain[16].z=-.1f;t.Hold(uncertain,.26f,true);
            check(t.R.BeamProgress>=progress&&t.Beams==0&&t.Punches==0,$"visible pose uncertainty within the grace window pauses rather than resets charge at {fps} fps");
            t.Hold(Beam(),.65f,true);
            check(t.Beams==1&&t.Punches==0,$"finisher completes after a visible short pose uncertainty at {fps} fps");
            t=new Trial(fps);t.Hold(Guard(),.7f,true);t.Hold(Beam(),.3f,true);
            var missing=Beam();missing[15].visibility=.1f;t.Hold(missing,.26f,true);
            check(t.Beams==0&&t.R.BeamProgress>0&&t.Punches==0,$"brief hidden wrist pauses charge without clearing or attacking at {fps} fps");
            t.Hold(Beam(),.7f,true);check(t.Beams==1&&t.Punches==0,$"charge resumes after a brief overlap at {fps} fps");
            t=new Trial(fps);t.Hold(Guard(),.7f,true);t.Hold(Beam(),.3f,true);
            var down=Guard();down[15].y=down[16].y=.78f;t.Hold(down,.55f,true);
            check(t.Beams==0&&t.R.BeamProgress==0,$"putting hands down cancels the reserved beam at {fps} fps");
            var punch=Guard();punch[15]=P(.62f,.40f,-.48f);t.Hold(Guard(),.4f);t.Hold(punch,.6f);
            check(t.Punches==1,$"a deliberate fresh punch still works after cancellation at {fps} fps");
        }
    }
}
