using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class MonsterRayReview
    {
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/monster-ray"));
        public static void Before()=>Render("before");
        public static void After(){Render("after");Checks();}
        public static void Checks()
        {
            Directory.CreateDirectory(Folder+"/checks");File.Delete(Folder+"/checks/validation.txt");var report=new StringBuilder();
            foreach(int hz in new[]{15,30,60})foreach(string mode in new[]{"guard","miss","late","release","punch","pause","restart","late-guide"})
                Validate("Tiga",hz,mode,report);
            foreach(string hero in new[]{"Mebius","Zero","Geed","Grigio"})Validate(hero,30,"guard",report);
            Validate("Tiga",30,"repeat",report);
            var clip=GameAudio.CreateMonsterRay();var samples=new float[clip.samples];clip.GetData(samples,0);float peak=0;double energy=0;
            foreach(float sample in samples){peak=Mathf.Max(peak,Mathf.Abs(sample));energy+=sample*sample;}
            float seam=Mathf.Abs(samples[0]-samples[samples.Length-1]);
            if(peak>=.95f||seam>.035f||energy/samples.Length<.002)throw new Exception("Invalid ray sound");
            report.AppendLine($"Audio peak={peak:F4} RMS={Math.Sqrt(energy/samples.Length):F4} seam={seam:F5}");UnityEngine.Object.DestroyImmediate(clip);
            if(UnityEditor.ShaderUtil.ShaderHasError(Resources.Load<Shader>("MonsterRay")))throw new Exception("Ray shader compile error");
            File.WriteAllText(Folder+"/checks/validation.txt",report.ToString());Debug.Log("[MonsterRayChecks] PASS\n"+report);
        }
        static void Validate(string heroId,int hz,string mode,StringBuilder report)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var world=new GameWorld();var hero=new AnimatedActor(heroId,world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            var state=new Battle();state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});float dt=1f/hz;
            var joints=enemy.Root.GetComponentsInChildren<Transform>();var positions=new Vector3[joints.Length];var mesh=new Mesh();
            int final=mode=="repeat"?10:4,seen=0;bool intervention=false,saved=false;
            Vector3 left=Vector3.zero,right=Vector3.zero;bool feet=false;
            float drift=0,zero=0,minimum=100,bend=0;int travel=0,contact=0;
            var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
            try
            {
                for(int f=0;f<hz*155;f++)
                {
                    bool ray=MonsterRayMotion.Active(state);var input=new PlayerInput{Tracking=true,Shield=true};
                    if(ray&&(mode=="miss"||mode=="late"&&state.Enemy!=EnemyPhase.Attack||mode=="late"&&state.EnemyAge<.24f||mode=="release"&&state.Enemy==EnemyPhase.Attack&&state.EnemyAge>.26f))input.Shield=false;
                    if(!intervention&&ray&&state.Enemy==EnemyPhase.Windup&&state.WarningDuration-state.EnemyAge<.70f)
                    {
                        if(mode=="punch"){input.Shield=false;input.LeftPunch=true;intervention=true;}
                        if(mode=="late-guide"){state.GiveInstructionTime(2,true);intervention=true;}
                    }
                    if(!intervention&&ray&&state.Enemy==EnemyPhase.Attack&&state.EnemyAge>=.26f&&(mode=="pause"||mode=="restart"))
                    {
                        state.Pause();world.Tick(state,dt,f*dt);world.ResetPresentation();
                        if(world.MonsterRayVisible||world.MonsterRayPower!=0)throw new Exception("Ray survived pause");
                        if(mode=="restart")state=new Battle();
                        hero.Update(state,world.Camera,dt,f*dt);enemy.Update(state,world.Camera,dt,f*dt);world.Tick(state,dt,f*dt);
                        if(world.MonsterRayVisible||GameObject.Find("Golza ray spill").GetComponent<Light>().intensity!=0)throw new Exception("Ray survived reset");
                        intervention=true;break;
                    }
                    state.Tick(dt,input);while(state.TryCue(out var cue))world.Cue(cue,state);
                    hero.Update(state,world.Camera,dt,f*dt);enemy.Update(state,world.Camera,dt,f*dt);world.Tick(state,dt,f*dt);
                    ray=MonsterRayMotion.Active(state);
                    if(ray&&!feet){left=enemy.FootPosition(true);right=enemy.FootPosition(false);feet=true;}
                    if(ray)
                    {
                        seen++;
                        if(mode!="punch")drift=Mathf.Max(drift,Vector3.Distance(left,enemy.FootPosition(true)),Vector3.Distance(right,enemy.FootPosition(false)));
                        foreach(string side in new[]{"L","R"})
                        {
                            Vector3 palm=Vector3.zero;foreach(string finger in new[]{"index","middle","ring","pinky"})palm+=Bone(enemy,"bip_"+finger+"_0_"+side).position;
                            var wrist=Bone(enemy,"bip_hand_"+side);bend=Mathf.Max(bend,Vector3.Angle(palm/4-wrist.position,wrist.position-Bone(enemy,"bip_lowerArm_"+side).position));
                        }
                        for(int i=0;i<joints.Length;i++)positions[i]=joints[i].position;
                        enemy.Update(state,world.Camera,0,f*dt);
                        for(int i=0;i<joints.Length;i++)zero=Mathf.Max(zero,Vector3.Distance(positions[i],joints[i].position));
                        if(f%4==0)foreach(var skin in enemy.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {skin.BakeMesh(mesh,true);foreach(var vertex in mesh.vertices)minimum=Mathf.Min(minimum,skin.transform.TransformPoint(vertex).y);}
                    }
                    if(world.MonsterRayVisible)
                    {
                        if(!ray||state.Enemy!=EnemyPhase.Attack||world.EnemySlashVisible||world.MonsterTrailVisible)throw new Exception("Ray mixed with a melee effect");
                        if(Vector3.Distance(world.MonsterRayOrigin,enemy.RayOrigin)>.0001f)throw new Exception("Detached forehead ray");
                        if(state.EnemyAge<Battle.EnemyHitSeconds){travel++;if(state.Blocks!=state.EnemyAttackCount-1||state.HitsTaken!=0)throw new Exception("Premature ray contact");}
                        else contact++;
                        if(!saved&&state.EnemyAge>.48f&&mode=="guard"&&hz==30)
                        {CharacterReview.Save(world.Camera,rt,Folder+"/checks/"+heroId+".png");saved=true;}
                    }
                    if(state.EnemyAttackCount==final&&state.Enemy==EnemyPhase.Rest)break;
                }
                bool stopped=mode=="pause"||mode=="restart",miss=mode=="miss"||mode=="release";
                int launches=world.MonsterRayLaunches;
                string result=$"{heroId} {hz}Hz {mode} launches={launches} seen={seen} travel={travel} contact={contact} blocks={state.Blocks} hurt={state.HitsTaken} footDrift={drift:F5} zeroTime={zero:F6} wrist={bend:F2} floor={minimum:F5} intervention={intervention}";
                if(seen==0||zero>.0001f||drift>.035f||bend>36||minimum<-.035f||world.MonsterRayVisible||world.MonsterRayPower!=0||
                    !stopped&&(launches!=(mode=="repeat"?2:1)||state.Blocks!=final-(miss?1:0)||state.HitsTaken!=(miss?1:0)||travel==0||contact==0)||
                    stopped&&!intervention||mode=="punch"&&state.Punches!=1||mode=="late-guide"&&!intervention)
                    throw new Exception("Ray validation failed: "+result);
                report.AppendLine(result+" passed");Debug.Log("[MonsterRayFlow] "+result);
            }
            finally{world.Camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(mesh);}
        }
        static Transform Bone(AnimatedActor actor,string name)
        {foreach(var bone in actor.Root.GetComponentsInChildren<Transform>())if(bone.name==name)return bone;throw new Exception("Missing bone "+name);}
        static void Render(string version)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(944);
            var world=new GameWorld();var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);
            var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            var state=new Battle();state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
            string folder=Folder+"/"+version;Directory.CreateDirectory(folder+"/frames");File.Delete(folder+"/validation.txt");
            var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
            var csv=new StringBuilder("frame,enemy,attack,age,blocks,hurt,health\n");int frame=0,captured=0;
            try
            {
                for(int i=0;i<4200;i++)
                {
                    const float dt=1/60f;state.Tick(dt,new PlayerInput{Tracking=true,Shield=true});
                    while(state.TryCue(out var cue))world.Cue(cue,state);
                    hero.Update(state,world.Camera,dt,i*dt);enemy.Update(state,world.Camera,dt,i*dt);world.Tick(state,dt,i*dt);
                    bool capture=state.EnemyAttackCount==3&&state.Enemy==EnemyPhase.Windup&&state.WarningDuration-state.EnemyAge<2.2f||state.EnemyAttackCount==4;
                    if(capture)
                    {
                        csv.AppendLine(FormattableString.Invariant($"{frame},{state.Enemy},{state.EnemyAttackCount},{state.EnemyAge:F4},{state.Blocks},{state.HitsTaken},{state.EnemyHealth}"));
                        if(frame%2==0){CharacterReview.Save(world.Camera,rt,$"{folder}/frames/{captured:D4}.png");captured++;}frame++;
                    }
                    if(state.EnemyAttackCount==4&&state.Enemy==EnemyPhase.Rest)break;
                }
                if(state.EnemyAttackCount!=4||state.Blocks!=4||state.HitsTaken!=0||captured<90)throw new Exception("Incomplete ray sequence");
                File.WriteAllText(folder+"/sequence.csv",csv.ToString());
                var source=new StringBuilder("UTC: "+DateTime.UtcNow.ToString("O")+"\n");
                using(var sha=System.Security.Cryptography.SHA256.Create())
                    foreach(string file in new[]{"Scripts/Runtime/RiggedActor.cs","Scripts/Runtime/AnimatedActor.cs","Scripts/Runtime/CombatVfx.cs","Scripts/Runtime/MonsterAttackEffects.cs","Scripts/Runtime/GameWorld.cs","Scripts/Runtime/StrikeTrails.cs","Scripts/Runtime/GameAudio.cs","Scripts/Runtime/ArenaController.cs","Scripts/Core/Battle.cs","Scripts/Core/MonsterRayMotion.cs","Scripts/Runtime/MonsterRay.cs","Resources/MonsterRay.shader","Resources/BeamChargeVolume.shader","Scripts/Runtime/SkinnedSurfaceAnchor.cs","Scripts/Runtime/ArcadeHud.cs","Scripts/Runtime/GuidedProof.cs"})
                    {string path=Path.Combine(Application.dataPath,file);if(File.Exists(path))source.AppendLine(file+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant());}
                File.WriteAllText(folder+"/source.txt",source.ToString());
                string result=$"{version}: frames={captured} blocks=4 hurt=0 health=50 fourth-attack=passed";
                File.WriteAllText(folder+"/validation.txt",result);Debug.Log("[MonsterRayReview] "+result);
            }
            finally{world.Camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
        }
    }
}
