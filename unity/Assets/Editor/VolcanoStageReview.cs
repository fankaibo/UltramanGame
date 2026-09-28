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
    public static class VolcanoStageReview
    {
        static string OutputRoot
        {
            get
            {
                var args=Environment.GetCommandLineArgs();
                int at=Array.IndexOf(args,"--volcano-output");
                return at>=0&&at+1<args.Length?Path.GetFullPath(args[at+1]):Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/volcano-volume"));
            }
        }
        public static void Before()=>Render("before");
        public static void After()=>Render("after");
        public static void Release(){After();VolumeChecks();}
        public static void VolumeChecks()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            string folder=OutputRoot;Directory.CreateDirectory(folder+"/inspection");File.Delete(folder+"/volume-validation.txt");
            var camera=new GameObject("Volume inspection camera").AddComponent<Camera>();camera.enabled=false;
            camera.depthTextureMode=DepthTextureMode.Depth;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.10f,.14f);
            camera.transform.position=new Vector3(0,2.8f,-7);camera.transform.LookAt(new Vector3(0,2.8f,0));camera.fieldOfView=45;camera.aspect=1;
            var volume=GameObject.CreatePrimitive(PrimitiveType.Cube);volume.name="Volume depth inspection";volume.transform.position=new Vector3(0,2.8f,0);volume.transform.localScale=new Vector3(3.8f,5.6f,3.2f);
            var smoke=new Material(Resources.Load<Shader>("VolcanicPlume"));smoke.SetFloat("_Clock",7.1f);smoke.SetFloat("_Surge",.8f);
            var densityNoise=VolcanoStage.CreatePlumeNoise();smoke.SetTexture("_Noise",densityNoise);
            var renderer=volume.GetComponent<Renderer>();renderer.sharedMaterial=smoke;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.transform.position=new Vector3(0,2.8f,-2.5f);blocker.transform.localScale=new Vector3(8,8,.3f);
            var opaque=new Material(Shader.Find("Standard"));opaque.color=new Color(.25f,.4f,.6f);blocker.GetComponent<Renderer>().sharedMaterial=opaque;blocker.SetActive(false);
            var target=new RenderTexture(256,256,24);target.Create();camera.targetTexture=target;var pixels=new Texture2D(256,256,TextureFormat.RGB24,false);var report=new StringBuilder();
            Color32[] Read(string name)
            {camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,256,256),0,0);pixels.Apply();File.WriteAllBytes(folder+"/inspection/"+name+".png",pixels.EncodeToPNG());return pixels.GetPixels32();}
            int Changed(Color32[] a,Color32[] b,int threshold)
            {int changed=0;for(int i=0;i<a.Length;i++)if(Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b)>threshold)changed++;return changed;}
            try
            {
                renderer.enabled=false;var clear=Read("clear");renderer.enabled=true;var visible=Read("visible");var repeat=Read("repeat");
                int contribution=Changed(clear,visible,9);if(contribution<2000||Changed(visible,repeat,0)!=0)throw new Exception("Smoke missing or changing without time");
                smoke.SetFloat("_Clock",8.1f);int animated=Changed(visible,Read("later"),6);if(animated<800)throw new Exception("Smoke is static");
                blocker.SetActive(true);renderer.enabled=false;var blocked=Read("opaque-front");renderer.enabled=true;
                int leaked=Changed(blocked,Read("opaque-front-smoke"),1);if(leaked!=0)throw new Exception("Smoke draws through foreground geometry: "+leaked);
                blocker.SetActive(false);camera.transform.position=new Vector3(0,2.8f,0);camera.transform.rotation=Quaternion.identity;
                renderer.enabled=false;clear=Read("inside-clear");renderer.enabled=true;int inside=Changed(clear,Read("inside-smoke"),9);if(inside<2000)throw new Exception("Volume disappears when camera enters it");
                report.AppendLine($"GPU: visiblePixels={contribution} animatedPixels={animated} foregroundLeakPixels={leaked} insidePixels={inside} zeroTime=passed");
                File.WriteAllText(folder+"/volume-validation.txt",report.ToString());Debug.Log("[VolcanoVolumeChecks] "+report);
            }
            finally {camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(smoke);UnityEngine.Object.DestroyImmediate(opaque);UnityEngine.Object.DestroyImmediate(densityNoise);}
        }
        static void Render(string version)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(929);
            string folder=Path.Combine(OutputRoot,version);Directory.CreateDirectory(folder+"/frames");
            File.Delete(folder+"/validation.txt");
            foreach(string name in new[]{"VolcanoGround","VolcanicPlume","VolcanicLava"})
            {var shader=Resources.Load<Shader>(name);if(!shader||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new Exception("Invalid stage shader "+name);}
            foreach(string file in Directory.GetFiles(Path.Combine(Application.dataPath,"Resources/Art/Basalt"),"*.jpg"))
            {
                string path="Assets"+file.Substring(Application.dataPath.Length);
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                bool normal=file.Contains("_nor_gl_"),data=normal||file.Contains("_arm_");
                if(!texture||texture.width!=2048||texture.height!=2048||importer.sRGBTexture==data||!importer.mipmapEnabled||importer.wrapMode!=TextureWrapMode.Repeat||normal&&importer.textureType!=TextureImporterType.NormalMap)
                    throw new Exception("Invalid scanned surface import "+path);
            }
            var world=new GameWorld();var state=new Battle();
            var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
            for(int f=0;f<1000&&state.Enemy!=EnemyPhase.Windup;f++)state.Tick(1/60f,new PlayerInput{Tracking=true});while(state.TryCue(out _)){}
            var target=new RenderTexture(1280,720,24){antiAliasing=4};target.Create();world.Camera.targetTexture=target;world.Camera.aspect=16f/9;
            var samples=new StringBuilder("frame,enemy,age,health,blocks,hurt\n");
            try
            {
                for(int frame=0;frame<480;frame++)
                {
                    const float dt=1/60f;float time=frame*dt;
                    state.Tick(dt,new PlayerInput{Tracking=true,Shield=true});while(state.TryCue(out var cue))world.Cue(cue,state);
                    hero.Update(state,world.Camera,dt,time);enemy.Update(state,world.Camera,dt,time);world.Tick(state,dt,time);
                    samples.AppendLine(FormattableString.Invariant($"{frame},{state.Enemy},{state.EnemyAge:F4},{state.EnemyHealth},{state.Blocks},{state.HitsTaken}"));
                    if(frame%2==0)CharacterReview.Save(world.Camera,target,$"{folder}/frames/{frame/2:D4}.png");
                    if(frame==180||frame==420)CharacterReview.Save(world.Camera,target,$"{folder}/battle-{frame}.png");
                }
                File.WriteAllText(folder+"/sequence.csv",samples.ToString());
                var stage=UnityEngine.Object.FindFirstObjectByType<VolcanoStage>();
                // Inspect the same world-space vent from three directions. This
                // camera is only for reviewing volume and crust, never gameplay.
                var pivot=new Vector3(5.3f,1.7f,13.5f);
                for(int angle=-25;angle<=25;angle+=25)
                {
                    world.Camera.transform.position=pivot+Quaternion.Euler(0,angle,0)*new Vector3(0,.5f,-6.5f);
                    world.Camera.transform.LookAt(pivot);world.Camera.fieldOfView=43;stage.Tick(7.1f);
                    CharacterReview.Save(world.Camera,target,$"{folder}/vent-{angle+25}.png");
                }
                CharacterReview.Save(world.Camera,target,folder+"/repeat-a.png");stage.Tick(7.1f);CharacterReview.Save(world.Camera,target,folder+"/repeat-b.png");
                if(!Equal(File.ReadAllBytes(folder+"/repeat-a.png"),File.ReadAllBytes(folder+"/repeat-b.png")))throw new Exception("Frozen stage changes without time");
                if(state.EnemyHealth!=50||state.HitsTaken!=0||state.Blocks!=1)throw new Exception("Stage capture changed battle result");
                var files=new System.Collections.Generic.List<string>{"Scripts/Runtime/VolcanoStage.cs","Scripts/Runtime/GameWorld.cs","Resources/VolcanoGround.shader","Resources/VolcanicPlume.shader","Resources/VolcanicLava.shader","Editor/VolcanoStageReview.cs","Editor/BasaltTextureImport.cs"};
                foreach(string file in Directory.GetFiles(Path.Combine(Application.dataPath,"Resources/Art/Basalt")))files.Add(file.Substring(Application.dataPath.Length+1));
                var sources=new StringBuilder();using(var sha=System.Security.Cryptography.SHA256.Create())
                    foreach(string file in files)
                    {string path=Path.Combine(Application.dataPath,file);if(File.Exists(path))sources.AppendLine(file+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant());}
                File.WriteAllText(folder+"/sources.txt",sources.ToString());
                string report=$"{version}: frames=240 samples=480 ventViews=3 repeat=passed health={state.EnemyHealth} blocks={state.Blocks} hurt={state.HitsTaken}";
                File.WriteAllText(folder+"/validation.txt",report);Debug.Log("[VolcanoStageReview] "+report);
            }
            finally {world.Camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);}
        }
        static bool Equal(byte[] a,byte[] b){if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
    }
}
