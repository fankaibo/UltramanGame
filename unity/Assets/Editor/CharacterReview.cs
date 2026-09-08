using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class CharacterReview
    {
        // Uses the player shader, UV frames and arena; no webcam is opened or captured.
        [MenuItem("UltramanGame/Render character review")]
        public static void Render()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var world=new GameWorld();var state=new Battle();
            var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);
            var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);
            world.Tick(state,1,1);
            var camera=world.Camera;
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/animation-review"));Directory.CreateDirectory(folder);
            var target=new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32);target.antiAliasing=4;target.Create();camera.targetTexture=target;
            camera.aspect=target.width/(float)target.height;world.Tick(state,1,1);
            for(int frame=0;frame<8;frame++)
            {
                hero.Update(state,camera,0,0,frame);enemy.Update(state,camera,0,0,frame);
                if(hero.Frame!=frame||enemy.Frame!=frame)throw new Exception("Action atlas frame mismatch");
                Save(camera,target,Path.Combine(folder,"pose-"+frame+".png"));
            }
            state.Tick(.01f,new PlayerInput {Tracking=true,Transform=true});Step(state,2.3f);
            state.Tick(.06f,new PlayerInput {Tracking=true,LeftPunch=true});Step(state,.06f);
            RenderBattle(world,state,hero,enemy,target,folder,"battle-punch");
            var travel=hero.Root.position-world.HeroHome;
            if(travel.magnitude<1 || Vector3.Angle(travel,world.EnemyHome-world.HeroHome)>.1f)
                throw new Exception("Punch must travel toward Golza in both X and Z");
            Step(state,.4f);state.Tick(.01f,new PlayerInput {Tracking=true,Shield=true});
            RenderBattle(world,state,hero,enemy,target,folder,"battle-shield");
            for(int i=0;i<60&&state.Energy<Battle.MaxEnergy;i++)
            {state.Tick(.01f,new PlayerInput {Tracking=true,LeftPunch=true});Step(state,.4f);}
            state.Tick(.01f,new PlayerInput {Tracking=true,Beam=true});Step(state,.35f);
            if(state.Action!=HeroAction.Beam)throw new Exception("Review did not reach beam action");
            RenderBattle(world,state,hero,enemy,target,folder,"battle-beam");
            RenderTexture.active=null;camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);
            Debug.Log($"[CharacterReview] frames=16 battleViews=3 diagonalPunch=passed renderers=2 output={folder}");
        }
        static void Step(Battle state,float duration)
        {while(duration>.0001f) {float dt=Mathf.Min(duration,.02f);state.Tick(dt,new PlayerInput {Tracking=true});duration-=dt;}}
        static void RenderBattle(GameWorld world,Battle state,AnimatedActor hero,AnimatedActor enemy,RenderTexture target,string folder,string name)
        {
            world.Tick(state,.1f,1);hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);
            Save(world.Camera,target,Path.Combine(folder,name+".png"));
        }
        static void Save(Camera camera,RenderTexture target,string path)
        {
            camera.Render();RenderTexture.active=target;
            var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();
            File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
