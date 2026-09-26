using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class StaggerReview
    {
        public static void Before()=>Render("before");
        public static void After(){Render("after");Interruptions();RapidCombo();}
        public static void CheckRecovery(){Interruptions();RapidCombo();}
        public static void Release(){After();ExchangeReview.After();ContactReactionReview.After();MonsterStepReview.After();RiggedReview.ValidateMotion();}
        static Transform Bone(Transform root,string name)
        {foreach(var b in root.GetComponentsInChildren<Transform>())if(b.name==name)return b;throw new Exception(name);}
        static Battle Ready()
        {
            var state=new Battle();state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
            for(int i=0;i<120;i++)state.Tick(.02f,new PlayerInput{Tracking=true});
            state.GiveInstructionTime(20);while(state.TryCue(out _)){}return state;
        }
        static void Interruptions()
        {
            foreach(int fps in new[]{15,30,60})foreach(float stop in new[]{.08f,.25f,.45f})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var world=new GameWorld();var state=Ready();var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);
                float dt=1f/fps,t=0,age=-1,health=state.EnemyHealth;enemy.Update(state,world.Camera,0,t);
                var foot=enemy.FootPosition(true);var head=Bone(enemy.Root,"bip_head");
                while(age<stop)
                {
                    state.Tick(dt,new PlayerInput{Tracking=true,LeftPunch=t==0});enemy.Update(state,world.Camera,dt,t);
                    if(state.EnemyHealth<health)age=0;else if(age>=0)age+=dt;health=state.EnemyHealth;t+=dt;
                    if(t>2)throw new Exception("No test contact");
                }
                var h=head.rotation;var f=enemy.FootPosition(true);var p=enemy.Root.position;
                for(int i=0;i<5;i++)enemy.Update(state,world.Camera,0,t-dt);
                if(Quaternion.Angle(h,head.rotation)>.02f||Vector3.Distance(f,enemy.FootPosition(true))>.001f||Vector3.Distance(p,enemy.Root.position)>.001f)
                    throw new Exception("Recoil correction feeds back into repeated sampling");
                state.Pause();enemy.Update(state,world.Camera,dt,t);
                if(Vector3.Distance(enemy.Root.position,world.EnemyHome)>.001f)throw new Exception("Paused recoil retained root displacement");
                while(state.Phase==GamePhase.Paused){state.Tick(dt,new PlayerInput{Tracking=true});enemy.Update(state,world.Camera,dt,t);t+=dt;}
                var reference=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);reference.Update(state,world.Camera,0,t-dt);
                // Standing knees may lower the hips to preserve leg reach.
                // Compare with a fresh idle actor at the same animation sample,
                // rather than treating that valid vertical offset as recoil.
                if(Vector3.Distance(enemy.Root.position,reference.Root.position)>.001f||Quaternion.Angle(head.rotation,Bone(reference.Root,"bip_head").rotation)>.02f)
                    throw new Exception($"Resume differs from fresh idle: root={enemy.Root.position} reference={reference.Root.position} fps={fps} stop={stop}");
                var fresh=new Battle();enemy.Update(fresh,world.Camera,dt,t);
                if(Vector3.Distance(enemy.Root.position,world.EnemyHome)>.001f)throw new Exception("New round retained recoil");
            }
            Debug.Log("[StaggerInterruptions] 15/30/60Hz contact/sustain/recover repeated sampling, pause, resume and new round passed");
        }
        static void RapidCombo()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var world=new GameWorld();var state=Ready();var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);
            enemy.Update(state,world.Camera,0,0);float health=state.EnemyHealth,maxJump=0,maxHeadStep=0;int contacts=0;
            var head=Bone(enemy.Root,"bip_head");var oldHead=head.position;
            for(int i=0;i<300;i++)
            {
                var oldRoot=enemy.Root.position;
                state.Tick(1/60f,new PlayerInput{Tracking=true,LeftPunch=i%36==0,RightPunch=i%36==18});
                enemy.Update(state,world.Camera,1/60f,i/60f);
                if(state.EnemyHealth<health){contacts++;maxJump=Mathf.Max(maxJump,Vector3.ProjectOnPlane(enemy.Root.position-oldRoot,Vector3.up).magnitude);}
                maxHeadStep=Mathf.Max(maxHeadStep,Vector3.Distance(head.position,oldHead));oldHead=head.position;health=state.EnemyHealth;
            }
            if(contacts<10||maxJump>.035f||maxHeadStep>.45f)throw new Exception($"Rapid combo discontinuity: contacts={contacts} rootJump={maxJump} headStep={maxHeadStep}");
            Debug.Log($"[StaggerRapid] contacts={contacts} contactRootJump={maxJump:F5} maxHeadStep={maxHeadStep:F4}");
        }
        static void Render(string version)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(270927);
            var world=new GameWorld();var state=Ready();var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);
            var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,1,0);
            Vector3 leftHome=enemy.FootPosition(true),rightHome=enemy.FootPosition(false);
            var head=Bone(enemy.Root,"bip_head");Vector3 oldHead=head.position;
            var hands=new[]{Bone(enemy.Root,"bip_hand_L"),Bone(enemy.Root,"bip_hand_R")};
            var arms=new[]{Bone(enemy.Root,"bip_lowerArm_L"),Bone(enemy.Root,"bip_lowerArm_R")};
            var fingers=new Transform[2,4];string[] names={"index","middle","ring","pinky"};
            for(int s=0;s<2;s++)for(int f=0;f<4;f++)fingers[s,f]=Bone(enemy.Root,"bip_"+names[f]+"_0_"+(s==0?"L":"R"));
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/stagger",version));Directory.CreateDirectory(folder+"/frames");File.Delete(folder+"/validation.txt");
            var source=new StringBuilder("UTC: "+DateTime.UtcNow.ToString("O")+"\n");
            using(var sha=System.Security.Cryptography.SHA256.Create())
                foreach(string file in new[]{"Scripts/Runtime/RiggedActor.cs","Scripts/Core/MonsterRecoilMotion.cs","Scripts/Runtime/GameWorld.cs","Editor/StaggerReview.cs"})
                {string p=Path.Combine(Application.dataPath,file);if(File.Exists(p))source.AppendLine(file+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant());}
            File.WriteAllText(folder+"/render-source.txt",source.ToString());
            var target=new RenderTexture(1280,720,24){antiAliasing=4};target.Create();world.Camera.targetTexture=target;world.Camera.aspect=16f/9;
            var mesh=new Mesh();float health=state.EnemyHealth,footSlide=0,footLift=0,headStep=0,back=0,minGround=100,wrist=0;int contacts=0;float hitAge=10;var saved=new bool[3];
            var csv=new StringBuilder("frame,health,energy,action,actionAge,rootX,rootY,rootZ,leftX,leftY,leftZ,rightX,rightY,rightZ\n");
            try
            {
                for(int frame=0;frame<360;frame++)
                {
                    const float dt=1/60f;float t=frame*dt;hitAge+=dt;
                    state.Tick(world.BattleDelta(dt,state),new PlayerInput{Tracking=true,LeftPunch=frame==12||frame==84,RightPunch=frame==48||frame==160});
                    while(state.TryCue(out var cue))world.Cue(cue,state);
                    hero.Update(state,world.Camera,dt,t);enemy.Update(state,world.Camera,dt,t);
                    if(state.EnemyHealth<health){contacts++;hitAge=0;saved=new bool[3];world.Hit(false,state);}health=state.EnemyHealth;
                    world.Tick(state,dt,t);
                    var left=enemy.FootPosition(true);var right=enemy.FootPosition(false);var p=enemy.Root.position;
                    footSlide=Mathf.Max(footSlide,Vector3.ProjectOnPlane(left-leftHome,Vector3.up).magnitude,Vector3.ProjectOnPlane(right-rightHome,Vector3.up).magnitude);
                    footLift=Mathf.Max(footLift,Mathf.Abs(left.y-leftHome.y),Mathf.Abs(right.y-rightHome.y));
                    back=Mathf.Max(back,Vector3.Dot(p-world.EnemyHome,world.BattleAxis));headStep=Mathf.Max(headStep,Vector3.Distance(head.position,oldHead));oldHead=head.position;
                    for(int s=0;s<2;s++){Vector3 center=Vector3.zero;for(int f=0;f<4;f++)center+=fingers[s,f].position;center/=4;wrist=Mathf.Max(wrist,Vector3.Angle(center-hands[s].position,hands[s].position-arms[s].position));}
                    if(frame%3==0)foreach(var skin in enemy.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {skin.BakeMesh(mesh,true);foreach(var v in mesh.vertices)minGround=Mathf.Min(minGround,skin.transform.TransformPoint(v).y);}
                    csv.AppendLine(FormattableString.Invariant($"{frame},{health},{state.Energy},{state.Action},{state.ActionAge:F5},{p.x:F5},{p.y:F5},{p.z:F5},{left.x:F5},{left.y:F5},{left.z:F5},{right.x:F5},{right.y:F5},{right.z:F5}"));
                    if(frame%2==0)CharacterReview.Save(world.Camera,target,$"{folder}/frames/{frame/2:D4}.png");
                    for(int i=0;i<3;i++)if(contacts>0&&!saved[i]&&hitAge>=new[]{.08f,.22f,.48f}[i]){saved[i]=true;CharacterReview.Save(world.Camera,target,$"{folder}/contact-{contacts}-{i}.png");}
                }
                string result=$"{version}: contacts={contacts} punches={state.Punches} health={state.EnemyHealth} energy={state.Energy} footSlide={footSlide:F4} footLift={footLift:F4} rootBack={back:F4} minGround={minGround:F4} maxHeadStep={headStep:F4} maxWrist={wrist:F2} frames=180 duration=6";
                File.WriteAllText(folder+"/motion.csv",csv.ToString());Debug.Log("[StaggerReview] "+result);
                if(contacts!=4||state.Punches!=4||state.EnemyHealth!=46||state.Energy!=4)throw new Exception("Invalid combo sequence");
                if(version=="after"&&(footSlide>.035f||footLift>.035f||minGround<-.035f||headStep>.45f||wrist>36||back<.18f))throw new Exception("Stagger motion failed: "+result);
                File.WriteAllText(folder+"/validation.txt",result);
            }
            finally{world.Camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(mesh);}
        }
    }
}
