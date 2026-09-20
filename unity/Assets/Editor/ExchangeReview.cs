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
    // Identical real-runtime exchanges, including left/right punches and both
    // monster claws. The two folders permit direct comparison after an edit.
    public static class ExchangeReview
    {
        [MenuItem("UltramanGame/Exchange review/Save before")]
        public static void Before() => Render("before");
        [MenuItem("UltramanGame/Exchange review/Save after")]
        public static void After() => Render("after");
        [MenuItem("UltramanGame/Exchange review/Verify active pause and reset")]
        public static void Interruptions()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var world=new GameWorld();var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);
            var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            foreach(bool pause in new[]{true,false})
            {
                var state=new Battle();state.Tick(.01f,new PlayerInput{Tracking=true,Transform=true});
                for(int i=0;i<140;i++)state.Tick(.02f,new PlayerInput{Tracking=true});
                for(int i=0;i<9;i++)
                {
                    state.Tick(1/60f,new PlayerInput{Tracking=true,LeftPunch=i==0});
                    hero.Update(state,world.Camera,1/60f,i/60f);enemy.Update(state,world.Camera,1/60f,i/60f);world.Tick(state,1/60f,i/60f);
                }
                if(!world.HeroTrailVisible)throw new Exception("Pause proof did not begin with an active trajectory");
                if(pause){state.Pause();world.Tick(state,1/60f,1);}else world.ResetPresentation();
                if(world.HeroTrailVisible||world.MonsterTrailVisible)throw new Exception("Interrupt left visible motion history");
                if(pause)
                {
                    for(int i=0;i<85;i++)
                    {
                        state.Tick(1/60f,new PlayerInput{Tracking=true});
                        hero.Update(state,world.Camera,1/60f,i/60f);enemy.Update(state,world.Camera,1/60f,i/60f);world.Tick(state,1/60f,i/60f);
                    }
                    if(state.Phase!=GamePhase.Battle||world.HeroTrailVisible||GameObject.Find("Impact smoke"))
                        throw new Exception("Resume replayed an old hand or foot contact effect");
                }
            }
            Debug.Log("[ExchangeInterruptions] activeTrailPause=passed activeTrailReset=passed resumeWithoutStaleDust=passed");
        }
        static void Render(string version)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            UnityEngine.Random.InitState(20260916);
            var world=new GameWorld();var state=new Battle();
            var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);
            var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);
            world.BindActors(hero,enemy);
            state.Tick(.01f,new PlayerInput{Tracking=true,Transform=true});
            for(int i=0;i<4000;i++)
            {
                state.Tick(.02f,new PlayerInput{Tracking=true});
                if(state.Enemy==EnemyPhase.Windup&&state.EnemyAge>state.WarningDuration-.8f)break;
            }
            while(state.TryCue(out _)){}
            hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);
            world.Tick(state,1,0);
            var args=Environment.GetCommandLineArgs();int outputAt=Array.IndexOf(args,"--exchange-output");
            string output=outputAt>=0&&outputAt+1<args.Length?args[outputAt+1]:Path.Combine(Application.dataPath,"../../artifacts/exchange-review");
            string folder=Path.GetFullPath(Path.Combine(output,version));
            Directory.CreateDirectory(folder+"/frames");File.Delete(folder+"/validation.txt");
            var sources=new StringBuilder("Rendered UTC: "+DateTime.UtcNow.ToString("O")+"\nUnity: "+Application.unityVersion+"\n");
            using(var sha=System.Security.Cryptography.SHA256.Create())
                foreach(string path in new[]{"Scripts/Core/ImpactTiming.cs","Resources/Characters/Tiga/Tiga.fbx","Resources/Characters/Tiga/motion.json","Scripts/Runtime/StrikeTrails.cs","Scripts/Runtime/MonsterAttackEffects.cs","Scripts/Runtime/CombatVfx.cs","Scripts/Runtime/RiggedActor.cs","Scripts/Runtime/GameWorld.cs","Scripts/Runtime/VolcanoStage.cs","Resources/EnergyShield.shader","Resources/StrikeRibbon.shader","Resources/Backdrop.shader","Resources/BackdropAtmosphere.cginc"})
                    sources.AppendLine(path+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Application.dataPath,path)))).Replace("-","").ToLowerInvariant());
            File.WriteAllText(folder+"/render-source.txt",sources.ToString());
            var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32){antiAliasing=4};target.Create();
            world.Camera.targetTexture=target;world.Camera.aspect=16f/9;
            var events=new StringBuilder("seconds,event,health,energy\n");float health=state.EnemyHealth;
            int leftWakeFrames=0,rightWakeFrames=0,rightClawFrames=0,leftClawFrames=0,visiblePixelsChecked=0;
            var pixelEvents=new System.Collections.Generic.HashSet<string>();
            Transform heroSpine=null;Quaternion punchSpineBase=Quaternion.identity;float punchBodyLead=0;
            bool punchBodyLeadPassed=false;
            if(version=="after")
            {
                heroSpine=FindBone(hero.Root,"spineLower","bip_spine_0");
                punchSpineBase=heroSpine.localRotation;
            }
            if(version=="after")
            {
                var shader=Resources.Load<Shader>("StrikeRibbon");
                if(!shader||ShaderUtil.ShaderHasError(shader))throw new Exception("Strike ribbon shader failed to compile");
            }
            try
            {
                for(int frame=0;frame<960;frame++)
                {
                    float dt=1/60f,time=frame*dt;
                    var input=new PlayerInput{Tracking=true,Shield=time<2,
                        LeftPunch=frame==156||frame==252,RightPunch=frame==204||frame==294};
                    state.Tick(world.BattleDelta(dt,state),input);
                    while(state.TryCue(out var cue))
                    {world.Cue(cue,state);events.AppendLine($"{time:F3},{cue},{state.EnemyHealth},{state.Energy}");}
                    hero.Update(state,world.Camera,dt,time);enemy.Update(state,world.Camera,dt,time);
                    if(version=="after"&&frame==155)
                        punchSpineBase=heroSpine.localRotation;
                    if(version=="after"&&frame>=156&&frame<174&&state.Action==HeroAction.LeftPunch)
                    {
                        punchBodyLead=Mathf.Max(punchBodyLead,Quaternion.Angle(punchSpineBase,heroSpine.localRotation));
                        if(punchBodyLead>=2.0f)punchBodyLeadPassed=true;
                    }
                    if(state.EnemyHealth<health)
                    {world.Hit(false,state);events.AppendLine($"{time:F3},HeroHit,{state.EnemyHealth},{state.Energy}");}
                    health=state.EnemyHealth;world.Tick(state,dt,time);
                    if(version=="after")
                    {
                        if(world.HeroTrailVisible)
                        {
                            if(state.Action==HeroAction.LeftPunch)leftWakeFrames++;
                            if(state.Action==HeroAction.RightPunch)rightWakeFrames++;
                            if(state.ActionAge>.025f&&state.ActionAge<.21f)
                                VerifyTip("Hero striking hand wake",hero.StrikeOrigin(state.Action));
                        }
                        if(world.MonsterTrailVisible)
                        {
                            if(state.EnemyAttackCount==1)rightClawFrames++;else leftClawFrames++;
                            if(state.Enemy==EnemyPhase.Attack&&state.EnemyAge>.16f&&state.EnemyAge<.54f)
                                VerifyTip("Monster moving claw 1",enemy.EnemyStrikeOrigin(state));
                        }
                        if(time>6&&time<10&&(world.HeroTrailVisible||world.MonsterTrailVisible))
                            throw new Exception("Idle exchange retained a movement trail");
                        // Contact holds change wall-clock frame indices. Check
                        // each actual lead hand at its attack phase instead of
                        // accidentally measuring an idle frame at an old index.
                        string pixelEvent=null;
                        if(world.HeroTrailVisible&&state.ActionAge>=.08f&&state.ActionAge<=.12f)
                            pixelEvent=state.Action.ToString();
                        if(world.MonsterTrailVisible&&state.Enemy==EnemyPhase.Attack&&state.EnemyAge>=.40f&&state.EnemyAge<.52f)
                            pixelEvent="Claw"+state.EnemyAttackCount;
                        if(pixelEvent!=null&&pixelEvents.Add(pixelEvent))
                        {VerifyPixels(world.Camera,target,folder,frame);visiblePixelsChecked++;}
                    }
                    if(frame%2==0)CharacterReview.Save(world.Camera,target,$"{folder}/frames/frame-{frame/2:D4}.png");
                }
                if(state.Punches!=4||state.Blocks!=1||state.HitsTaken!=1||state.EnemyAttackCount!=2)
                    throw new Exception($"Exchange mismatch: punches={state.Punches} blocks={state.Blocks} hurt={state.HitsTaken} attacks={state.EnemyAttackCount}");
                world.ResetPresentation();
                if(world.BeamVisible||world.EnemySlashVisible||world.ActiveSparkCount!=0||world.HeroTrailVisible||world.MonsterTrailVisible)
                    throw new Exception("Exchange reset retained effects");
                if(version=="after"&&(leftWakeFrames<4||rightWakeFrames<4||leftClawFrames<4||rightClawFrames<4))
                    throw new Exception("An attacking hand had no visible trajectory");
                if(version=="after"&&!punchBodyLeadPassed)
                    throw new Exception($"Hero punch did not transfer into the torso: lead={punchBodyLead:F2}deg");
                if(version=="after"&&visiblePixelsChecked!=4)
                    throw new Exception("Missing a left/right hand contact pixel check");
                File.WriteAllText(folder+"/events.csv",events.ToString());
                string result=$"[ExchangeReview] {version} passed duration=16 frames=480 leftPunches=2 rightPunches=2 blocks=1 hurt=1 alternatingClaws=2 reset=passed handTipBinding=passed heroBodyLead={punchBodyLead:F2}deg visiblePixelChecks={visiblePixelsChecked} trailFrames={leftWakeFrames}/{rightWakeFrames}/{rightClawFrames}/{leftClawFrames}";
                File.WriteAllText(folder+"/validation.txt",result);Debug.Log(result);
            }
            finally
            {world.Camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);}
        }
        static void VerifyTip(string name,Vector3 hand)
        {
            var mesh=GameObject.Find(name).GetComponent<MeshFilter>().sharedMesh;
            var v=mesh.vertices;var colors=mesh.colors;
            int newest=-1;for(int i=0;i<v.Length;i+=2)if(colors[i].a>0)newest=i;
            if(newest<0||Vector3.Distance((v[newest]+v[newest+1])*.5f,hand)>.035f)
                throw new Exception("Visible trajectory detached from the active hand: "+name);
        }
        static Transform FindBone(Transform root,params string[] names)
        {
            foreach(var t in root.GetComponentsInChildren<Transform>())
                if(Array.IndexOf(names,t.name)>=0)return t;
            throw new Exception("Missing review bone: "+string.Join("/",names));
        }
        static void VerifyPixels(Camera camera,RenderTexture target,string folder,int frame)
        {
            string[] names={"Hero striking hand wake","Monster moving claw 0","Monster moving claw 1","Monster moving claw 2"};
            var renderers=new Renderer[names.Length];var active=new bool[names.Length];
            for(int i=0;i<names.Length;i++){renderers[i]=GameObject.Find(names[i]).GetComponent<Renderer>();active[i]=renderers[i].enabled;}
            var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            try
            {
                CharacterReview.Save(camera,target,$"{folder}/visible-{frame}.png");
                RenderTexture.active=target;image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();var shown=image.GetPixels32();
                foreach(var r in renderers)r.enabled=false;
                CharacterReview.Save(camera,target,$"{folder}/hidden-{frame}.png");
                RenderTexture.active=target;image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();var hidden=image.GetPixels32();
                int changed=0;for(int i=0;i<shown.Length;i++)
                    if(Mathf.Abs(shown[i].r-hidden[i].r)+Mathf.Abs(shown[i].g-hidden[i].g)+Mathf.Abs(shown[i].b-hidden[i].b)>12)changed++;
                Debug.Log($"[TrailPixels] frame={frame} visiblePixels={changed}");
                if(changed<35)throw new Exception("Hand wake was enabled but not visibly rendered: "+frame+" pixels="+changed);
            }
            finally {for(int i=0;i<renderers.Length;i++)renderers[i].enabled=active[i];UnityEngine.Object.DestroyImmediate(image);}
        }
    }
}
