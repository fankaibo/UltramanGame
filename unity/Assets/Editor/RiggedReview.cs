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
        public static void Render() => Run(false);
        public static void ValidateMotion() => Run(true);
        public static void ReviewLiveRelease(){Run(false);CharacterReview.Render();}
        static void Run(bool motionOnly)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var world=new GameWorld();var state=new Battle(26);
            var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);
            var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);
            world.BindActors(hero,enemy);
            if(!hero.IsRigged||!enemy.IsRigged)throw new Exception("Both skeletal assets must load");
            hero.Update(state,world.Camera,0,0,0);
            enemy.Update(state,world.Camera,0,0,0);
            var bounds=BodyBounds(hero.Root);
            if(Mathf.Abs(bounds.size.y-3.45f)>.04f||Mathf.Abs(bounds.min.y)>.02f)
                throw new Exception("Imported character size/grounding failed: "+bounds);
            var monsterBounds=BodyBounds(enemy.Root);
            if(Mathf.Abs(monsterBounds.size.y-3.6f)>.04f||Mathf.Abs(monsterBounds.min.y)>.03f)
                throw new Exception("Monster scale/grounding failed: "+monsterBounds);
            foreach(string side in new[]{"L","R"})
            {
                float shoulder=Mathf.Abs(enemy.Root.InverseTransformPoint(Joint(enemy.Root,"bip_upperArm_"+side).position).x);
                float elbow=Mathf.Abs(enemy.Root.InverseTransformPoint(Joint(enemy.Root,"bip_lowerArm_"+side).position).x);
                if(elbow<=shoulder+.025f)throw new Exception("Monster elbow folds into the chest: "+side);
            }
            var claw=Joint(enemy.Root,"bip_hand_R");var idleClaw=claw.position;
            enemy.Update(state,world.Camera,0,0,2);
            if(Vector3.Distance(idleClaw,claw.position)<.3f)throw new Exception("Monster claw clip did not deform the arm");
            enemy.Update(state,world.Camera,0,0,0);
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
            world.Tick(state,1,0);int releases=0;var previousHand=idle;float maxHandStep=0,minGround=0,maxMonsterFootLift=0;
            var monsterFoot=Joint(enemy.Root,"bip_foot_L");float footRest=monsterFoot.position.y;
            float health=state.EnemyHealth;
            for(int frame=0;frame<300;frame++)
            {
                float t=frame/30f;
                var input=new PlayerInput {Tracking=true,Shield=t>.2f&&t<2.1f,LeftPunch=frame==72,RightPunch=frame==96,Beam=frame==138};
                state.Tick(world.BattleDelta(1/30f,state),input);
                while(state.TryCue(out var cue))world.Cue(cue);
                if(state.EnemyHealth<health)world.Hit(health-state.EnemyHealth>1,state);
                health=state.EnemyHealth;
                hero.Update(state,world.Camera,1/30f,t);enemy.Update(state,world.Camera,1/30f,t);world.Tick(state,1/30f,t);enemy.SetPresentationOpacity(world.EnemyOpacity);
                if(world.BeamStarted)releases++;
                if(frame>1)maxHandStep=Mathf.Max(maxHandStep,Vector3.Distance(previousHand,wrist.position));previousHand=wrist.position;
                if(frame%3==0&&state.Phase==GamePhase.Battle)
                {
                    float h=BodyBounds(hero.Root).min.y,m=BodyBounds(enemy.Root).min.y;
                    if(Mathf.Min(h,m)<minGround-.01f)Debug.Log($"[GroundReview] frame={frame} hero={h:F3} monster={m:F3} action={state.Action} age={state.ActionAge:F3} enemy={state.Enemy} enemyAge={state.EnemyAge:F3}");
                    minGround=Mathf.Min(minGround,h,m);
                    maxMonsterFootLift=Mathf.Max(maxMonsterFootLift,monsterFoot.position.y-footRest);
                }
                if(!motionOnly||frame%90==0)CharacterReview.Save(world.Camera,target,Path.Combine(folder,$"frame-{frame:D4}.png"));
            }
            if(state.Punches!=17||state.Blocks!=1||state.HitsTaken!=0||state.Phase!=GamePhase.Victory||releases!=1)
                throw new Exception($"Combat sequence failed: punches={state.Punches} blocks={state.Blocks} hits={state.HitsTaken} phase={state.Phase} beams={releases}");
            var raisedHand=world.Camera.WorldToViewportPoint(wrist.position);
            if(raisedHand.y>.9f||raisedHand.y<.5f)throw new Exception("Victory hand overlaps the top HUD or leaves the frame: "+raisedHand);
            if(maxHandStep>1)throw new Exception("Skeletal action transition jumped more than one metre in a frame");
            if(minGround<-.12f)throw new Exception("Character foot penetrated the plaza: "+minGround);
            if(maxMonsterFootLift<.06f)throw new Exception("Monster rush did not lift its leading foot");
            if(world.Camera.GetComponent<ContactShadows>().RenderCount<(motionOnly?4:300))throw new Exception("Actor contact shadows did not render");
            world.Camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);
            Debug.Log($"[RiggedReview] bothActors=rigged scale=passed grounding=passed minGround={minGround:F3} monsterStep={maxMonsterFootLift:F3} wristAndHipMotion=passed continuousCombat=passed punches={state.Punches} blocks={state.Blocks} beamReleases={releases} victory=passed maxHandStep={maxHandStep:F3} shadows=passed output={folder}");
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
