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
        void CaptureGuidedProof()
        {
            bool proofInput=pose?.source=="synthetic"||review!=null;
            if(!Debug.isDebugBuild||!proofInput||System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--guided-proof")<0||proofBusy)return;
            string clawKey=battle.EnemyAttackCount%2==0?"monster-rush-left":"monster-rush-right";
            // A frame can cross the claw contact and start Hurt together. Save
            // the attacking claw first, then the later fall stages; otherwise
            // the Hurt priority can erase an entire side's contact evidence.
            bool captureClaw=!photo.Active&&battle.Phase==GamePhase.Battle&&battle.Enemy==EnemyPhase.Attack&&
                battle.EnemyAge>=Battle.EnemyHitSeconds-.02f&&battle.EnemyAge<Battle.EnemyHitSeconds+.15f&&
                !proofFrames.Contains(clawKey+(photo.Captures>0?"-after-photo":""));
            if(review!=null&&battle.Action==HeroAction.Hurt&&battle.ActionAge<.18f&&!captureClaw)return;
            // Capture the reaching claw at contact, not the first windup-like
            // frame of an attack. Keep left and right evidence independently.
            if(battle.Phase==GamePhase.Battle&&battle.Enemy==EnemyPhase.Attack&&battle.EnemyAge<Battle.EnemyHitSeconds-.02f)return;
            string key=photo.Active?"photo-"+photo.Stage+(photo.Stage==PhotoStage.Framing?(photo.HasLivePerson?"-live":"-preparing"):""):
                battle.Phase==GamePhase.Battle?(world.Closeup.Focus>.999f?"beam-closeup-peak":world.Closeup.Active?"beam-closeup":world.BeamVisible?"beam-firing":battle.Action==HeroAction.Hurt?"hero-hurt":battle.Enemy==EnemyPhase.Attack&&battle.Shield&&battle.EnemyAge>Battle.EnemyHitSeconds+.09f&&battle.EnemyAge<Battle.EnemyHitSeconds+.32f?"guard-impact":battle.Enemy==EnemyPhase.Attack?(battle.EnemyAttackCount%2==0?"monster-rush-left":"monster-rush-right"):battle.Enemy==EnemyPhase.Windup?"guard-guide":battle.Energy>=15?"beam-guide":battle.Punches>2?"battle":"battle-entry"):
                battle.Phase.ToString();
            if(!photo.Active&&battle.Phase==GamePhase.Battle&&battle.Action==HeroAction.Hurt)
                key=battle.ActionAge<KnockdownMotion.LandingSeconds+.13f?"hero-hurt":
                    battle.ActionAge<KnockdownMotion.RiseSeconds+.24f?"hero-landed":
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
                    world.VictoryAge<3.7f?"victory-turn":"victory-hero";
            if(!photo.Active&&battle.Phase==GamePhase.Transforming)
                key=world.EntranceAge<.26f?"Transforming":world.EntranceAge<1?"transform-front":
                    world.EntranceAge<1.66f?"transform-radiance":"transform-return";
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
            if(!photo.Active&&photo.Captures>0)key+="-after-photo";
            if(proofFrames.Contains(key))return;
            proofFrames.Add(key);StartCoroutine(SaveGuidedProof(key));
        }
        IEnumerator SaveGuidedProof(string key)
        {
            proofBusy=true;yield return new WaitForEndOfFrame();
            var args=System.Environment.GetCommandLineArgs();int at=System.Array.IndexOf(args,"--proof-output");
            string folder=at>=0&&at+1<args.Length?Path.GetFullPath(args[at+1]):Path.Combine(System.Environment.CurrentDirectory,"artifacts/guided-arcade/native");Directory.CreateDirectory(folder);
            var texture=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);texture.Apply();
            File.WriteAllBytes(Path.Combine(folder,key+".png"),texture.EncodeToPNG());Destroy(texture);proofBusy=false;
            Debug.Log("[GuidedProof] screenshot="+key+" source="+(review!=null?"review-playback":"synthetic")+" file="+Path.Combine(folder,key+".png"));
        }
    }
}
