using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class CinematicReview
    {
        public static void RenderRelease() {RiggedReview.ReviewLiveRelease();Render();}
        public static void RenderCityRelease() {RiggedReview.ValidateMotion();CharacterReview.Render();RenderAt("arcade-city");}
        [MenuItem("UltramanGame/Render full cinematic battle")]
        public static void Render()
        {RenderAt("cinematic-combat");}
        static void RenderAt(string outputFolder)
        {
            // The flash locality micro-review is useful as a separate GPU
            // diagnostic, but it must not gate the actual movie render: some
            // macOS driver paths quantize the tiny off-screen probe even
            // though the player compositor is healthy. Opt into that probe
            // explicitly so the default review always produces the real
            // deterministic battle frames.
            bool runFlash=Array.IndexOf(Environment.GetCommandLineArgs(),"--run-flash-review")>=0;
            if(runFlash&&SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null)CinematicFlashReview.Run();
            else Debug.Log("[CinematicReview] flash locality micro-review skipped; use --run-flash-review to run it separately");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(20260909);
            // The default review remains stable for regression checks. Optional
            // deterministic variants make the exported showcase cover the same
            // ground-slam, monster-ray and alternating-punch beats visible in
            // the arcade reference without changing player input rules.
            var args=Environment.GetCommandLineArgs();
            int outputAt=Array.IndexOf(args,"--review-output");
            if(outputAt>=0&&outputAt+1<args.Length)outputFolder=args[outputAt+1];
            bool slam=Array.IndexOf(args,"--review-slam")>=0;
            bool ray=Array.IndexOf(args,"--review-ray")>=0;
            bool linked=Array.IndexOf(args,"--review-linked")>=0;
            bool clawCycle=Array.IndexOf(args,"--review-claw-cycle")>=0;
            string extension=Array.IndexOf(args,"--review-jpeg")>=0?"jpg":"png";
            var world=new GameWorld();var battle=new Battle();var input=new ReviewPlayback(slam,ray,linked,clawCycle:clawCycle);
            world.UseFighterFraming=Array.IndexOf(args,"--review-baseline-framing")<0;
            var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts",outputFolder));Directory.CreateDirectory(folder+"/frames");
            File.Delete(folder+"/validation.txt");
            foreach(string suffix in new[]{"png","jpg"})
                foreach(var old in Directory.GetFiles(folder+"/frames","frame-????."+suffix))File.Delete(old);
            var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32){antiAliasing=4};target.Create();world.Camera.targetTexture=target;world.Camera.aspect=16/9f;
            var events=new StringBuilder("seconds,event,health,energy\n");int beams=0,output=0;bool paused=false;float victory=-1;
            float lastHealth=battle.EnemyHealth;
            for(int frame=0;frame<60*150;frame++)
            {
                float dt=1/60f,time=frame*dt;var command=input.Next(battle,dt);
                battle.Tick(world.BattleDelta(dt,battle),command);
                while(battle.TryCue(out var cue))
                {world.Cue(cue,battle);events.AppendLine($"{time:F3},{cue},{battle.EnemyHealth},{battle.Energy}");}
                if(battle.Phase==GamePhase.Paused)paused=true;
                hero.Update(battle,world.Camera,dt,time);enemy.Update(battle,world.Camera,dt,time);
                if(battle.EnemyHealth<lastHealth)
                {
                    world.Hit(lastHealth-battle.EnemyHealth>1,battle);
                    events.AppendLine($"{time:F3},HeroHit,{battle.EnemyHealth},{battle.Energy}");
                }
                lastHealth=battle.EnemyHealth;
                world.Tick(battle,dt,time);enemy.SetPresentationOpacity(world.EnemyOpacity);
                if(world.BeamStarted)
                {beams++;events.AppendLine($"{time:F3},BeamVisible,{battle.EnemyHealth},{battle.Energy}");}
                if(frame%2==0){CharacterReview.Save(world.Camera,target,$"{folder}/frames/frame-{output:0000}.{extension}");output++;}
                if(battle.Phase==GamePhase.Victory){if(victory<0)victory=time;if(time-victory>3)break;}
            }
            if(battle.Phase!=GamePhase.Victory||battle.Punches!=32||beams!=2||battle.HitsTaken!=1||battle.Blocks<(clawCycle?4:1)||!paused)
                throw new Exception($"Cinematic battle failed: phase={battle.Phase} punches={battle.Punches} beams={beams} blocks={battle.Blocks} hurt={battle.HitsTaken} paused={paused}");
            if(world.Camera.GetComponent<CinematicCamera>().RenderCount<output)throw new Exception("Post processing did not render");
            world.ResetPresentation();
            if(world.Closeup.Active||world.BeamVisible||world.EnemySlashVisible||world.ActiveSparkCount!=0)
                throw new Exception("Restart retained cinematic effects");
            File.WriteAllText(folder+"/events.csv",events.ToString());
            string report=$"[CinematicReview] passed frames={output} seconds={output/30f:F2} punches={battle.Punches} beams={beams} blocks={battle.Blocks} hurt={battle.HitsTaken} paused={paused}";
            File.WriteAllText(folder+"/validation.txt",report);Debug.Log(report);
            world.Camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
