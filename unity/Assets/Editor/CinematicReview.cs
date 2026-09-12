using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(20260909);
            var world=new GameWorld();var battle=new Battle();var input=new ReviewPlayback();
            var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts",outputFolder));Directory.CreateDirectory(folder+"/frames");
            foreach(var old in Directory.GetFiles(folder+"/frames","frame-????.png"))File.Delete(old);
            var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32){antiAliasing=4};target.Create();world.Camera.targetTexture=target;world.Camera.aspect=16/9f;
            var events=new StringBuilder("seconds,event,health,energy\n");int beams=0,output=0;bool paused=false;float victory=-1;
            float lastHealth=battle.EnemyHealth;
            for(int frame=0;frame<60*150;frame++)
            {
                float dt=1/60f,time=frame*dt;var command=input.Next(battle,dt);
                battle.Tick(world.BattleDelta(dt,battle),command);
                while(battle.TryCue(out var cue))
                {world.Cue(cue);events.AppendLine($"{time:F3},{cue},{battle.EnemyHealth},{battle.Energy}");}
                if(battle.Phase==GamePhase.Paused)paused=true;
                hero.Update(battle,world.Camera,dt,time);enemy.Update(battle,world.Camera,dt,time);
                if(battle.EnemyHealth<lastHealth)world.Hit(lastHealth-battle.EnemyHealth>1,battle);lastHealth=battle.EnemyHealth;
                world.Tick(battle,dt,time);enemy.SetPresentationOpacity(world.EnemyOpacity);
                if(world.BeamStarted)beams++;
                if(frame%2==0){CharacterReview.Save(world.Camera,target,$"{folder}/frames/frame-{output:0000}.png");output++;}
                if(battle.Phase==GamePhase.Victory){if(victory<0)victory=time;if(time-victory>3)break;}
            }
            if(battle.Phase!=GamePhase.Victory||battle.Punches!=32||beams!=2||battle.HitsTaken!=1||battle.Blocks<1||!paused)
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
