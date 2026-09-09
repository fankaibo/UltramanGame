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
            world.BindActors(hero,enemy);
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
            if(Mathf.Abs(travel.magnitude-AnimatedActor.PunchAdvance)>.02f || Vector3.Angle(travel,world.EnemyHome-world.HeroHome)>.1f)
                throw new Exception("Punch must travel toward Golza in both X and Z");
            Step(state,.4f);state.Tick(.01f,new PlayerInput {Tracking=true,Shield=true});
            RenderBattle(world,state,hero,enemy,target,folder,"battle-shield");
            for(int i=0;i<60&&state.Energy<Battle.MaxEnergy;i++)
            {state.Tick(.01f,new PlayerInput {Tracking=true,LeftPunch=true});Step(state,.4f);}
            state.Tick(.01f,new PlayerInput {Tracking=true,Beam=true});Step(state,.35f);
            if(state.Action!=HeroAction.Beam)throw new Exception("Review did not reach beam action");
            RenderBattle(world,state,hero,enemy,target,folder,"battle-beam");
            RenderEnemy(world,hero,enemy,target,folder,"enemy-windup",EnemyPhase.Windup,1.8f);
            RenderEnemy(world,hero,enemy,target,folder,"enemy-approach",EnemyPhase.Attack,.25f);
            var contact=RenderEnemy(world,hero,enemy,target,folder,"enemy-contact",EnemyPhase.Attack,.42f);
            if(Mathf.Abs(Vector3.Dot(enemy.Root.position-world.EnemyHome,-world.BattleAxis)-AnimatedActor.EnemyAdvance)>.02f||contact.HitsTaken!=1||!world.EnemySlashVisible)
                throw new Exception("Enemy contact must combine forward movement, claw effect and one hit");
            var blocked=RenderEnemy(world,hero,enemy,target,folder,"enemy-blocked",EnemyPhase.Attack,.42f,true);
            if(blocked.Blocks!=1||blocked.HitsTaken!=0)throw new Exception("Enemy shield contact did not block");
            RenderEnemy(world,hero,enemy,target,folder,"enemy-retreat",EnemyPhase.Attack,.8f);
            RenderEnemy(world,hero,enemy,target,folder,"enemy-home",EnemyPhase.Recover,.2f);
            if(Vector3.Distance(enemy.Root.position,world.EnemyHome)>.001f||world.EnemySlashVisible)
                throw new Exception("Enemy must return home and clear claw effects");
            var interrupted=RenderEnemy(world,hero,enemy,target,folder,"enemy-before-pause",EnemyPhase.Attack,.3f);
            interrupted.Pause();RenderBattle(world,interrupted,hero,enemy,target,folder,"enemy-paused");
            if(world.EnemySlashVisible)throw new Exception("Pause must clear enemy attack effects");
            RenderCloseup(world,hero,enemy,target,folder);
            RenderTexture.active=null;camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);
            Debug.Log($"[CharacterReview] frames=16 battleViews=3 enemyViews=8 closeupViews=8 diagonalPunch=passed enemyContact=passed beamCloseup=passed renderers=2 output={folder}");
        }
        static Battle BeamReady()
        {
            var state=new Battle(20);state.Tick(.02f,new PlayerInput {Tracking=true,Transform=true});Step(state,2.3f);
            for(int attempt=0;attempt<80&&state.Energy<Battle.MaxEnergy;attempt++)
            {
                state.Tick(.02f,new PlayerInput {Tracking=true,LeftPunch=true});
                for(int i=0;i<25;i++)state.Tick(.02f,new PlayerInput {Tracking=true,Shield=true});
            }
            if(state.Energy<Battle.MaxEnergy)throw new Exception("Closeup review did not fill energy");
            while(state.TryCue(out _)){}return state;
        }
        static void RenderCloseup(GameWorld world,AnimatedActor hero,AnimatedActor enemy,RenderTexture target,string folder)
        {
            var state=BeamReady();float health=state.EnemyHealth;
            world.Tick(state,1,0);float wideFov=world.Camera.fieldOfView;
            state.Tick(.02f,new PlayerInput {Tracking=true,Beam=true});
            while(state.TryCue(out var cue))world.Cue(cue);
            int[] captures={5,18,42,54,62,76,90};int capture=0,beamStarts=0;
            for(int frame=0;frame<=100;frame++)
            {
                state.Tick(world.Closeup.Active?0:1/60f,new PlayerInput {Tracking=true});
                hero.Update(state,world.Camera,1/60f,frame/60f);enemy.Update(state,world.Camera,1/60f,frame/60f);
                world.Tick(state,1/60f,frame/60f);
                enemy.SetPresentationOpacity(1-world.Closeup.Focus);
                if(world.BeamStarted)beamStarts++;
                if(world.Closeup.Active&&state.EnemyHealth!=health)throw new Exception("Damage occurred during closeup");
                if(frame==18)
                {
                    float zoom=Mathf.Tan(wideFov*Mathf.Deg2Rad/2)/Mathf.Tan(world.Camera.fieldOfView*Mathf.Deg2Rad/2);
                    if(zoom<1.8f||hero.Frame!=4)throw new Exception("Closeup must enlarge the beam illustration");
                    foreach(float height in new[]{2.6f,3.3f})
                    {
                        var p=world.Camera.WorldToViewportPoint(world.HeroHome+Vector3.up*height);
                        if(p.x<.15f||p.x>.85f||p.y<.15f||p.y>.85f)throw new Exception("Beam head/hands leave the closeup frame");
                    }
                }
                if(capture<captures.Length&&frame==captures[capture])
                {Save(world.Camera,target,Path.Combine(folder,"beam-closeup-"+capture+".png"));capture++;}
            }
            if(beamStarts!=1||state.Phase!=GamePhase.Victory||world.Closeup.Active||world.Camera.fieldOfView<25)
                throw new Exception("Closeup must return to the arena, emit once and allow victory");
            state=BeamReady();state.Tick(.02f,new PlayerInput {Tracking=true,Beam=true});
            while(state.TryCue(out var cue))world.Cue(cue);
            world.Tick(state,.1f,1);state.Pause();world.Tick(state,.02f,1.02f);
            hero.Update(state,world.Camera,0,1);enemy.Update(state,world.Camera,0,1);
            Save(world.Camera,target,Path.Combine(folder,"beam-closeup-paused.png"));
            if(world.Closeup.Active||world.Closeup.Focus!=0||world.Camera.fieldOfView<25||world.BeamStarted)
                throw new Exception("Pause must immediately restore the normal camera without releasing a beam");
        }
        static Battle RenderEnemy(GameWorld world,AnimatedActor hero,AnimatedActor enemy,RenderTexture target,string folder,string name,EnemyPhase phase,float age,bool shield=false)
        {
            var state=new Battle();state.Tick(.02f,new PlayerInput {Tracking=true,Transform=true});Step(state,2.3f);
            for(int i=0;i<1000&&state.Enemy!=phase;i++)state.Tick(.02f,new PlayerInput {Tracking=true,Shield=shield});
            if(state.Enemy!=phase)throw new Exception("Enemy review phase not reached");
            while(age>.0001f) {float dt=Mathf.Min(age,.02f);state.Tick(dt,new PlayerInput {Tracking=true,Shield=shield});age-=dt;}
            world.Tick(state,1,1);
            while(state.TryCue(out var cue))
                if(cue==GameCue.EnemyAttack||cue==GameCue.Hurt||cue==GameCue.Block)world.Cue(cue);
            hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,0,1);
            Save(world.Camera,target,Path.Combine(folder,name+".png"));return state;
        }
        static void Step(Battle state,float duration)
        {while(duration>.0001f) {float dt=Mathf.Min(duration,.02f);state.Tick(dt,new PlayerInput {Tracking=true});duration-=dt;}}
        static void RenderBattle(GameWorld world,Battle state,AnimatedActor hero,AnimatedActor enemy,RenderTexture target,string folder,string name)
        {
            // Review the final framing, rather than a single frame partway through the camera transition.
            hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,1,1);
            Save(world.Camera,target,Path.Combine(folder,name+".png"));
        }
        internal static void Save(Camera camera,RenderTexture target,string path,System.Action beforeRender=null)
        {
            // Batch rendering runs without the player loop that refreshes GPU skinning.
            // Snapshot the current deformed meshes so review frames match sampled bones.
            var skins=UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None);
            var snapshots=new System.Collections.Generic.List<GameObject>();
            foreach(var skin in skins)
            {
                if(!skin.enabled)continue;
                var mesh=new Mesh();skin.BakeMesh(mesh,true);
                var snapshot=new GameObject("Skin review snapshot");snapshot.transform.SetParent(skin.transform,false);
                snapshot.layer=skin.gameObject.layer;
                snapshot.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=snapshot.AddComponent<MeshRenderer>();renderer.sharedMaterials=skin.sharedMaterials;
                renderer.shadowCastingMode=skin.shadowCastingMode;renderer.receiveShadows=skin.receiveShadows;
                snapshots.Add(snapshot);skin.enabled=false;
            }
            try {beforeRender?.Invoke();camera.Render();}
            finally
            {
                foreach(var snapshot in snapshots)
                {
                    snapshot.transform.parent.GetComponent<SkinnedMeshRenderer>().enabled=true;
                    UnityEngine.Object.DestroyImmediate(snapshot.GetComponent<MeshFilter>().sharedMesh);
                    UnityEngine.Object.DestroyImmediate(snapshot);
                }
            }
            RenderTexture.active=target;
            var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();
            File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
