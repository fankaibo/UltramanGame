using System;
using System.IO;
using UnityEngine;
using UnityEditor.SceneManagement;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class UpgradeVisualReview
    {
        public static void Render()
        {
            RiggedReview.ValidateMotion();CharacterReview.Render();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(1509);
            var world=new GameWorld();var state=new Battle();
            var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            state.Tick(.01f,new PlayerInput{Tracking=true,Transform=true});
            for(int i=0;i<4000;i++)
            {state.Tick(.02f,new PlayerInput{Tracking=true});if(state.Enemy==EnemyPhase.Windup&&state.EnemyAge>state.WarningDuration-1.0f)break;}
            while(state.TryCue(out _)){}
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/upgrade-review"));Directory.CreateDirectory(folder+"/frames");
            var target=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32){antiAliasing=4};target.Create();world.Camera.targetTexture=target;world.Camera.aspect=16f/9;
            float health=state.EnemyHealth;
            for(int frame=0;frame<360;frame++)
            {
                float time=frame/60f;state.Tick(world.BattleDelta(1/60f,state),new PlayerInput{Tracking=true,Shield=time<2,LeftPunch=frame==180,RightPunch=frame==228});
                while(state.TryCue(out var cue))world.Cue(cue);
                hero.Update(state,world.Camera,1/60f,time);enemy.Update(state,world.Camera,1/60f,time);
                if(state.EnemyHealth<health)world.Hit(false,state);health=state.EnemyHealth;
                world.Tick(state,1/60f,time);enemy.SetPresentationOpacity(world.EnemyOpacity);
                CharacterReview.Save(world.Camera,target,$"{folder}/frames/frame-{frame:D4}.png");
            }
            world.Camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);
            Debug.Log("[UpgradeReview] 1920x1080 frames=360 fps=60 independentClaws=verified smokeAndBallisticLava=rendered");
        }
    }
}
