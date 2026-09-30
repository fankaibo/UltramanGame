using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class ClawPoseReview
    {
        public static void Before()=>Render("before");
        public static void After()=>Render("after");
        public static void Release(){After();Stability();ExchangeReview.ClawTiming();}
        static Transform Bone(Transform root,string name)
        {foreach(var b in root.GetComponentsInChildren<Transform>())if(b.name==name)return b;throw new Exception(name);}
        public static void Stability()
        {
            var args=Environment.GetCommandLineArgs();int outputAt=Array.IndexOf(args,"--claw-output");
            string folder=outputAt>=0&&outputAt+1<args.Length?args[outputAt+1]:Path.Combine(Application.dataPath,"../../artifacts/claw-alignment");
            Directory.CreateDirectory(folder);File.Delete(Path.Combine(folder,"stability.txt"));var report=new StringBuilder();
            foreach(int rate in new[]{15,30,60})foreach(string mode in new[]{"exchange","strikes","pause","new-round"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var world=new GameWorld();var state=new Battle(80);state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
                for(int f=0;f<(Battle.TransformationSeconds+0.6f)/(.02f);f++)state.Tick(.02f,new PlayerInput{Tracking=true});
                if(mode!="exchange")state.GiveInstructionTime(40);while(state.TryCue(out _)){}
                var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                var tracked=new System.Collections.Generic.List<Transform>();
                foreach(var joint in enemy.Root.GetComponentsInChildren<Transform>())
                    if(joint.name.StartsWith("tail_")||joint.name.StartsWith("bip_")&&(joint.name.Contains("hand_")||joint.name.Contains("lowerArm_")||joint.name.Contains("index_")||joint.name.Contains("middle_")||joint.name.Contains("ring_")||joint.name.Contains("pinky_")||joint.name.Contains("thumb_")))tracked.Add(joint);
                var before=new Vector3[tracked.Count];float error=0,bend=0,dt=1f/rate;int hits=0,beams=0;bool reset=false,pause=false,resume=false;string worst="";
                for(int f=0;f<rate*28;f++)
                {
                    bool strike=mode!="exchange"&&f%Mathf.CeilToInt(rate*.60f)==0&&state.Punches<16;
                    if(mode=="pause"&&!pause&&enemy.LaunchAge>.2f&&enemy.LaunchAge<.5f){state.Pause();pause=true;}
                    if(mode=="pause"&&pause&&!resume&&f>rate*8){state.Tick(dt,new PlayerInput{Tracking=true});resume=true;}
                    if(mode=="new-round"&&!reset&&enemy.LaunchAge>.2f&&enemy.LaunchAge<.5f){state=new Battle(80);world.ResetPresentation();reset=true;}
                    float health=state.EnemyHealth;
                    state.Tick(world.BattleDelta(dt,state),new PlayerInput{Tracking=!(pause&&!resume),LeftPunch=strike,Beam=state.Energy>=15,Shield=mode=="exchange"&&state.EnemyAttackCount<=1});
                    while(state.TryCue(out var cue)){world.Cue(cue,state);if(cue==GameCue.Beam)beams++;}
                    hero.Update(state,world.Camera,dt,f*dt);enemy.Update(state,world.Camera,dt,f*dt);
                    if(state.EnemyHealth<health){hits++;world.Hit(health-state.EnemyHealth>1,state);}world.Tick(state,dt,f*dt);
                    for(int i=0;i<tracked.Count;i++)before[i]=tracked[i].position;
                    enemy.Update(state,world.Camera,0,f*dt);
                    for(int i=0;i<tracked.Count;i++)
                    {
                        var p=tracked[i].position;if(float.IsNaN(p.sqrMagnitude)||float.IsInfinity(p.sqrMagnitude))throw new Exception("Invalid claw transform");
                        float distance=Vector3.Distance(before[i],p);if(distance>error){error=distance;worst=tracked[i].name+"/"+state.Enemy+"/"+state.EnemyAge.ToString("F3");}
                    }
                    foreach(string side in new[]{"L","R"})
                    {
                        Vector3 center=Vector3.zero;foreach(string finger in new[]{"index","middle","ring","pinky"})center+=Bone(enemy.Root,"bip_"+finger+"_0_"+side).position;
                        var wrist=Bone(enemy.Root,"bip_hand_"+side);var lower=Bone(enemy.Root,"bip_lowerArm_"+side);
                        bend=Mathf.Max(bend,Vector3.Angle(center/4-wrist.position,wrist.position-lower.position));
                    }
                }
                string line=$"{rate}Hz {mode} zeroTimeError={error:F6} worst={worst} wristBend={bend:F3} contacts={hits} beams={beams} blocks={state.Blocks} hurt={state.HitsTaken} pause={pause} resume={resume} reset={reset}";
                Debug.Log("[ClawStability] "+line);
                if(error>.0001f||bend>36||mode=="exchange"&&(state.Blocks!=1||state.HitsTaken<1)||mode=="strikes"&&(state.Punches!=16||beams!=1)||mode=="pause"&&(!pause||!resume)||mode=="new-round"&&!reset)
                    throw new Exception("Claw flow failed: "+line);
                report.AppendLine(line+" passed");
            }
            File.WriteAllText(Path.Combine(folder,"stability.txt"),report.ToString());
        }
        static void Render(string version)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(926);
            var world=new GameWorld();var state=new Battle();
            var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);
            var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            Transform[] hands={Bone(enemy.Root,"bip_hand_L"),Bone(enemy.Root,"bip_hand_R")};
            Transform[] lower={Bone(enemy.Root,"bip_lowerArm_L"),Bone(enemy.Root,"bip_lowerArm_R")};
            var roots=new Transform[2,4];string[] fingers={"index","middle","ring","pinky"};
            for(int s=0;s<2;s++)for(int f=0;f<4;f++)roots[s,f]=Bone(enemy.Root,"bip_"+fingers[f]+"_0_"+(s==0?"L":"R"));
            var args=Environment.GetCommandLineArgs();int outputAt=Array.IndexOf(args,"--claw-output");
            string output=outputAt>=0&&outputAt+1<args.Length?args[outputAt+1]:Path.Combine(Application.dataPath,"../../artifacts/claw-alignment");
            string folder=Path.GetFullPath(Path.Combine(output,version));
            Directory.CreateDirectory(folder+"/frames");File.Delete(folder+"/validation.txt");
            var sources=new StringBuilder("Rendered UTC: "+DateTime.UtcNow.ToString("O")+"\nUnity: "+Application.unityVersion+"\n");
            using(var sha=System.Security.Cryptography.SHA256.Create())
                foreach(string path in new[]{"Scripts/Runtime/RiggedActor.cs","Resources/Characters/Golza/Golza.fbx","Editor/ClawPoseReview.cs"})
                    sources.AppendLine(path+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Application.dataPath,path)))).Replace("-","").ToLowerInvariant());
            File.WriteAllText(folder+"/render-source.txt",sources.ToString());
            var csv=new StringBuilder("frame,phase,attack,age,side,wristBend,forwardDot,palmUp,handX,handY,handZ\n");
            var rt=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
            state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});for(int i=0;i<(Battle.TransformationSeconds+0.2f)/(.02f);i++)state.Tick(.02f,new PlayerInput{Tracking=true});while(state.TryCue(out _)){}
            float maxBend=0,minForward=1,maxHandStep=0;int image=0;Vector3[] previous=new Vector3[2];
            var markers=new System.Collections.Generic.HashSet<string>();
            try
            {
                for(int frame=0;frame<1900;frame++)
                {
                    const float dt=1/60f;float time=frame*dt;
                    state.Tick(world.BattleDelta(dt,state),new PlayerInput{Tracking=true,Shield=true});
                    while(state.TryCue(out var cue))world.Cue(cue,state);
                    hero.Update(state,world.Camera,dt,time);enemy.Update(state,world.Camera,dt,time);world.Tick(state,dt,time);
                    for(int s=0;s<2;s++)
                    {
                        Vector3 center=Vector3.zero;for(int f=0;f<4;f++)center+=roots[s,f].position;center/=4;
                        Vector3 palm=(center-hands[s].position).normalized,fore=(hands[s].position-lower[s].position).normalized;
                        float bend=Vector3.Angle(palm,fore),dot=Vector3.Dot(palm,-world.BattleAxis);
                        Vector3 normal=Vector3.Cross(palm,roots[s,0].position-roots[s,3].position).normalized*(s==0?1:-1);
                        if(frame>0)maxHandStep=Mathf.Max(maxHandStep,Vector3.Distance(previous[s],hands[s].position));previous[s]=hands[s].position;
                        maxBend=Mathf.Max(maxBend,bend);minForward=Mathf.Min(minForward,dot);var h=hands[s].position;
                        csv.AppendLine(FormattableString.Invariant($"{frame},{state.Enemy},{state.EnemyAttackCount},{state.EnemyAge:F4},{s},{bend:F3},{dot:F4},{normal.y:F4},{h.x:F5},{h.y:F5},{h.z:F5}"));
                    }
                    bool selected=frame<30||state.Enemy==EnemyPhase.Attack||
                        (state.Enemy==EnemyPhase.Windup&&state.EnemyAge>state.WarningDuration-.5f)||
                        (state.Enemy==EnemyPhase.Recover&&state.EnemyAge<.5f);
                    if(selected)
                    {
                        Vector3 savedPosition=world.Camera.transform.position;Quaternion savedRotation=world.Camera.transform.rotation;
                        float savedFov=world.Camera.fieldOfView;int savedMask=world.Camera.cullingMask;
                        CameraClearFlags savedClear=world.Camera.clearFlags;Color savedColor=world.Camera.backgroundColor;
                        hero.Root.gameObject.SetActive(false);
                        var forward=-world.BattleAxis;var side=Vector3.Cross(Vector3.up,forward);
                        world.Camera.transform.position=enemy.Root.position+forward*5.0f-side*1.6f+Vector3.up*2.8f;
                        world.Camera.transform.LookAt(enemy.Root.position+Vector3.up*2.30f);world.Camera.fieldOfView=32;
                        world.Camera.cullingMask=1<<ContactShadows.ActorLayer;world.Camera.clearFlags=CameraClearFlags.SolidColor;
                        world.Camera.backgroundColor=new Color(.055f,.075f,.11f);
                        if(frame%2==0)CharacterReview.Save(world.Camera,rt,$"{folder}/frames/{image++:D4}.png");
                        string key=state.Enemy+"-"+state.EnemyAttackCount;
                        if(state.Enemy==EnemyPhase.Attack&&state.EnemyAge<.37f)key=null;
                        if(key!=null&&markers.Add(key))CharacterReview.Save(world.Camera,rt,folder+"/"+key+".png");
                        hero.Root.gameObject.SetActive(true);
                        world.Camera.transform.SetPositionAndRotation(savedPosition,savedRotation);world.Camera.fieldOfView=savedFov;
                        world.Camera.cullingMask=savedMask;world.Camera.clearFlags=savedClear;world.Camera.backgroundColor=savedColor;
                    }
                    if(state.EnemyAttackCount>=2&&state.Enemy==EnemyPhase.Recover&&state.EnemyAge>.5f)break;
                }
                File.WriteAllText(folder+"/motion.csv",csv.ToString());
                string report=$"{version}: attacks={state.EnemyAttackCount} blocks={state.Blocks} maxWristBend={maxBend:F2} minForwardDot={minForward:F3} maxHandStep={maxHandStep:F4} frames={image}";
                Debug.Log("[ClawPoseReview] "+report);
                if(state.EnemyAttackCount!=2||state.Blocks!=2||image<100||maxHandStep>.7f)throw new Exception("Claw sequence incomplete or discontinuous: "+report);
                if(version=="after"&&maxBend>36)throw new Exception("Blended claw still folds back over its forearm: "+report);
                File.WriteAllText(folder+"/validation.txt",report);
            }
            finally{world.Camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
        }
    }
}
