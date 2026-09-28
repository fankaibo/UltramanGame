using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class MonsterSlamReview
    {
        public static void Before()=>Render("before");
        public static void After()=>Render("after");
        public static void Quick()=>Render("diagnostic",false);
        public static void Release(){Checks();After();}
        public static void Checks()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/monster-slam"));Directory.CreateDirectory(folder);
            File.Delete(folder+"/checks.txt");var report=new StringBuilder();
            foreach(int rate in new[]{15,30,60})foreach(string mode in new[]{"guard","miss","late-guide","punch","pause","restart","repeat"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var world=new GameWorld();var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                var state=new Battle(80);state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
                var joints=enemy.Root.GetComponentsInChildren<Transform>();var positions=new Vector3[joints.Length];
                Vector3 leftHome=enemy.FootPosition(true),rightHome=enemy.FootPosition(false),priorLeft=Vector3.zero,priorRight=Vector3.zero;
                var mesh=new Mesh();float dt=1f/rate,zeroError=0,bend=0,drift=0,bottom=100,speed=0;bool acted=false,priorActive=false;
                int slams=0,blocks=0,hurt=0;string worst="";
                try
                {
                    int finalAttack=mode=="repeat"?6:3;
                    for(int f=0;f<rate*110;f++)
                    {
                        bool attack=state.EnemyAttackCount==3&&state.Enemy==EnemyPhase.Attack;
                        bool finalWarning=state.EnemyAttackCount==2&&state.Enemy==EnemyPhase.Windup&&state.WarningDuration-state.EnemyAge<.3f;
                        bool punch=false;
                        if(!acted&&mode=="late-guide"&&finalWarning){state.GiveInstructionTime(1,true);acted=true;}
                        if(!acted&&mode=="punch"&&finalWarning){punch=true;acted=true;}
                        if(!acted&&(mode=="pause"||mode=="restart")&&attack&&state.EnemyAge>=.32f)
                        {
                            blocks=state.Blocks;hurt=state.HitsTaken;
                            if(mode=="pause")state.Pause();else state=new Battle(80);
                            world.ResetPresentation();hero.Update(state,world.Camera,dt,f*dt);enemy.Update(state,world.Camera,dt,f*dt);world.Tick(state,dt,f*dt);acted=true;
                            if(Vector3.Distance(enemy.Root.position,world.EnemyHome)>.001f||world.ActiveGroundStones!=0||world.ActiveGroundDust!=0)throw new Exception("Cancelled slam retained crouch or debris");
                            break;
                        }
                        float health=state.EnemyHealth;
                        state.Tick(world.BattleDelta(dt,state),new PlayerInput{Tracking=true,Shield=mode!="miss"&&!punch,LeftPunch=punch});
                        while(state.TryCue(out var cue))world.Cue(cue,state);
                        hero.Update(state,world.Camera,dt,f*dt);enemy.Update(state,world.Camera,dt,f*dt);
                        if(state.EnemyHealth<health)world.Hit(false,state);
                        int before=world.GroundContactCount;world.Tick(state,dt,f*dt);
                        if(world.GroundContactCount>before&&world.GroundContactCause=="slam")slams++;
                        bool active=MonsterSlamMotion.Active(state);
                        if(active)
                        {
                            foreach(string side in new[]{"L","R"})
                            {
                                Vector3 center=Vector3.zero;foreach(string finger in new[]{"index","middle","ring","pinky"})center+=Bone(enemy,"bip_"+finger+"_0_"+side).position;
                                var wrist=Bone(enemy,"bip_hand_"+side);var lower=Bone(enemy,"bip_lowerArm_"+side);
                                bend=Mathf.Max(bend,Vector3.Angle(center/4-wrist.position,wrist.position-lower.position));
                            }
                            if(mode!="punch")drift=Mathf.Max(drift,Vector3.Distance(leftHome,enemy.FootPosition(true)),Vector3.Distance(rightHome,enemy.FootPosition(false)));
                            if(priorActive)speed=Mathf.Max(speed,Vector3.Distance(priorLeft,Bone(enemy,"bip_hand_L").position)/dt,Vector3.Distance(priorRight,Bone(enemy,"bip_hand_R").position)/dt);
                            if(f%3==0)foreach(var skin in enemy.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                            {skin.BakeMesh(mesh,true);foreach(var v in mesh.vertices)bottom=Mathf.Min(bottom,skin.transform.TransformPoint(v).y);}
                            for(int i=0;i<joints.Length;i++)positions[i]=joints[i].position;
                            enemy.Update(state,world.Camera,0,f*dt);
                            for(int i=0;i<joints.Length;i++)
                            {
                                float error=Vector3.Distance(positions[i],joints[i].position);
                                if(float.IsNaN(error)||float.IsInfinity(error))throw new Exception("Invalid slam pose");
                                if(error>zeroError){zeroError=error;worst=joints[i].name+"/"+state.Enemy+"/"+state.EnemyAge.ToString("F3");}
                            }
                        }
                        priorLeft=Bone(enemy,"bip_hand_L").position;priorRight=Bone(enemy,"bip_hand_R").position;priorActive=active;
                        blocks=state.Blocks;hurt=state.HitsTaken;
                        if(state.EnemyAttackCount==finalAttack&&state.Enemy==EnemyPhase.Rest)break;
                    }
                    bool interrupt=mode=="pause"||mode=="restart";
                    string result=$"{rate}Hz {mode} slams={slams} blocks={blocks} hurt={hurt} zeroTime={zeroError:F6} worst={worst} wrist={bend:F3} feetDrift={drift:F5} minGround={bottom:F5} handSpeed={speed:F3} intervention={acted}";
                    Debug.Log("[SlamChecks] "+result);
                    if(slams!=(mode=="repeat"?2:1)||zeroError>.0001f||bend>36||drift>.035f||bottom<-.035f||speed>26||
                        !interrupt&&(blocks!=(mode=="miss"?0:finalAttack)||hurt!=(mode=="miss"?3:0))||mode!="guard"&&mode!="miss"&&mode!="repeat"&&!acted||mode=="punch"&&state.Punches!=1)
                        throw new Exception("Slam flow failed: "+result);
                    report.AppendLine(result+" passed");
                }
                finally{UnityEngine.Object.DestroyImmediate(mesh);}
            }
            // The shock front starts at the claws, reaches the defender 120 ms
            // later, and cannot reappear after a reset with pending lanes.
            foreach(int rate in new[]{15,30,60})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var root=new GameObject("Slam wave inspection");var camera=new GameObject("Camera").AddComponent<Camera>();var fx=new GroundImpact(root.transform);
                fx.Burst(Vector3.zero,Vector3.right,"slam",2);fx.Tick(camera,0);
                if(fx.ActiveStones!=8||fx.ActiveClouds!=4)throw new Exception("Wave did not start with its nearest lane");
                int initial=root.GetComponentsInChildren<Transform>(true).Length;float age=0,minimum=100;
                while(age<.16f){float step=Mathf.Min(1f/rate,.16f-age);fx.Tick(camera,step);age+=step;}
                if(fx.ActiveStones!=48||fx.ActiveClouds!=24)throw new Exception("Wave did not reach the defender");
                var filters=root.GetComponentsInChildren<MeshFilter>(true);
                while(age<2)
                {fx.Tick(camera,1f/rate);age+=1f/rate;foreach(var filter in filters)if(filter.gameObject.activeSelf&&filter.name=="Ground basalt fragment")foreach(var v in filter.sharedMesh.vertices)minimum=Mathf.Min(minimum,filter.transform.TransformPoint(v).y);}
                if(minimum<.007f||fx.ActiveStones!=0||fx.ActiveClouds!=0)throw new Exception("Slam debris penetrates floor or never expires");
                fx.Burst(Vector3.zero,Vector3.right,"slam",2);fx.Tick(camera,.02f);fx.Clear();fx.Tick(camera,.3f);
                if(fx.ActiveStones!=0||fx.ActiveClouds!=0||root.GetComponentsInChildren<Transform>(true).Length!=initial)throw new Exception("Pending wave survives clear or grows its pool");
                report.AppendLine($"{rate}Hz ground wave=8/4→48/24 minGround={minimum:F5} expiry=passed clear=passed pool=passed");
            }
            File.WriteAllText(folder+"/checks.txt",report.ToString());
        }
        static Transform Bone(AnimatedActor actor,string name)
        {foreach(var b in actor.Root.GetComponentsInChildren<Transform>())if(b.name==name)return b;throw new Exception(name);}
        static void Render(string version,bool images=true)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(932);
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/monster-slam/"+version));Directory.CreateDirectory(folder);File.Delete(folder+"/validation.txt");
            var world=new GameWorld();var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            var state=new Battle();state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
            var target=new RenderTexture(1280,720,24){antiAliasing=4};target.Create();world.Camera.targetTexture=target;world.Camera.aspect=16f/9;
            var csv=new StringBuilder("frame,phase,attack,age,blocks,hurt,health,leftY,rightY,leftFootY,rightFootY\n");
            int frame=0;float bottom=100;var mesh=new Mesh();string lowest="";
            try
            {
                for(int i=0;i<1500;i++)
                {
                    const float dt=1/30f;float time=i*dt;
                    state.Tick(dt,new PlayerInput{Tracking=true,Shield=true});while(state.TryCue(out var cue))world.Cue(cue,state);
                    hero.Update(state,world.Camera,dt,time);enemy.Update(state,world.Camera,dt,time);world.Tick(state,dt,time);
                    bool capture=state.EnemyAttackCount==2&&state.Enemy==EnemyPhase.Windup&&state.WarningDuration-state.EnemyAge<1.4f||state.EnemyAttackCount==3;
                    if(capture)
                    {
                        if(images)CharacterReview.Save(world.Camera,target,folder+"/frame-"+frame.ToString("D3")+".png");
                        csv.AppendLine(FormattableString.Invariant($"{frame},{state.Enemy},{state.EnemyAttackCount},{state.EnemyAge:F4},{state.Blocks},{state.HitsTaken},{state.EnemyHealth},{Bone(enemy,"bip_hand_L").position.y:F4},{Bone(enemy,"bip_hand_R").position.y:F4},{enemy.FootPosition(true).y:F4},{enemy.FootPosition(false).y:F4}"));
                        foreach(var skin in enemy.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {skin.BakeMesh(mesh,true);var vertices=mesh.vertices;var weights=skin.sharedMesh.boneWeights;for(int vi=0;vi<vertices.Length;vi++){var p=skin.transform.TransformPoint(vertices[vi]);if(p.y<bottom){bottom=p.y;float nearest=100;string bone="";foreach(var b in skin.bones){float d=Vector3.Distance(p,b.position);if(d<nearest){nearest=d;bone=b.name;}}var w=weights[vi];lowest=$"frame={frame} {state.Enemy}/{state.EnemyAge:F4} {bone} {p} weights={skin.bones[w.boneIndex0].name}/{w.weight0:F2} {skin.bones[w.boneIndex1].name}/{w.weight1:F2}";}}}
                        if(state.Enemy==EnemyPhase.Attack&&state.EnemyAge>=.28f&&state.EnemyAge<.32f)
                        {
                            foreach(string n in new[]{"bip_spine_2","bip_upperArm_L","bip_lowerArm_L","bip_hand_L","bip_middle_1_L"})
                                Debug.Log("[SlamBone] "+n+"="+Bone(enemy,n).position.ToString("F4"));
                        }
                        frame++;
                    }
                    if(state.EnemyAttackCount==3&&state.Enemy==EnemyPhase.Recover&&state.EnemyAge>1.5f)break;
                }
                if(state.Blocks!=3||state.HitsTaken!=0||frame<100)throw new Exception("Slam review incomplete");
                File.WriteAllText(folder+"/motion.csv",csv.ToString());
                File.WriteAllText(folder+"/validation.txt",$"frames={frame} blocks={state.Blocks} health={state.EnemyHealth} minGround={bottom:F4} lowest={lowest}");
                var sources=new StringBuilder();using(var sha=System.Security.Cryptography.SHA256.Create())
                    foreach(string path in new[]{"Assets/Scripts/Runtime/RiggedActor.cs","Assets/Scripts/Runtime/AnimatedActor.cs","Assets/Scripts/Runtime/GameWorld.cs","Assets/Scripts/Runtime/CombatVfx.cs","Assets/Scripts/Runtime/GroundImpact.cs","Assets/Scripts/Runtime/MonsterAttackEffects.cs","Assets/Scripts/Runtime/StrikeTrails.cs","Assets/Scripts/Core/MonsterSlamMotion.cs","Assets/Scripts/Core/Battle.cs","Assets/Editor/MonsterSlamReview.cs"})
                    {var file=Path.Combine(Application.dataPath,"..",path);if(File.Exists(file))sources.AppendLine(path+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file))).Replace("-","").ToLowerInvariant());}
                File.WriteAllText(folder+"/sources.txt",sources.ToString());Debug.Log($"[MonsterSlamReview] {version} frames={frame} blocks={state.Blocks} minGround={bottom:F4}");
            }
            finally{world.Camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(mesh);}
        }
    }
}
