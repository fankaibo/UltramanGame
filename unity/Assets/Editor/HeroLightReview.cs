using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class HeroLightReview
    {
        public static void Release()
        {HeroMeshReview.After();Validate();GuardImpactReview.Render();GuardImpactReview.LightInterruptions();}
        public static void Validate()
        {
            var shader=Resources.Load<Shader>("HeroSurface");
            if(!shader||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new Exception("Hero lighting shader failed");
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/hero-lights/emission"));
            Directory.CreateDirectory(folder);File.Delete(folder+"/validation.txt");var report=new StringBuilder();
            foreach(string id in new[]{"Grigio","Geed"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var world=new GameWorld();var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);
                var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                enemy.Root.gameObject.SetActive(false);hero.Update(new Battle(),world.Camera,0,0,0);
                var materials=hero.Root.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Distinct().ToArray();
                var mat=materials.Single(m=>m.HasProperty("_AtlasLights")&&m.GetFloat("_AtlasLights")>.5f);
                var camera=new GameObject("Hero lens inspection").AddComponent<Camera>();camera.enabled=false;camera.allowHDR=true;
                camera.cullingMask=1<<ContactShadows.ActorLayer;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
                camera.aspect=1;camera.fieldOfView=30;camera.renderingPath=RenderingPath.Forward;
                camera.transform.position=world.HeroHome+world.BattleAxis*5.8f+Vector3.up*2.7f;
                camera.transform.LookAt(world.HeroHome+Vector3.up*2.7f);
                var target=new RenderTexture(512,512,24,RenderTextureFormat.ARGBHalf){antiAliasing=4};target.Create();camera.targetTexture=target;
                bool fog=RenderSettings.fog;RenderSettings.fog=false;
                try
                {
                    float eye=mat.GetFloat("_EyeRadiance"),core=mat.GetFloat("_CoreRadiance");
                    mat.SetFloat("_EmissionAudit",1);mat.SetFloat("_CoreRadiance",0);
                    var eyes=Read(camera,target,folder+"/"+id+"-eyes");
                    mat.SetFloat("_EyeRadiance",0);mat.SetFloat("_CoreRadiance",core);
                    var chest=Read(camera,target,folder+"/"+id+"-core");
                    mat.SetFloat("_CoreRadiance",0);var off=Read(camera,target,null);
                    if(Energy(off)>.001)throw new Exception("Body emits outside configured lenses: "+id);
                    var head=Bone(hero,"bip_head");
                    var shoulders=(Bone(hero,"bip_upperArm_L").position+Bone(hero,"bip_upperArm_R").position)*.5f;
                    int eyePixels=CheckRegion(eyes,Bounds(camera,head.position,.48f,-.1f,.60f),id+" eyes");
                    int corePixels=CheckRegion(chest,Bounds(camera,shoulders,.65f,-.65f,.23f),id+" core");
                    var e=Channels(eyes);var c=Channels(chest);
                    if((id=="Grigio"?e.x<=e.z*1.05:e.z<=e.x*1.1)||c.z<=c.x*1.1)
                        throw new Exception("Original warm/cool lens colors were lost: "+id);
                    // Keep the inspection pose fixed while exercising real game
                    // phases, so measured radiance is not confused with occlusion.
                    var state=new Battle(200);state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
                    for(int f=0;f<120;f++)state.Tick(.02f,new PlayerInput{Tracking=true});state.GiveInstructionTime(20);
                    for(int n=0;n<15;n++)
                    {state.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});for(int f=0;f<25;f++)state.Tick(.02f,new PlayerInput{Tracking=true});}
                    if(state.Energy!=15)throw new Exception("Light review did not charge the actual battle");
                    state.Tick(.02f,new PlayerInput{Tracking=true,Beam=true});for(int f=0;f<20;f++)state.Tick(.02f,new PlayerInput{Tracking=true});
                    hero.Update(state,world.Camera,0,0,0);mat.SetFloat("_EyeRadiance",0);
                    var charged=Read(camera,target,folder+"/"+id+"-charged-core");
                    double ratio=Energy(charged)/Energy(chest);
                    if(ratio<1.5)throw new Exception("Beam charge did not brighten the core: "+id);
                    hero.Update(state,world.Camera,0,0,0);mat.SetFloat("_EyeRadiance",0);
                    if(Math.Abs(Energy(Read(camera,target,null))-Energy(charged))>.001)throw new Exception("Zero-time sample changed lens light");
                    state.Pause();hero.Update(state,world.Camera,0,0,0);mat.SetFloat("_EyeRadiance",0);
                    if(Math.Abs(Energy(Read(camera,target,null))-Energy(chest))>.001)throw new Exception("Pause retained charge light");
                    hero.Update(new Battle(),world.Camera,0,0,0);mat.SetFloat("_EyeRadiance",0);
                    if(Math.Abs(Energy(Read(camera,target,null))-Energy(chest))>.001)throw new Exception("New round retained charge light");
                    mat.SetFloat("_EyeRadiance",eye);mat.SetFloat("_CoreRadiance",core);
                    report.AppendLine(FormattableString.Invariant($"{id}: eyePixels={eyePixels} corePixels={corePixels} bounded=passed originalColor=passed chargeEnergyRatio={ratio:F3} zeroTime=passed pause=passed newRound=passed"));
                }
                finally
                {mat.SetFloat("_EmissionAudit",0);RenderSettings.fog=fog;camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);}
            }
            File.WriteAllText(folder+"/validation.txt",report.ToString());Debug.Log("[HeroLightReview] "+report);
        }
        static Transform Bone(AnimatedActor actor,string name)=>actor.Root.GetComponentsInChildren<Transform>().Single(t=>t.name==name);
        static Rect Bounds(Camera camera,Vector3 center,float width,float bottom,float top)
        {
            var a=camera.WorldToViewportPoint(center-camera.transform.right*width+Vector3.up*bottom);
            var b=camera.WorldToViewportPoint(center+camera.transform.right*width+Vector3.up*top);
            return Rect.MinMaxRect(a.x,a.y,b.x,b.y);
        }
        static Color[] Read(Camera camera,RenderTexture target,string path)
        {
            camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;
            var image=new Texture2D(512,512,TextureFormat.RGBAFloat,false,true);
            try
            {
                image.ReadPixels(new Rect(0,0,512,512),0,0);image.Apply();var pixels=image.GetPixels();
                if(path!=null)
                {var png=new Texture2D(512,512,TextureFormat.RGBA32,false,true);png.SetPixels(pixels);png.Apply();File.WriteAllBytes(path+".png",png.EncodeToPNG());UnityEngine.Object.DestroyImmediate(png);}
                return pixels;
            }
            finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(image);}
        }
        static Vector3 Channels(Color[] pixels)
        {var sum=Vector3.zero;foreach(var c in pixels)sum+=new Vector3(c.r,c.g,c.b);return sum;}
        static double Energy(Color[] pixels)
        {double sum=0;foreach(var c in pixels)sum+=c.r+c.g+c.b;return sum;}
        static int CheckRegion(Color[] pixels,Rect region,string name)
        {
            double outside=0,total=0;int count=0;
            for(int i=0;i<pixels.Length;i++)
            {
                var c=pixels[i];double energy=c.r+c.g+c.b;total+=energy;
                if(c.maxColorComponent>.02)count++;
                if(!region.Contains(new Vector2((i%512+.5f)/512,(i/512+.5f)/512)))outside+=energy;
            }
            Debug.Log($"[HeroLensRegion] {name} pixels={count} outside={outside/Math.Max(total,.0001):F6} bounds={region}");
            if(count<25||outside/Math.Max(total,.0001)>.01)throw new Exception("Light invisible or outside its anatomical region: "+name);
            return count;
        }
    }
}
