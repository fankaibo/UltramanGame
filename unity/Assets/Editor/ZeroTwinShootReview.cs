using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class ZeroTwinShootReview
    {
        public static void Before()=>Run("before",60,true);
        public static void After(){foreach(int hz in new[]{15,30,60})Run("after",hz,hz==60);Lifecycle();}
        static Battle Ready()
        {
            var b=new Battle(50);b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
            while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});
            b.GiveInstructionTime(60);
            for(int i=0;i<15;i++){b.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});for(int k=0;k<25;k++)b.Tick(.02f,new PlayerInput{Tracking=true});}
            while(b.TryCue(out _)){}return b;
        }
        static void Run(string version,int hz,bool movie)
        {
            string folder=Path.GetFullPath("../artifacts/zero-twin-shoot-20261009/"+version+"/"+hz);Directory.CreateDirectory(folder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(1060);
            var world=new GameWorld();var hero=new AnimatedActor("Zero",world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);
            world.BindActors(hero,enemy);world.SetHeroProfile("Zero");var state=Ready();
            var report=new StringBuilder();var mesh=new Mesh();
            foreach(var skin in hero.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skin.BakeMesh(mesh,true);var vertices=mesh.vertices;var mats=skin.sharedMaterials;
                for(int sub=0;sub<mats.Length;sub++)
                {Vector3 center=Vector3.zero;var set=new System.Collections.Generic.HashSet<int>(mesh.GetTriangles(sub));foreach(int i in set)center+=skin.transform.TransformPoint(vertices[i]);report.AppendLine($"{skin.name}/{mats[sub].name} count={set.Count} center={center/set.Count}");}
            }
            UnityEngine.Object.DestroyImmediate(mesh);File.WriteAllText(folder+"/model.txt",report.ToString());
            var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
            if(movie)Directory.CreateDirectory(folder+"/frames");
            var trace=new StringBuilder("frame,action,age,health,energy,beams,closeup\n");float health=state.EnemyHealth;int hits=0,launches=0;
            float dt=1f/hz;int shot=0;float[] marks={.55f,1.25f,2.0f,3.55f,4.0f};
            float dockError=0,muzzleError=0,repeatError=0,maxStep=0,minGround=10,armError=0,timerOffset=0;int dockFrames=0;
            var arms=new System.Collections.Generic.List<Transform>();var armRest=new System.Collections.Generic.List<float>();
            foreach(var bone in hero.Root.GetComponentsInChildren<Transform>())if(bone.name.StartsWith("bip_lowerArm_")||bone.name.StartsWith("bip_hand_"))
            {arms.Add(bone);armRest.Add(Vector3.Distance(bone.position,bone.parent.position));}
            var oldLeft=hero.HandPosition;var baked=new Mesh();
            try
            {
                for(int f=0;f<hz*5;f++)
                {
                    float time=f*dt;state.Tick(world.BattleDelta(dt,state),new PlayerInput{Tracking=true,Beam=f==hz/5});
                    while(state.TryCue(out var cue))world.Cue(cue,state);
                    hero.Update(state,world.Camera,dt,time);enemy.Update(state,world.Camera,dt,time);
                    if(state.EnemyHealth<health){world.Hit(true,state);hits++;}health=state.EnemyHealth;
                    world.Tick(state,dt,time);enemy.SetPresentationOpacity(world.EnemyOpacity);if(world.BeamStarted)launches++;
                    if(version=="after")
                    {
                        if(hero.TwinShoot==null||hero.TwinShoot.TimerVertices!=184)throw new Exception("Missing source timer or blades");
                        if(hero.TwinShoot.Active)
                        {
                            if(f>hz/5+1)maxStep=Mathf.Max(maxStep,Vector3.Distance(hero.HandPosition,oldLeft));
                            if(hero.TwinShoot.Dock>.999f)
                            {dockFrames++;for(int i=0;i<2;i++)dockError=Mathf.Max(dockError,Vector3.Distance(hero.Sluggers.Center(i),hero.TwinShoot.BladeTarget(i)));}
                        }
                        oldLeft=hero.HandPosition;
                        var stream=GameObject.Find("Zeperion traveling stream").GetComponent<LineRenderer>();
                        if(world.BeamVisible)muzzleError=Mathf.Max(muzzleError,Vector3.Distance(stream.GetPosition(0),hero.TwinShoot.Muzzle));
                        var positions=new[]{hero.Sluggers.Center(0),hero.Sluggers.Center(1),hero.HandPosition};
                        hero.Update(state,world.Camera,0,time);world.Tick(state,0,time);
                        repeatError=Mathf.Max(repeatError,Vector3.Distance(positions[0],hero.Sluggers.Center(0)),Vector3.Distance(positions[1],hero.Sluggers.Center(1)),Vector3.Distance(positions[2],hero.HandPosition));
                        for(int i=0;i<arms.Count;i++)armError=Mathf.Max(armError,Mathf.Abs(Vector3.Distance(arms[i].position,arms[i].parent.position)-armRest[i]));
                        if(f%3==0)foreach(var skin in hero.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {
                            skin.BakeMesh(baked,true);var vertices=baked.vertices;foreach(var v in vertices)minGround=Mathf.Min(minGround,skin.transform.TransformPoint(v).y);
                            for(int sub=0;sub<skin.sharedMaterials.Length;sub++)if(skin.sharedMaterials[sub].name.Contains("colortimer"))
                            {var indices=new System.Collections.Generic.HashSet<int>(baked.GetTriangles(sub));Vector3 point=Vector3.zero;foreach(int v in indices)point+=skin.transform.TransformPoint(vertices[v]);timerOffset=Mathf.Max(timerOffset,Vector3.Distance(point/indices.Count,hero.BeamOrigin));}
                        }
                    }
                    trace.AppendLine(FormattableString.Invariant($"{f},{state.Action},{state.ActionAge:F5},{health},{state.Energy},{hits},{world.Closeup.Active}"));
                    if(movie&&f%2==0)CharacterReview.Save(world.Camera,rt,folder+"/frames/"+(f/2).ToString("D4")+".png");
                    if(movie&&shot<marks.Length&&time>=marks[shot])
                    {
                        CharacterReview.Save(world.Camera,rt,folder+"/stage-"+shot+".png");
                        var p=world.Camera.transform.position;var r=world.Camera.transform.rotation;float lens=world.Camera.fieldOfView;
                        var center=hero.Root.position+Vector3.up*2.45f;world.Camera.transform.position=center+world.BattleAxis*3.0f+Vector3.Cross(Vector3.up,world.BattleAxis)*1.0f;
                        world.Camera.transform.LookAt(center);world.Camera.fieldOfView=35;
                        float opacity=world.EnemyOpacity;enemy.SetPresentationOpacity(0);
                        CharacterReview.Save(world.Camera,rt,folder+"/front-"+shot+".png");world.Camera.transform.SetPositionAndRotation(p,r);world.Camera.fieldOfView=lens;shot++;
                        enemy.SetPresentationOpacity(opacity);
                    }
                }
                if(hits!=1||launches!=1||health!=26||world.BeamVisible)throw new Exception("Incomplete finisher "+version);
                if(version=="after"&&(dockFrames<hz||dockError>.0001f||muzzleError>.0001f||repeatError>.0001f||maxStep>.25f*60/hz||minGround<-.025f||armError>.0001f||timerOffset>.09f||hero.Sluggers.Detached||hero.TwinShoot.Active))
                    throw new Exception($"Invalid finisher dock={dockFrames}/{dockError} muzzle={muzzleError} repeat={repeatError} step={maxStep} ground={minGround} arm={armError} timer={timerOffset} returned={!hero.Sluggers.Detached}");
            }
            finally{world.Camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(baked);}
            File.WriteAllText(folder+"/trace.csv",trace.ToString());
            var sources=new StringBuilder();foreach(string p in Directory.GetFiles(Application.dataPath+"/Scripts","*.cs",SearchOption.AllDirectories))
                using(var sha=System.Security.Cryptography.SHA256.Create())sources.AppendLine(p.Substring(Application.dataPath.Length+1)+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant());
            File.WriteAllText(folder+"/sources.txt",sources.ToString());
            string result=$"{version}-{hz} hits={hits} launches={launches} health={health} dockFrames={dockFrames} dockError={dockError:F6} muzzleError={muzzleError:F6} repeat={repeatError:F6} maxHandStep={maxStep:F4} ground={minGround:F5} armError={armError:F6} timerOffset={timerOffset:F5} passed";
            File.WriteAllText(folder+"/validation.txt",result+"\n");Debug.Log("[ZeroTwinShootReview] "+result);
        }
        static void Require(bool value,string message){if(!value)throw new Exception(message);}
        static void Lifecycle()
        {
            var report=new StringBuilder();
            foreach(int hz in new[]{15,30,60})foreach(string mode in new[]{"pause-grab","pause-dock","pause-fire","new-round","guard-return","ranged-return","hero-switch","victory","pause-terminal"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var world=new GameWorld();var hero=new AnimatedActor("Zero",world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);
                world.BindActors(hero,enemy);world.SetHeroProfile("Zero");var b=Ready();float dt=1f/hz,time=0;
                if(mode=="victory"||mode=="pause-terminal")
                {
                    // 15 ordinary hits fill a 24 HP round, making its beam terminal.
                    b=new Battle(24);b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});b.GiveInstructionTime(60);
                    for(int i=0;i<15;i++){b.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});for(int j=0;j<25;j++)b.Tick(.02f,new PlayerInput{Tracking=true});}while(b.TryCue(out _)){}
                }
                void Draw(float step)
                {time+=step;hero.Update(b,world.Camera,step,time);enemy.Update(b,world.Camera,step,time);world.Tick(b,step,time);}
                void Tick(PlayerInput input)
                {b.Tick(world.BattleDelta(dt,b),input);while(b.TryCue(out var cue))world.Cue(cue,b);Draw(dt);}
                void Mounted(){Require(!hero.Sluggers.Detached&&!hero.TwinShoot.Active,"Stale finisher: "+mode);for(int i=0;i<2;i++)Require(Vector3.Distance(hero.Sluggers.Center(i),hero.Sluggers.MountedCenter(i))<.0001f,"Missing mounted blade");}
                Draw(dt);Tick(new PlayerInput{Tracking=true,Beam=true});
                float at=mode=="pause-grab"?.2f:mode=="pause-dock"?.95f:1.9f;
                while(time<at)Tick(new PlayerInput{Tracking=true});
                Require(hero.TwinShoot.Active,"Missing finisher: "+mode);
                if(mode=="pause-terminal")
                {
                    while(b.EnemyHealth>0)Tick(new PlayerInput{Tracking=true});
                    var before=new[]{hero.Sluggers.Center(0),hero.Sluggers.Center(1),hero.HandPosition};b.Pause();
                    for(int i=0;i<3;i++)Draw(dt);
                    Require(Vector3.Distance(before[0],hero.Sluggers.Center(0))<.0001f&&Vector3.Distance(before[1],hero.Sluggers.Center(1))<.0001f&&Vector3.Distance(before[2],hero.HandPosition)<.0001f,"Terminal pause moved pose");
                    for(int i=0;i<hz*4;i++)Tick(new PlayerInput{Tracking=true});Mounted();Require(b.Phase==GamePhase.Victory,"Terminal resume failed");
                }
                else if(mode.StartsWith("pause"))
                {
                    b.Pause();Draw(dt);Mounted();Require(!world.BeamVisible&&!world.ChargeVisible,"Paused energy remains");
                    for(int i=0;i<hz*2;i++)Tick(new PlayerInput{Tracking=true});Mounted();Require(b.Phase==GamePhase.Battle&&b.Action!=HeroAction.Beam,"Pause resumed canceled finisher");
                }
                else if(mode=="new-round")
                {world.ResetPresentation();b=Ready();Draw(dt);Mounted();Require(!world.BeamVisible&&!world.ChargeVisible,"New round retains energy");}
                else if(mode=="hero-switch")
                {var other=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);world.ResetPresentation();world.BindActors(other,enemy);world.SetHeroProfile("Tiga");Mounted();Require(other.TwinShoot==null,"Zero animation leaked into Tiga");}
                else
                {
                    for(int i=0;i<hz*3&&b.Action==HeroAction.Beam&&b.Phase==GamePhase.Battle;i++)Tick(new PlayerInput{Tracking=true});
                    if(mode=="guard-return"){Tick(new PlayerInput{Tracking=true,GuardIntent=true,Shield=true});Mounted();Require(b.Shield,"Return blocks defence");}
                    else if(mode=="ranged-return")
                    {
                        Tick(new PlayerInput{Tracking=true,RightPunch=true,RangedAttack=true});
                        for(int i=0;i<hz*2;i++)Tick(new PlayerInput{Tracking=true});Mounted();Require(world.Projectile.Launches==1,"Return blocks ranged blades");
                    }
                    else {Require(b.Phase==GamePhase.Victory,"Terminal beam missing victory");for(int i=0;i<hz;i++)Draw(dt);Mounted();}
                }
                report.AppendLine($"{hz}Hz {mode} passed");
            }
            File.WriteAllText(Path.GetFullPath("../artifacts/zero-twin-shoot-20261009/lifecycle.txt"),report.ToString());Debug.Log("[ZeroTwinShootLifecycle] 27 passed");
        }
    }
}
