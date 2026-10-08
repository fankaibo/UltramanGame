using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    public sealed partial class ArenaController
    {
        readonly HashSet<string> proofFrames=new HashSet<string>();
        bool proofBusy;
        float proofLastKickTime=-10;
        void CaptureGuidedProof()
        {
            bool proofInput=pose?.source=="synthetic"||review!=null;
            if(!TraceEnabled||!proofInput||!GuidedProofRequested||proofBusy)return;
            if(!photo.Active&&hero.TwinShoot!=null&&battle.Phase==GamePhase.Battle)
            {
                string twin=hero.TwinShoot.Active&&battle.Action==HeroAction.Beam?
                    hero.TwinShoot.Age>.18f&&hero.TwinShoot.Age<.60f?"zero-twin-grab":
                    hero.TwinShoot.Dock>.999f&&world.Closeup.Active?"zero-twin-chest":null:
                    !hero.TwinShoot.Active&&proofFrames.Contains("zero-twin-chest")?"zero-twin-restored":null;
                if(twin!=null&&proofFrames.Add(twin)){StartCoroutine(SaveGuidedProof(twin));return;}
            }
            if(review?.GuardHandoffEnabled==true&&battle.Phase==GamePhase.Battle&&battle.Punches>=1)
            {
                string guardKey=battle.Shield&&hero.GuardHandoffProgress<1?"guard-handoff-active":
                    battle.Shield&&proofFrames.Contains("guard-handoff-active")?"guard-handoff-settled":
                    review.GuardHandoffObserved&&battle.IsPunch&&battle.Punches==2?"guard-counter-contact":null;
                if(guardKey!=null&&proofFrames.Add(guardKey)){StartCoroutine(SaveGuidedProof(guardKey));return;}
            }
            if(!photo.Active&&world.Projectile.LaunchVisible&&battle.IsRangedPunch&&battle.ActionAge<AttackTempo.RangedLaunchSeconds&&proofFrames.Add("ranged-launch-volume"))
            {StartCoroutine(SaveGuidedProof("ranged-launch-volume"));return;}
            if(!photo.Active&&world.Projectile.ImpactVisible&&proofFrames.Add("ranged-impact-volume"))
            {StartCoroutine(SaveGuidedProof("ranged-impact-volume"));return;}
            if(!photo.Active&&battle.Phase==GamePhase.Battle&&battle.Action==HeroAction.None&&
                battle.Punches>=3&&hero.EngagementWeight>.99f&&proofFrames.Add("engagement-held-stance"))
            {
                Debug.Log($"[Engagement] held hero={SelectedHero.Id} weight={hero.EngagementWeight:F3} distance={Vector3.Distance(hero.StancePosition,world.HeroHome):F3}");
                StartCoroutine(SaveGuidedProof("engagement-held-stance"));return;
            }
            if(!photo.Active&&world.Blade.Visible&&battle.ActionAge>.055f&&battle.ActionAge<.12f&&proofFrames.Add("mebium-blade"))
            {StartCoroutine(SaveGuidedProof("mebium-blade"));return;}
            if(!photo.Active&&HeroArsenal.Sluggers(SelectedHero.Id)&&world.Projectile.Visible)
            {
                string weapon=battle.Shot.Age>.33f?"zero-sluggers-return":battle.Shot.Age>.20f?"zero-sluggers-out":null;
                if(weapon!=null&&proofFrames.Add(weapon)){StartCoroutine(SaveGuidedProof(weapon));return;}
            }
            if(!photo.Active&&world.Projectile.Visible&&AttackTempo.Travel(battle.Shot.Age)>.30f&&AttackTempo.Travel(battle.Shot.Age)<.85f)
            {
                string shotKey="light-bullet-"+(battle.Shot.Side==HeroAction.LeftPunch?"left":"right");
                if(proofFrames.Add(shotKey)){StartCoroutine(SaveGuidedProof(shotKey));return;}
            }
            if(HeroKickMotion.Active(battle))proofLastKickTime=Time.unscaledTime;
            else if(!photo.Active&&battle.Phase==GamePhase.Battle&&battle.Action==HeroAction.None&&!battle.Shield&&
                Time.unscaledTime-proofLastKickTime<.75f&&hero.FootPosition(true).y<.4f&&hero.FootPosition(false).y<.4f)
            {
                // A slow screenshot can step across the last 50 ms of the
                // kick. Capture the actual planted feet after it, rather than
                // requiring a narrow synthetic time window to be visited.
                string landed="kick-setdown"+(photo.Captures>0?"-after-photo":"");
                if(proofFrames.Add(landed)){StartCoroutine(SaveGuidedProof(landed));return;}
            }
            if(!photo.Active&&battle.Phase==GamePhase.Battle&&enemy.BeamRecoilAge>=.39f&&enemy.BeamRecoilAge<.65f)
            {
                // A terminal beam has no later beam to recover a missed frame.
                // Capture its planted brace before the overlapping arm/shot tags.
                string brace="beam-braced"+(photo.Captures>0?"-after-photo":"");
                if(proofFrames.Add(brace)){StartCoroutine(SaveGuidedProof(brace));return;}
            }
            if(!photo.Active&&battle.Phase==GamePhase.Battle&&enemy.RecoveryWeight>.65f)
            {
                string recoverKey=battle.EnemyAge>=.6f&&battle.EnemyAge<.8f?"monster-recovery-drop":
                    battle.EnemyAge>=1.3f&&battle.EnemyAge<1.5f?"monster-recovery-return":null;
                if(recoverKey!=null&&proofFrames.Add(recoverKey)){StartCoroutine(SaveGuidedProof(recoverKey));return;}
            }
            if(!photo.Active&&battle.Phase==GamePhase.Battle&&hero.LinkedPunchWeight>.5f)
            {
                string linkKey=battle.BufferedPunch!=HeroAction.None&&battle.ActionAge>.31f?"punch-link-prepare":
                    (battle.Action==HeroAction.LeftPunch||battle.Action==HeroAction.RightPunch)&&battle.ActionAge<.1f?"punch-link-handoff":null;
                if(linkKey!=null&&proofFrames.Add(linkKey)){StartCoroutine(SaveGuidedProof(linkKey));return;}
            }
            if(!photo.Active&&world.BeamReactionCloseup)
            {
                string reactionKey=battle.ActionAge<.78f?"beam-reaction-entry":battle.ActionAge>1.05f?"beam-reaction-peak":null;
                if(reactionKey!=null&&proofFrames.Add(reactionKey)){StartCoroutine(SaveGuidedProof(reactionKey));return;}
            }
            if(!photo.Active&&battle.Phase==GamePhase.Battle&&enemy.ClawReactionAmount>.28f)
            {
                string armKey=enemy.BeamRecoilAge>=.35f&&enemy.BeamRecoilAge<.65f?"monster-arms-beam":
                    enemy.LaunchAge>.16f&&enemy.LaunchAge<.45f?"monster-arms-uppercut":
                    battle.Action==HeroAction.LeftPunch?"monster-arms-left":battle.Action==HeroAction.RightPunch?"monster-arms-right":null;
                if(armKey!=null&&proofFrames.Add(armKey)){StartCoroutine(SaveGuidedProof(armKey));return;}
            }
            if(!photo.Active&&MonsterRayMotion.Active(battle))
            {
                string rayKey=battle.Enemy==EnemyPhase.Windup&&battle.WarningDuration-battle.EnemyAge<.18f?"ray-prepare":
                    battle.Enemy==EnemyPhase.Attack?(battle.EnemyAge>=.24f&&battle.EnemyAge<.36f?"ray-travel":
                    battle.EnemyAge>=.46f&&battle.EnemyAge<.62f?"ray-block":
                    battle.EnemyAge>=.75f&&battle.EnemyAge<.85f?"ray-fade":null):battle.Enemy==EnemyPhase.Recover?"ray-recover":null;
                if(rayKey!=null&&proofFrames.Add(rayKey)){StartCoroutine(SaveGuidedProof(rayKey));return;}
            }
            if(!photo.Active&&MonsterSlamMotion.Active(battle))
            {
                string slamKey=battle.Enemy==EnemyPhase.Windup&&battle.WarningDuration-battle.EnemyAge<.2f?"slam-prepare":
                    battle.Enemy==EnemyPhase.Attack?(battle.EnemyAge>=.13f&&battle.EnemyAge<.25f?"slam-swing":
                    battle.EnemyAge>=.28f&&battle.EnemyAge<.36f?"slam-ground":
                    battle.EnemyAge>=.44f&&battle.EnemyAge<.62f?"slam-wave":
                    battle.EnemyAge>=.78f&&battle.EnemyAge<.98f?"slam-rise":null):null;
                if(slamKey!=null&&proofFrames.Add(slamKey)){StartCoroutine(SaveGuidedProof(slamKey));return;}
            }
            if(!photo.Active&&MonsterRockMotion.Active(battle))
            {
                string rockKey=world.Rock.Holding&&battle.WarningDuration-battle.EnemyAge<.4f?"monster-rock-hold":
                    world.Rock.Flying&&battle.EnemyAge>.42f?"monster-rock-flight":world.Rock.Fragments?"monster-rock-break":null;
                if(rockKey!=null&&proofFrames.Add(rockKey)){StartCoroutine(SaveGuidedProof(rockKey));return;}
            }
            string clawKey=MonsterRockMotion.Active(battle)?"monster-rock-flight":MonsterRayMotion.Active(battle)?"ray-travel":MonsterSlamMotion.Active(battle)?"slam-swing":MonsterStepMotion.ClawLeft(battle.EnemyAttackCount)?"monster-rush-left":"monster-rush-right";
            // A frame can cross the claw contact and start Hurt together. Save
            // the attacking claw first, then the later fall stages; otherwise
            // the Hurt priority can erase an entire side's contact evidence.
            bool captureClaw=!photo.Active&&battle.Phase==GamePhase.Battle&&battle.Enemy==EnemyPhase.Attack&&
                !MonsterRockMotion.Active(battle)&&!MonsterRayMotion.Active(battle)&&!MonsterSlamMotion.Active(battle)&&
                battle.EnemyAge>=battle.EnemyContactSeconds-.02f&&battle.EnemyAge<battle.EnemyContactSeconds+.15f&&
                !proofFrames.Contains(clawKey+(photo.Captures>0?"-after-photo":""));
            if(review!=null&&battle.Action==HeroAction.Hurt&&battle.ActionAge<.18f&&!captureClaw)return;
            // Capture the reaching claw at contact, not the first windup-like
            // frame of an attack. Keep left and right evidence independently.
            if(battle.Phase==GamePhase.Battle&&battle.Enemy==EnemyPhase.Attack&&battle.EnemyAge<battle.EnemyContactSeconds-.02f)return;
            string key=photo.Active?"photo-"+photo.Stage+(photo.Stage==PhotoStage.Framing?(photo.HasLivePerson?"-live":"-preparing"):""):
                battle.Phase==GamePhase.Battle?(world.Closeup.Focus>.999f?"beam-closeup-peak":world.Closeup.Active?"beam-closeup":world.BeamVisible?"beam-firing":battle.Action==HeroAction.Hurt?"hero-hurt":battle.Enemy==EnemyPhase.Attack&&battle.Shield&&battle.EnemyAge>battle.EnemyContactSeconds+.09f&&battle.EnemyAge<battle.EnemyContactSeconds+.32f?"guard-impact":battle.Enemy==EnemyPhase.Attack?clawKey:battle.Enemy==EnemyPhase.Windup?"guard-guide":battle.Energy>=15?"beam-guide":battle.Punches>2?"battle":"battle-entry"):
                battle.Phase.ToString();
            if(!photo.Active&&battle.Phase==GamePhase.Battle&&battle.Action==HeroAction.Hurt)
                key=battle.ActionAge<KnockdownMotion.LandingSeconds+.13f?"hero-hurt":
                    battle.ActionAge<KnockdownMotion.RiseSeconds+.24f?"hero-landed":
                    battle.ActionAge>=1.15f&&battle.ActionAge<1.35f?"hero-rise-support":
                    battle.ActionAge<KnockdownMotion.Duration-.20f?"hero-rising":"hero-recovered";
            if(!photo.Active&&battle.Phase==GamePhase.Battle&&world.BeamVisible)
                key=battle.ActionAge<Battle.BeamHitSeconds?"beam-firing":battle.ActionAge<.62f?"beam-contact":
                    battle.ActionAge<1.3f?"beam-sustain":"beam-fade";
            if(!photo.Active&&battle.Phase==GamePhase.Battle&&battle.Enemy!=EnemyPhase.Attack&&battle.Punches>0&&
                battle.ActionAge>=.20f&&battle.ActionAge<.28f)
            {
                if(battle.Action==HeroAction.LeftPunch)key="left-punch-recoil";
                else if(battle.Action==HeroAction.RightPunch)key="right-punch-recoil";
            }
            if(!photo.Active&&battle.Phase==GamePhase.Victory)
                key=world.VictoryAge<.8f?"Victory":world.VictoryAge<1.45f?"victory-collapse":
                    world.VictoryAge<3.04f?"victory-turn":world.VictoryAge<3.3f?"victory-dissolve":
                    world.VictoryAge<3.7f?"victory-motes":"victory-hero";
            if(!photo.Active&&battle.Phase==GamePhase.Victory&&world.DefeatImpactVisible&&
                proofFrames.Contains(key+(photo.Captures>0?"-after-photo":"")))
            {
                if(world.DefeatImpactAge>=.14f&&world.DefeatImpactAge<.42f)key="defeat-flash";
                else if(world.DefeatImpactAge>=.55f&&world.DefeatImpactAge<.95f)key="defeat-billows";
                else if(world.DefeatImpactAge>=1.3f&&world.DefeatImpactAge<1.7f)key="defeat-settling";
            }
            if(!photo.Active&&battle.Phase==GamePhase.Transforming)
                key=world.EntranceAge<.26f?"Transforming":world.EntranceAge<1?"transform-front":
                    world.EntranceAge<1.66f?"transform-radiance":world.EntranceAge<MonsterEntranceMotion.Start?"transform-return":
                    world.EntranceAge<2.8f?"monster-entrance-step":world.EntranceAge<3.2f?"monster-entrance-plant":
                    world.EntranceAge<3.65f?"monster-entrance-roar":world.MonsterEntranceCloseup?"monster-entrance-settle":"monster-entrance-return";
            if(!photo.Active&&battle.Enemy!=EnemyPhase.Attack&&ComboStrikeMotion.Active(battle)&&
                battle.ActionAge>=Battle.PunchHitSeconds&&battle.ActionAge<.22f)
                key=battle.Action==HeroAction.LeftPunch?"combo-left":"combo-right";
            if(!photo.Active&&battle.Phase==GamePhase.Battle&&battle.Enemy==EnemyPhase.Windup)
            {
                if(world.ThreatFocus>.99f&&battle.EnemyAge>1.2f)key="monster-threat";
                else if(battle.EnemyAge>=3.5f&&battle.EnemyAge<4)key="monster-threat-return";
            }
            if(!photo.Active&&battle.Phase==GamePhase.Battle&&battle.Enemy!=EnemyPhase.Attack&&battle.Action!=HeroAction.Beam&&enemy.StaggerAge<MonsterStaggerMotion.Duration)
            {
                if(enemy.StaggerAge>=.10f&&enemy.StaggerAge<MonsterStaggerMotion.Landing)key="monster-stagger-lift";
                else if(enemy.StaggerAge>=MonsterStaggerMotion.Landing&&enemy.StaggerAge<MonsterStaggerMotion.Return)key="monster-stagger-land";
                else if(enemy.StaggerAge>=MonsterStaggerMotion.Return+.1f)key="monster-stagger-return";
            }
            if(!photo.Active&&battle.Phase==GamePhase.Battle&&enemy.LaunchAge<MonsterLaunchMotion.Duration)
            {
                if(enemy.LaunchAge>=.22f&&enemy.LaunchAge<.46f)key="uppercut-airborne";
                else if(enemy.LaunchAge>=MonsterLaunchMotion.Landing&&enemy.LaunchAge<MonsterLaunchMotion.Recovery)key="uppercut-land";
                else if(enemy.LaunchAge>=MonsterLaunchMotion.Recovery+.12f)key="uppercut-recover";
            }
            if(!photo.Active&&battle.Phase==GamePhase.Battle&&world.ActiveContactCount>0&&!ComboStrikeMotion.Active(battle)&&
                (battle.Action==HeroAction.LeftPunch||battle.Action==HeroAction.RightPunch)&&battle.ActionAge>=Battle.PunchHitSeconds&&battle.ActionAge<=.24f)
            {
                string contactKey=battle.Action==HeroAction.LeftPunch?"punch-impact-left":"punch-impact-right";
                if(!proofFrames.Contains(contactKey+(photo.Captures>0?"-after-photo":"")))key=contactKey;
            }
            // Preserve the contact/landing evidence first, then capture the
            // sustained close framing and its return on following frames.
            if(!photo.Active&&battle.Phase==GamePhase.Battle&&!battle.Shield&&
                (battle.Enemy==EnemyPhase.Rest||battle.Enemy==EnemyPhase.Recover)&&
                proofFrames.Contains(key+(photo.Captures>0?"-after-photo":"")))
            {
                if(world.ComboFocus>.95f&&world.ComboCameraAge>=.24f)key="combo-camera-peak";
                else if(world.ComboCameraAge>.55f&&world.ComboFocus>.01f&&world.ComboFocus<.30f)key="combo-camera-return";
            }
            if(captureClaw)key=clawKey;
            if(!photo.Active&&battle.Finishing&&proofFrames.Contains(key+(photo.Captures>0?"-after-photo":"")))
            {
                if(battle.Action==HeroAction.Beam)
                    key=battle.ActionAge<.75f?"final-strike-contact":battle.ActionAge<1.28f?"final-strike-sustain":"final-strike-release";
                else if(battle.ActionAge>=.24f)key="final-punch-recovery";
            }
            if(!photo.Active&&battle.Phase==GamePhase.Battle&&enemy.BeamRecoilAge<MonsterBeamMotion.Duration&&
                proofFrames.Contains(key+(photo.Captures>0?"-after-photo":"")))
            {
                float age=enemy.BeamRecoilAge;
                if(age>.39f&&age<.65f)key="beam-braced";
                else if(age>.82f&&age<1.02f)key="beam-pressure";
                else if(age>1.35f&&age<1.62f)key="beam-recovery";
            }
            if(!captureClaw&&!photo.Active&&world.ActiveGroundStones>0&&world.GroundImpactAge>=.18f&&world.GroundImpactAge<.36f&&
                proofFrames.Contains(key+(photo.Captures>0?"-after-photo":"")))
            {
                string groundKey="ground-"+world.GroundContactCause;
                if(!proofFrames.Contains(groundKey+(photo.Captures>0?"-after-photo":"")))key=groundKey;
            }
            if(!photo.Active&&battle.Phase==GamePhase.Battle&&world.OutpostDamage.Detached>=2&&
                proofFrames.Contains(key+(photo.Captures>0?"-after-photo":"")))
            {
                string outpost=world.OutpostDamage.ActiveClouds>=3?"outpost-dust":world.OutpostDamage.ActiveClouds==0?"outpost-settled":null;
                if(outpost!=null&&!proofFrames.Contains(outpost+(photo.Captures>0?"-after-photo":"")))key=outpost;
            }
            // A single new kick frame takes priority over already recurring
            // stagger/camera shots. Do this after their classification, or
            // the retraction evidence is silently replaced by the footstep.
            if(!captureClaw&&!photo.Active&&HeroKickMotion.Active(battle))
            {
                string kickSuffix=battle.Punches>=24?"-air":"";
                string kickKey=(battle.ActionAge<Battle.PunchHitSeconds?"kick-chamber":battle.ActionAge<.23f?"kick-contact":battle.ActionAge<.33f?"kick-retract":"kick-setdown")+kickSuffix;
                if(!proofFrames.Contains(kickKey+(photo.Captures>0?"-after-photo":"")))key=kickKey;
            }
            if(!photo.Active&&photo.Captures>0)key+="-after-photo";
            if(proofFrames.Contains(key))return;
            proofFrames.Add(key);StartCoroutine(SaveGuidedProof(key));
        }
        IEnumerator SaveGuidedProof(string key)
        {
            proofBusy=true;
            // Read the settled review image, not the temporary shutter flash.
            if(key=="photo-Review")yield return new WaitForSecondsRealtime(.35f);
            yield return new WaitForEndOfFrame();
            var args=System.Environment.GetCommandLineArgs();int at=System.Array.IndexOf(args,"--proof-output");
            string folder=at>=0&&at+1<args.Length?Path.GetFullPath(args[at+1]):Path.Combine(System.Environment.CurrentDirectory,"artifacts/guided-arcade/native");Directory.CreateDirectory(folder);
            var texture=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);texture.Apply();
            File.WriteAllBytes(Path.Combine(folder,key+".png"),texture.EncodeToPNG());Destroy(texture);proofBusy=false;
            Debug.Log("[GuidedProof] screenshot="+key+" source="+(review!=null?"review-playback":"synthetic")+" file="+Path.Combine(folder,key+".png"));
        }
    }
}
