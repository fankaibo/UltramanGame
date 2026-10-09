using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class AtmosImpactReview
    {
        public static void Before()=>Run("before",60,true);
        public static void After(){foreach(int hz in new[]{15,30,60})Run("after",hz,hz==60);Lifecycle();WaveLifecycle();}
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
            string folder=Path.GetFullPath("../artifacts/atmos-impact-20261009/"+version+"/"+hz);Directory.CreateDirectory(folder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(1060);
            var world=new GameWorld();var hero=new AnimatedActor("Geed",world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);
            world.BindActors(hero,enemy);world.SetHeroProfile("Geed");var state=Ready();
            var report=new StringBuilder();var mesh=new Mesh();
            foreach(var bone in hero.Root.GetComponentsInChildren<Transform>())if(bone.name.Contains("Arm")||bone.name.Contains("hand")||bone.name.Contains("spine"))report.AppendLine($"bone {bone.name} pos={hero.Root.InverseTransformPoint(bone.position)} rot={bone.localEulerAngles}");
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
            float minCrossGap=10,crossGap=0,muzzleError=0,repeatError=0,maxStep=0,minGround=10,armError=0,rightTilt=0,leftTilt=0;int crossFrames=0;
            var geometry=new StringBuilder("frame,age,rightTilt,leftTilt,crossGap,step\n");
            var arms=new System.Collections.Generic.List<Transform>();var armRest=new System.Collections.Generic.List<float>();
            foreach(var bone in hero.Root.GetComponentsInChildren<Transform>())if(bone.name.StartsWith("bip_lowerArm_")||bone.name.StartsWith("bip_hand_"))
            {arms.Add(bone);armRest.Add(Vector3.Distance(bone.position,bone.parent.position));}
            var oldLeft=hero.StrikeOrigin(HeroAction.LeftPunch);var oldRight=hero.HandPosition;var baked=new Mesh();
            Transform Bone(string name)=>Array.Find(hero.Root.GetComponentsInChildren<Transform>(),b=>b.name==name);
            var le=Bone("bip_lowerArm_L");var lw=Bone("bip_hand_L");var re=Bone("bip_lowerArm_R");var rw=Bone("bip_hand_R");
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
                        if(hero.Atmos==null||hero.Atmos.FingerCount!=4)throw new Exception("Missing Geed bones");
                        float step=Mathf.Max(Vector3.Distance(lw.position,oldLeft),Vector3.Distance(rw.position,oldRight));
                        if(hero.Atmos.Active&&f>hz/5+1)maxStep=Mathf.Max(maxStep,step);
                        oldLeft=lw.position;oldRight=rw.position;
                        if(hero.Atmos.Crossed)
                        {
                            crossFrames++;var l=lw.position-le.position;var r=rw.position-re.position;
                            float a=Vector3.Angle(r,Vector3.up),b=Mathf.Abs(90-Vector3.Angle(l,Vector3.up));
                            var center=re.position+r*.52f;
                            var closest=le.position+l*Mathf.Clamp01(Vector3.Dot(center-le.position,l)/l.sqrMagnitude);
                            float gap=Vector3.Distance(closest,center);
                            rightTilt=Mathf.Max(rightTilt,a);leftTilt=Mathf.Max(leftTilt,b);crossGap=Mathf.Max(crossGap,gap);minCrossGap=Mathf.Min(minCrossGap,gap);
                            geometry.AppendLine(FormattableString.Invariant($"{f},{hero.Atmos.Age:F5},{a:F5},{b:F5},{gap:F5},{step:F5}"));
                        }
                        if(world.BeamVisible)
                        {var stream=GameObject.Find("Zeperion traveling stream").GetComponent<LineRenderer>();muzzleError=Mathf.Max(muzzleError,Vector3.Distance(stream.GetPosition(0),hero.BeamOrigin));}
                        var visible=new[]{lw.position,rw.position};hero.Update(state,world.Camera,0,time);world.Tick(state,0,time);
                        repeatError=Mathf.Max(repeatError,Vector3.Distance(visible[0],lw.position),Vector3.Distance(visible[1],rw.position));
                        for(int i=0;i<arms.Count;i++)armError=Mathf.Max(armError,Mathf.Abs(Vector3.Distance(arms[i].position,arms[i].parent.position)-armRest[i]));
                        if(f%3==0)foreach(var skin in hero.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {skin.BakeMesh(baked,true);foreach(var v in baked.vertices)minGround=Mathf.Min(minGround,skin.transform.TransformPoint(v).y);}
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
                if(version=="after")
                {
                    File.WriteAllText(folder+"/geometry.csv",geometry.ToString());
                    if(crossFrames<hz||rightTilt>2||leftTilt>15||crossGap>.40f||minCrossGap<.08f||muzzleError>.0001f||repeatError>.0001f||maxStep>.25f*60/hz||minGround<-.025f||armError>.0001f||hero.Atmos.Active)
                        throw new Exception($"Invalid Atmos cross={crossFrames} right={rightTilt} left={leftTilt} gap={minCrossGap}..{crossGap} muzzle={muzzleError} repeat={repeatError} step={maxStep} ground={minGround} length={armError} active={hero.Atmos.Active}");
                    if(UnityEditor.ShaderUtil.ShaderHasError(Resources.Load<Shader>("AtmosWave")))throw new Exception("Atmos wave shader failed");
                }

            }
            finally{world.Camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(baked);}
            File.WriteAllText(folder+"/trace.csv",trace.ToString());
            var sources=new StringBuilder();foreach(string p in Directory.GetFiles(Application.dataPath+"/Scripts","*.cs",SearchOption.AllDirectories))
                using(var sha=System.Security.Cryptography.SHA256.Create())sources.AppendLine(p.Substring(Application.dataPath.Length+1)+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant());
            foreach(string shader in new[]{"BeamChargeVolume","AtmosWave"})using(var sha=System.Security.Cryptography.SHA256.Create())sources.AppendLine("Resources/"+shader+".shader "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Application.dataPath+"/Resources/"+shader+".shader"))).Replace("-","").ToLowerInvariant());
            File.WriteAllText(folder+"/sources.txt",sources.ToString());
            string result=$"{version}-{hz} hits={hits} launches={launches} health={health} crossFrames={crossFrames} rightTilt={rightTilt:F4} leftTilt={leftTilt:F4} crossGap={minCrossGap:F5}..{crossGap:F5} muzzleError={muzzleError:F6} repeat={repeatError:F6} maxHandStep={maxStep:F4} ground={minGround:F5} armError={armError:F6} passed";
            File.WriteAllText(folder+"/validation.txt",result+"\n");Debug.Log("[AtmosImpactReview] "+result);
        }
        static void WaveLifecycle()
        {
            var report=new StringBuilder();
            foreach(int hz in new[]{15,30,60})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var parent=new GameObject("Wave review").transform;var beam=new BeamStream(parent);
                var camera=new GameObject("Wave camera").AddComponent<Camera>();camera.transform.position=new Vector3(0,2,-5);
                var origin=new Vector3(0,2,0);var target=new Vector3(4,2,0);beam.SetAtmos(true);
                int sampled=0;float firstContact=-1;
                for(int f=0;f<hz*2;f++)
                {
                    float age=f/(float)hz;beam.Tick(camera,origin,target,age,age<Battle.BeamSeconds,age);
                    if(beam.Wave.Visible)
                    {
                        sampled++;Require(beam.Wave.ActiveRings>0&&beam.Wave.ActiveRings<=6,"Unbounded wave fronts");
                        Require(Vector3.Distance(beam.Wave.Origin,origin)<.0001f,"Wrong wave muzzle");
                        Require(!GameObject.Find("Zeperion traveling stream").GetComponent<LineRenderer>().enabled,"Geed retains continuous ribbon");
                        if(firstContact<0&&Vector3.Distance(beam.Wave.FirstFront,target)<.0001f)firstContact=age;
                    }
                    var point=beam.Wave.FirstFront;int count=beam.Wave.ActiveRings;
                    beam.Tick(camera,origin,target,age,age<Battle.BeamSeconds,age);
                    Require(Vector3.Distance(point,beam.Wave.FirstFront)<.0001f&&count==beam.Wave.ActiveRings,"Repeated wave sampling drifts");
                }
                Require(sampled>hz/2&&firstContact>=Battle.BeamHitSeconds-.0001f&&firstContact<Battle.BeamHitSeconds+1f/hz+.0001f,"Wave contact desynchronized");
                Require(!beam.Visible&&!beam.Wave.Visible,"Wave survives completed attack");
                beam.Tick(camera,origin,target,.65f,true,.65f);beam.Clear();Require(!beam.Visible,"Wave survives pause");
                beam.Tick(camera,origin,target,.65f,true,.65f);beam.SetAtmos(false);Require(!beam.Wave.Visible,"Wave survives hero switch");
                beam.Tick(camera,origin,target,.65f,true,.65f);Require(beam.Visible&&!beam.Wave.Visible,"Ordinary beam not restored");
                report.AppendLine($"{hz}Hz propagation/contact/repeat/clear/hero-switch passed");
            }
            File.WriteAllText(Path.GetFullPath("../artifacts/atmos-impact-20261009/wave-lifecycle.txt"),report.ToString());
        }
        static void Require(bool value,string message){if(!value)throw new Exception(message);}
        static void Lifecycle()
        {
            var report=new StringBuilder();
            foreach(int hz in new[]{15,30,60})foreach(string mode in new[]{"pause-circle","pause-cross","pause-fire","new-round","guard-return","ranged-return","punch-return","hero-switch","preview","victory","pause-terminal"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var world=new GameWorld();var hero=new AnimatedActor("Geed",world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);
                world.BindActors(hero,enemy);world.SetHeroProfile("Geed");var b=Ready();float dt=1f/hz,time=0;
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
                void Mounted(){Require(!hero.Atmos.Active,"Stale finisher: "+mode);}
                Draw(dt);Tick(new PlayerInput{Tracking=true,Beam=true});
                float at=mode=="pause-circle"?.2f:mode=="pause-cross"?1.2f:1.9f;
                while(time<at)Tick(new PlayerInput{Tracking=true});
                Require(hero.Atmos.Active,"Missing finisher: "+mode);
                if(mode=="pause-terminal")
                {
                    while(b.EnemyHealth>0)Tick(new PlayerInput{Tracking=true});
                    var before=new[]{hero.StrikeOrigin(HeroAction.LeftPunch),hero.HandPosition,hero.BeamOrigin};b.Pause();
                    for(int i=0;i<3;i++)Draw(dt);
                    Require(Vector3.Distance(before[0],hero.StrikeOrigin(HeroAction.LeftPunch))<.0001f&&Vector3.Distance(before[1],hero.HandPosition)<.0001f&&Vector3.Distance(before[2],hero.BeamOrigin)<.0001f,"Terminal pause moved pose");
                    for(int i=0;i<hz*4;i++)Tick(new PlayerInput{Tracking=true});Mounted();Require(b.Phase==GamePhase.Victory,"Terminal resume failed");
                }
                else if(mode.StartsWith("pause"))
                {
                    b.Pause();Draw(dt);Mounted();Require(!world.BeamVisible&&!world.ChargeVisible,"Paused energy remains");
                    for(int i=0;i<hz*2;i++)Tick(new PlayerInput{Tracking=true});Mounted();Require(b.Phase==GamePhase.Battle&&b.Action!=HeroAction.Beam,"Pause resumed canceled finisher");
                }
                else if(mode=="new-round")
                {world.ResetPresentation();b=Ready();Draw(dt);Mounted();Require(!world.BeamVisible&&!world.ChargeVisible,"New round retains energy");}
                else if(mode=="preview")
                {
                    world.ResetPresentation();b=Ready();
                    hero.Update(b,world.Camera,dt,time+dt,4);
                    var rw=Array.Find(hero.Root.GetComponentsInChildren<Transform>(),j=>j.name=="bip_hand_R");
                    var re=rw.parent;Require(Vector3.Angle(rw.position-re.position,Vector3.up)<2,"Showcase has old arm pose");
                    var wrist=rw.position;hero.Update(b,world.Camera,0,time+dt,4);
                    Require(Vector3.Distance(wrist,rw.position)<.0001f,"Showcase pose accumulates");
                    hero.Update(b,world.Camera,dt,time+2*dt,0);Mounted();
                    Tick(new PlayerInput{Tracking=true});Mounted();
                }
                else if(mode=="hero-switch")
                {var other=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);world.ResetPresentation();world.BindActors(other,enemy);world.SetHeroProfile("Tiga");Mounted();Require(other.Atmos==null,"Atmos animation leaked into Tiga");Require(Array.Find(GameObject.Find("Volcanic night arena").GetComponentsInChildren<Renderer>(true),r=>r.name=="Gathered forearm energy").sharedMaterial.GetFloat("_RoseBlend")==0,"Rose charge leaked into Tiga");}
                else
                {
                    for(int i=0;i<hz*3&&b.Action==HeroAction.Beam&&b.Phase==GamePhase.Battle;i++)Tick(new PlayerInput{Tracking=true});
                    if(mode=="guard-return"){Tick(new PlayerInput{Tracking=true,GuardIntent=true,Shield=true});Mounted();Require(b.Shield,"Return blocks defence");}
                    else if(mode=="punch-return")
                    {
                        int hits=b.Punches;Tick(new PlayerInput{Tracking=true,LeftPunch=true});
                        for(int i=0;i<hz;i++)Tick(new PlayerInput{Tracking=true});
                        Mounted();Require(b.Punches==hits+1,"Return blocks ordinary punch");
                    }
                    else if(mode=="ranged-return")
                    {
                        Tick(new PlayerInput{Tracking=true,RightPunch=true,RangedAttack=true});
                        for(int i=0;i<hz*2;i++)Tick(new PlayerInput{Tracking=true});Mounted();Require(world.Projectile.Launches==1,"Return blocks ranged shot");
                    }
                    else {Require(b.Phase==GamePhase.Victory,"Terminal beam missing victory");for(int i=0;i<hz;i++)Draw(dt);Mounted();}
                }
                report.AppendLine($"{hz}Hz {mode} passed");
            }
            File.WriteAllText(Path.GetFullPath("../artifacts/atmos-impact-20261009/lifecycle.txt"),report.ToString());Debug.Log("[AtmosImpactLifecycle] 33 passed");
        }
    }
}
