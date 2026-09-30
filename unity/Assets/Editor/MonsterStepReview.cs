using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class MonsterStepReview
    {
        public static void Before()=>Run("before");
        public static void After(){Run("after");Interruptions();}
        public static void Release(){After();ExchangeReview.After();RiggedReview.ValidateMotion();}
        static Transform Bone(Transform root,string name)
        {foreach(var b in root.GetComponentsInChildren<Transform>())if(b.name==name)return b;throw new Exception(name);}
        static void Interruptions()
        {
            foreach(float age in new[]{.16f,.40f,.80f})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var world=new GameWorld();var state=new Battle();var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);
                state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
                for(int i=0;i<1000;i++)
                {
                    state.Tick(.02f,new PlayerInput{Tracking=true});enemy.Update(state,world.Camera,.02f,i*.02f);
                    if(state.Enemy==EnemyPhase.Attack&&state.EnemyAge>=age)break;
                }
                if(state.Enemy!=EnemyPhase.Attack)throw new Exception("Missing rush interruption");
                state.Pause();enemy.Update(state,world.Camera,.02f,30);
                if(Mathf.Abs(enemy.Root.position.y-world.EnemyHome.y)>.001f)throw new Exception("Paused rush retained hip drop");
                var waiting=new Battle();enemy.Update(waiting,world.Camera,.02f,31);
                if(Vector3.Distance(enemy.Root.position,world.EnemyHome)>.001f)throw new Exception("Restart retained rush offset");
            }
            Debug.Log("[MonsterStepInterruptions] approach/contact/recovery pause and reset passed");
        }
        static void Run(string version)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var world=new GameWorld();var state=new Battle();
            var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);
            var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            Transform[] hands={Bone(enemy.Root,"bip_hand_L"),Bone(enemy.Root,"bip_hand_R")},forearms={Bone(enemy.Root,"bip_lowerArm_L"),Bone(enemy.Root,"bip_lowerArm_R")};
            var fingers=new Transform[2,4];string[] fingerNames={"index","middle","ring","pinky"};
            for(int s=0;s<2;s++)for(int f=0;f<4;f++)fingers[s,f]=Bone(enemy.Root,"bip_"+fingerNames[f]+"_0_"+(s==0?"L":"R"));
            Vector3 leftHome=enemy.FootPosition(true),rightHome=enemy.FootPosition(false);
            state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
            for(int i=0;i<(Battle.TransformationSeconds+0.4f)/(.02f);i++)state.Tick(.02f,new PlayerInput{Tracking=true});
            while(state.TryCue(out _)){}
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/monster-step",version));Directory.CreateDirectory(folder);
            File.Delete(folder+"/validation.txt");
            var sources=new StringBuilder("UTC: "+DateTime.UtcNow.ToString("O")+"\n");
            using(var sha=System.Security.Cryptography.SHA256.Create())
                foreach(string file in new[]{"Scripts/Runtime/RiggedActor.cs","Scripts/Runtime/CombatVfx.cs","Scripts/Core/MonsterStepMotion.cs","Editor/MonsterStepReview.cs"})
                {string p=Path.Combine(Application.dataPath,file);if(File.Exists(p))sources.AppendLine(file+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant());}
            File.WriteAllText(folder+"/render-source.txt",sources.ToString());
            var csv=new StringBuilder("frame,attack,phase,age,leftX,leftY,leftZ,rightX,rightY,rightZ,clawX,clawY,clawZ\n");
            var mesh=new Mesh();var target=new RenderTexture(1280,720,24){antiAliasing=4};target.Create();world.Camera.targetTexture=target;world.Camera.aspect=16f/9;
            float[] support={0,0},lift={0,0};float minGround=100,maxWrist=0,maxHandStep=0;Vector3 priorClaw=default;int contacts=0,shots=0;bool contact=false;string lowest="";
            try
            {
                for(int frame=0;frame<1500;frame++)
                {
                    const float dt=1/60f;float time=frame*dt;
                    state.Tick(dt,new PlayerInput{Tracking=true,Shield=true});
                    while(state.TryCue(out var cue))world.Cue(cue,state);
                    hero.Update(state,world.Camera,dt,time);enemy.Update(state,world.Camera,dt,time);world.Tick(state,dt,time);
                    var left=enemy.FootPosition(true);var right=enemy.FootPosition(false);var claw=enemy.EnemyStrikeOrigin(state);
                    for(int s=0;s<2;s++)
                    {Vector3 center=Vector3.zero;for(int f=0;f<4;f++)center+=fingers[s,f].position;center/=4;maxWrist=Mathf.Max(maxWrist,Vector3.Angle(center-hands[s].position,hands[s].position-forearms[s].position));}
                    if(frame>0&&state.Enemy==EnemyPhase.Attack&&state.EnemyAge>.01f)maxHandStep=Mathf.Max(maxHandStep,Vector3.Distance(claw,priorClaw));priorClaw=claw;
                    csv.AppendLine(FormattableString.Invariant($"{frame},{state.EnemyAttackCount},{state.Enemy},{state.EnemyAge:F5},{left.x:F5},{left.y:F5},{left.z:F5},{right.x:F5},{right.y:F5},{right.z:F5},{claw.x:F5},{claw.y:F5},{claw.z:F5}"));
                    if(state.Enemy==EnemyPhase.Attack)
                    {
                        int at=state.EnemyAttackCount-1;bool leadLeft=state.EnemyAttackCount%2!=0;
                        if(at<2)
                        {
                            Vector3 supportFoot=leadLeft?right:left,anchor=leadLeft?rightHome:leftHome;
                            support[at]=Mathf.Max(support[at],Vector3.ProjectOnPlane(supportFoot-anchor,Vector3.up).magnitude);
                            lift[at]=Mathf.Max(lift[at],(leadLeft?left.y-leftHome.y:right.y-rightHome.y));
                        }
                        if(!contact&&state.EnemyAge>=Battle.EnemyHitSeconds){contact=true;contacts++;CharacterReview.Save(world.Camera,target,folder+"/contact-"+contacts+".png");}
                        int shot=(int)(state.EnemyAge/.2f);if(shot>shots){shots=shot;CharacterReview.Save(world.Camera,target,folder+"/attack-"+state.EnemyAttackCount+"-"+shot+".png");}
                    }
                    else {contact=false;shots=0;}
                    if(frame%3==0&&state.Enemy!=EnemyPhase.Rest)
                        foreach(var skin in enemy.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {skin.BakeMesh(mesh,true);foreach(var v in mesh.vertices){var p=skin.transform.TransformPoint(v);if(p.y<minGround){minGround=p.y;float near=100;string bone="";foreach(var b in skin.bones){float d=Vector3.Distance(p,b.position);if(d<near){near=d;bone=b.name;}}lowest=$"{state.Enemy}/{state.EnemyAge:F3} {bone} {p}";}}}
                    if(state.EnemyAttackCount==2&&state.Enemy==EnemyPhase.Rest)break;
                }
                string result=$"{version}: contacts={contacts} supportDrift={support[0]:F4}/{support[1]:F4} leadLift={lift[0]:F4}/{lift[1]:F4} minGround={minGround:F4} maxWristBend={maxWrist:F2} maxHandStep={maxHandStep:F4} lowest={lowest}";
                File.WriteAllText(folder+"/motion.csv",csv.ToString());Debug.Log("[MonsterStepReview] "+result);
                if(contacts!=2)throw new Exception("Missing alternating rushes");
                if(version=="after"&&(support[0]>.035f||support[1]>.035f||lift[0]<.10f||lift[1]<.10f||minGround<-.035f||maxWrist>36||maxHandStep>.70f))throw new Exception("Rush footing failed: "+result);
                File.WriteAllText(folder+"/validation.txt",result);
            }
            finally{world.Camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(mesh);}
        }
    }
}
