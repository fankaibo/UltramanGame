using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class MonsterBeamReview
    {
        public static void Before()=>Render("before");
        public static void After()=>Render("after");
        static void Render(string version)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(734);
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/monster-beam",version));Directory.CreateDirectory(folder+"/frames");File.Delete(folder+"/validation.txt");
            var world=new GameWorld();var state=Ready();var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);
            var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            while(state.TryCue(out _)){}hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,1,0);
            var target=new RenderTexture(1280,720,24){antiAliasing=4};target.Create();world.Camera.targetTexture=target;world.Camera.aspect=16f/9;
            var csv=new StringBuilder("frame,phase,action,age,health,energy\n");float health=state.EnemyHealth;int impacts=0;
            try
            {
                for(int f=0;f<390;f++)
                {
                    const float dt=1/60f;float time=f*dt;
                    state.Tick(world.BattleDelta(dt,state),new PlayerInput{Tracking=true,Beam=f==12});while(state.TryCue(out var cue))world.Cue(cue,state);
                    hero.Update(state,world.Camera,dt,time);enemy.Update(state,world.Camera,dt,time);
                    if(state.EnemyHealth<health){world.Hit(true,state);impacts++;}health=state.EnemyHealth;
                    world.Tick(state,dt,time);enemy.SetPresentationOpacity(world.EnemyOpacity);
                    csv.AppendLine(FormattableString.Invariant($"{f},{state.Phase},{state.Action},{state.ActionAge:F5},{health},{state.Energy}"));
                    if(f%2==0)CharacterReview.Save(world.Camera,target,$"{folder}/frames/{f/2:D4}.png");
                }
                if(impacts!=1||state.EnemyHealth!=56||state.Punches!=15||state.Energy!=0)throw new Exception("Beam motion changed combat");
                File.WriteAllText(folder+"/sequence.csv",csv.ToString());
                var sources=new StringBuilder();using(var sha=System.Security.Cryptography.SHA256.Create())
                    foreach(string file in new[]{"Scripts/Runtime/RiggedActor.cs","Scripts/Runtime/AnimatedActor.cs","Scripts/Runtime/GameWorld.cs","Scripts/Core/MonsterBeamMotion.cs","Editor/MonsterBeamReview.cs"})
                    {string path=Path.Combine(Application.dataPath,file);if(File.Exists(path))sources.AppendLine(file+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant());}
                File.WriteAllText(folder+"/sources.txt",sources.ToString());
                string report=$"{version}: frames=195 samples=390 impacts={impacts} health={health} punches={state.Punches} energy={state.Energy}";
                File.WriteAllText(folder+"/validation.txt",report);Debug.Log("[MonsterBeamReview] "+report);
            }
            finally{world.Camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);}
        }
        public static void Validate()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/monster-beam"));Directory.CreateDirectory(folder);File.Delete(folder+"/motion-validation.txt");
            var report=new StringBuilder();
            var cases=new System.Collections.Generic.List<(string hero,int rate,string mode)>();
            foreach(int rate in new[]{15,30,60})foreach(string mode in new[]{"hold","rapid","pause","restart","victory"})cases.Add(("Tiga",rate,mode));
            foreach(string hero in new[]{"Mebius","Zero","Geed","Grigio"})cases.Add((hero,30,"rapid"));
            foreach(var item in cases)
            {
                int rate=item.rate;string mode=item.mode;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var world=new GameWorld();var state=Ready(mode=="victory"?24:80);var hero=new AnimatedActor(item.hero,world.HeroHome,world.EnemyHome);
                var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                while(state.TryCue(out _)){}hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,1,0);
                var homeL=enemy.FootPosition(true);var homeR=enemy.FootPosition(false);var oldL=homeL;var oldR=homeR;
                var mesh=new Mesh();float dt=1f/rate,health=state.EnemyHealth,support=0,lift=0,back=0,slide=0,minY=10,step=0,rootStep=0,punchGap=0;
                Vector3 planted=Vector3.zero,oldRoot=enemy.Root.position;bool plantedSeen=false,interrupted=false,started=false;int sustained=0,postHits=0;
                try
                {
                    for(int frame=0;frame<rate*6;frame++)
                    {
                        float time=frame*dt;
                        if(!interrupted&&enemy.BeamRecoilAge>.2f&&enemy.BeamRecoilAge<.5f&&(mode=="pause"||mode=="restart"))
                        {interrupted=true;if(mode=="pause")state.Pause();else state=new Battle();}
                        state.Tick(world.BattleDelta(dt,state),new PlayerInput{Tracking=true,Beam=frame==0,LeftPunch=mode=="rapid"&&started&&state.Action==HeroAction.None});while(state.TryCue(out var cue))world.Cue(cue,state);
                        hero.Update(state,world.Camera,dt,time);enemy.Update(state,world.Camera,dt,time);
                        bool punchContact=state.EnemyHealth<health&&state.Action==HeroAction.LeftPunch;
                        if(state.EnemyHealth<health){world.Hit(state.Action==HeroAction.Beam,state);started=true;if(state.Action==HeroAction.LeftPunch||state.Action==HeroAction.RightPunch)postHits++;}health=state.EnemyHealth;world.Tick(state,dt,time);
                        var l=enemy.FootPosition(true);var r=enemy.FootPosition(false);float age=enemy.BeamRecoilAge;
                        if(started&&!interrupted&&mode!="victory")
                        {step=Mathf.Max(step,Vector3.Distance(l,oldL),Vector3.Distance(r,oldR));rootStep=Mathf.Max(rootStep,Vector3.Distance(enemy.Root.position,oldRoot));}
                        oldL=l;oldR=r;oldRoot=enemy.Root.position;
                        if(age<MonsterBeamMotion.Duration)
                        {
                            bool left=enemy.BeamRecoilLeft;var moving=left?l:r;var rest=left?homeL:homeR;
                            support=Mathf.Max(support,Vector3.Distance(left?r:l,left?homeR:homeL));lift=Mathf.Max(lift,moving.y-rest.y);back=Mathf.Max(back,Vector3.Dot(moving-rest,world.BattleAxis));
                            if(age>=MonsterBeamMotion.Landing&&age<=MonsterBeamMotion.Return)
                            {if(!plantedSeen){plantedSeen=true;planted=moving;}slide=Mathf.Max(slide,Vector3.Distance(moving,planted));}
                            if(state.Action==HeroAction.Beam&&state.ActionAge>1.3f){sustained++;if(enemy.Frame!=6)throw new Exception("Monster returned to idle before beam ended");}
                            var root=enemy.Root.position;var chest=enemy.BeamSurfaceContact;
                            enemy.Update(state,world.Camera,0,time);
                            if(Vector3.Distance(root,enemy.Root.position)>.002f||Vector3.Distance(l,enemy.FootPosition(true))>.002f||Vector3.Distance(r,enemy.FootPosition(false))>.002f||Vector3.Distance(chest,enemy.BeamSurfaceContact)>.002f)
                                throw new Exception($"Repeated beam sample drift {rate}/{mode} age={age:F3}");
                            if(frame%3==0)foreach(var skin in enemy.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                            {skin.BakeMesh(mesh,true);foreach(var v in mesh.vertices)minY=Mathf.Min(minY,skin.transform.TransformPoint(v).y);}
                            if(punchContact)
                            {
                                float nearest=float.MaxValue;var fist=hero.StrikeOrigin(state.Action)+world.BattleAxis*.12f;
                                foreach(var skin in enemy.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                                {
                                    skin.BakeMesh(mesh,true);var vertices=mesh.vertices;var weights=skin.sharedMesh.boneWeights;
                                    for(int v=0;v<vertices.Length;v++)
                                    {
                                        var w=weights[v];float Chest(int b,float weight)=>skin.bones[b].name.StartsWith("bip_spine",StringComparison.Ordinal)?weight:0;
                                        if(Chest(w.boneIndex0,w.weight0)+Chest(w.boneIndex1,w.weight1)+Chest(w.boneIndex2,w.weight2)+Chest(w.boneIndex3,w.weight3)<.75f)continue;
                                        nearest=Mathf.Min(nearest,Vector3.Distance(fist,skin.transform.TransformPoint(vertices[v])));
                                    }
                                }
                                punchGap=Mathf.Max(punchGap,nearest);
                                if(rate==30)
                                {
                                    var image=new RenderTexture(1280,720,24){antiAliasing=4};image.Create();world.Camera.aspect=16f/9;world.Camera.targetTexture=image;
                                    try{CharacterReview.Save(world.Camera,image,$"{folder}/{item.hero}-followup-{postHits}.png");}
                                    finally{world.Camera.targetTexture=null;RenderTexture.active=null;image.Release();UnityEngine.Object.DestroyImmediate(image);}
                                }
                            }
                        }
                    }
                    string line=$"{item.hero}/{rate}Hz {mode}: landings={enemy.BeamLandings} support={support:F4} lift={lift:F4} back={back:F4} plantedSlide={slide:F4} minY={minY:F4} footSpeed={step/dt:F3} rootSpeed={rootStep/dt:F3} sustain={sustained} postHits={postHits} torsoGap={punchGap:F3}";
                    Debug.Log("[MonsterBeamValidation] "+line);report.AppendLine(line);
                    if(mode=="hold"||mode=="rapid")
                    {
                        if(enemy.BeamLandings!=1||support>.035f||lift<.17f||back<.55f||slide>.035f||minY<-.035f||step/dt>6||rootStep/dt>5||sustained==0||enemy.BeamRecoilAge<10)
                            throw new Exception("Beam support/recovery failed: "+line);
                        if(mode=="rapid"&&(postHits<4||punchGap>.36f))throw new Exception("Following punches blocked or missing displaced torso: "+line);
                    }
                    if((mode=="pause"||mode=="restart")&&(!interrupted||enemy.BeamRecoilAge<10||enemy.BeamLandings!=0))throw new Exception("Interrupted beam step survived");
                    if(mode=="victory"&&(state.Phase!=GamePhase.Victory||enemy.BeamRecoilAge<10||enemy.BeamLandings!=0))throw new Exception("Beam step overrode victory");
                }
                finally{UnityEngine.Object.DestroyImmediate(mesh);}
            }
            File.WriteAllText(folder+"/motion-validation.txt",report.ToString());
        }
        static Battle Ready(int health=80)
        {
            var state=new Battle(health);state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});for(int i=0;i<140;i++)state.Tick(.02f,new PlayerInput{Tracking=true});state.GiveInstructionTime(30);
            for(int n=0;n<15;n++){state.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});for(int i=0;i<25;i++)state.Tick(.02f,new PlayerInput{Tracking=true});}
            return state;
        }
    }
}
