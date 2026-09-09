using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class RiggedReview
    {
        // Exercises the imported asset and runtime sampler, without opening a camera.
        [MenuItem("UltramanGame/Review skeletal combat")]
        public static void Render()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var world=new GameWorld();var state=new Battle(26);
            var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);
            var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);
            if(!hero.IsRigged)throw new Exception("Tiga skeletal asset was not loaded");
            hero.Update(state,world.Camera,0,0,0);
            var bounds=BodyBounds(hero.Root);
            if(Mathf.Abs(bounds.size.y-3.45f)>.04f||Mathf.Abs(bounds.min.y)>.02f)
                throw new Exception("Imported character size/grounding failed: "+bounds);
            var wrist=Joint(hero.Root,"HandBase_R");var hip=Joint(hero.Root,"hip");
            var idle=wrist.position;var hipIdle=hip.position;
            hero.Update(state,world.Camera,0,0,2);var punch=wrist.position;
            hero.Update(state,world.Camera,0,0,4);var beam=wrist.position;
            hero.Update(state,world.Camera,0,0,3);var guard=wrist.position;
            if(Vector3.Distance(idle,punch)<.3f||beam.y-idle.y<.3f||Vector3.Distance(idle,guard)<.15f||Vector3.Distance(hipIdle,hip.position)<.025f)
                throw new Exception("Imported wrist/hip curves did not deform the combat poses");
            state.Tick(.02f,new PlayerInput {Tracking=true,Transform=true});Step(state,2.3f);
            for(int hit=0;hit<15;hit++)
            {state.Tick(.02f,new PlayerInput {Tracking=true,LeftPunch=true});Step(state,.5f);}
            for(int i=0;i<1500&&(state.Enemy!=EnemyPhase.Windup||state.EnemyAge<4.5f);i++)Step(state,1/30f);
            if(state.Enemy!=EnemyPhase.Windup||state.Energy!=15)throw new Exception("Review battle setup failed");
            while(state.TryCue(out _)){}
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/rigged-combat"));Directory.CreateDirectory(folder);
            var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);target.antiAliasing=4;target.Create();world.Camera.targetTexture=target;world.Camera.aspect=16/9f;
            world.Tick(state,1,0);int releases=0;var previousHand=idle;float maxHandStep=0;
            for(int frame=0;frame<300;frame++)
            {
                float t=frame/30f;
                var input=new PlayerInput {Tracking=true,Shield=t>.2f&&t<2.1f,LeftPunch=frame==72,RightPunch=frame==96,Beam=frame==138};
                state.Tick(world.Closeup.Active?0:1/30f,input);
                while(state.TryCue(out var cue))world.Cue(cue);
                world.Tick(state,1/30f,t);hero.Update(state,world.Camera,1/30f,t);enemy.Update(state,world.Camera,1/30f,t);enemy.SetPresentationOpacity(1-world.Closeup.Focus);
                if(world.BeamStarted)releases++;
                if(frame>1)maxHandStep=Mathf.Max(maxHandStep,Vector3.Distance(previousHand,wrist.position));previousHand=wrist.position;
                CharacterReview.Save(world.Camera,target,Path.Combine(folder,$"frame-{frame:D4}.png"));
            }
            if(state.Punches!=17||state.Blocks!=1||state.HitsTaken!=0||state.Phase!=GamePhase.Victory||releases!=1)
                throw new Exception($"Combat sequence failed: punches={state.Punches} blocks={state.Blocks} hits={state.HitsTaken} phase={state.Phase} beams={releases}");
            var raisedHand=world.Camera.WorldToViewportPoint(wrist.position);
            if(raisedHand.y>.9f||raisedHand.y<.5f)throw new Exception("Victory hand overlaps the top HUD or leaves the frame: "+raisedHand);
            if(maxHandStep>1)throw new Exception("Skeletal action transition jumped more than one metre in a frame");
            world.Camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);
            Debug.Log($"[RiggedReview] scale=passed grounding=passed wristAndHipMotion=passed continuousCombat=passed punches={state.Punches} blocks={state.Blocks} beamReleases={releases} victory=passed maxHandStep={maxHandStep:F3} output={folder}");
        }
        static Transform Joint(Transform root,string name)
        {foreach(var t in root.GetComponentsInChildren<Transform>())if(t.name==name)return t;throw new Exception("Missing joint "+name);}
        static Bounds BodyBounds(Transform root)
        {
            var skin=root.GetComponentInChildren<SkinnedMeshRenderer>();var mesh=new Mesh();skin.BakeMesh(mesh,true);var vertices=mesh.vertices;
            var bounds=new Bounds(skin.transform.TransformPoint(vertices[0]),Vector3.zero);
            foreach(var vertex in vertices)bounds.Encapsulate(skin.transform.TransformPoint(vertex));
            UnityEngine.Object.DestroyImmediate(mesh);return bounds;
        }
        static void Step(Battle state,float duration)
        {while(duration>.0001f){float dt=Mathf.Min(duration,.02f);state.Tick(dt,new PlayerInput {Tracking=true});duration-=dt;}}
    }
}
