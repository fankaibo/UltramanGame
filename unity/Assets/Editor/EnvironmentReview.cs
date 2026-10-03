using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class EnvironmentReview
    {
        public static void Release()
        {Validate();HeroMaterialReview.After();CinematicFlashReview.HighlightShoulder();CinematicFlashReview.Run();GuardImpactReview.Render();}
        public static void Validate()
        {
            Inspect();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/hero-environment/inspection"));
            var env=VolcanoEnvironment.Create(null);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.black;RenderSettings.fog=false;
            var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var material=new Material(Resources.Load<Shader>("HeroSurface"));material.color=new Color(.7f,.7f,.7f);
            material.SetFloat("_Metallic",1);material.SetFloat("_Glossiness",.45f);sphere.GetComponent<Renderer>().sharedMaterial=material;
            var camera=new GameObject("Environment inspection").AddComponent<Camera>();camera.enabled=false;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            camera.transform.position=new Vector3(0,0,-2);camera.transform.LookAt(Vector3.zero);camera.fieldOfView=40;camera.aspect=1;
            var rt=new RenderTexture(256,256,24);rt.Create();camera.targetTexture=rt;
            var image=new Texture2D(256,256,TextureFormat.RGB24,false);var report=new StringBuilder();
            Color32[] Read(string name)
            {camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,256,256),0,0);image.Apply();File.WriteAllBytes(folder+"/"+name+".png",image.EncodeToPNG());return image.GetPixels32();}
            try
            {
                var on=Read("reflection-on");var repeat=Read("reflection-repeat");RenderSettings.customReflectionTexture=null;var off=Read("reflection-off");
                int changed=0;double difference=0;
                for(int i=0;i<on.Length;i++)
                {
                    if(!on[i].Equals(repeat[i]))throw new Exception("Static environment flickers without time/input");
                    int d=Math.Abs(on[i].r-off[i].r)+Math.Abs(on[i].g-off[i].g)+Math.Abs(on[i].b-off[i].b);
                    if(d>12)changed++;difference+=d/(3.0*255*on.Length);
                }
                if(changed<5000||difference<.02)throw new Exception("Environment does not illuminate the real hero shader");
                report.AppendLine($"GPU material contribution: changedPixels={changed} meanChannelDelta={difference:F6} zeroTime=passed");
                RenderSettings.customReflectionTexture=env.Reflection;
                UnityEngine.Object.DestroyImmediate(env.gameObject);
                var sentinel=new Cubemap(16,TextureFormat.RGBAHalf,false);
                try
                {
                    RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.customReflectionTexture=sentinel;RenderSettings.reflectionIntensity=.37f;
                    for(int round=0;round<3;round++)
                    {
                        env=VolcanoEnvironment.Create(null);var owned=env.Reflection;
                        if(!owned||owned==sentinel||!owned.IsCreated())throw new Exception("Round did not create an independent environment");
                        UnityEngine.Object.DestroyImmediate(env.gameObject);
                        if(owned||RenderSettings.customReflectionTexture!=sentinel||RenderSettings.defaultReflectionMode!=DefaultReflectionMode.Custom||Mathf.Abs(RenderSettings.reflectionIntensity-.37f)>.0001f)
                            throw new Exception($"Environment release did not restore previous reflection: alive={(bool)owned} textureMatch={RenderSettings.customReflectionTexture==sentinel} mode={RenderSettings.defaultReflectionMode} intensity={RenderSettings.reflectionIntensity}");
                    }
                    env=VolcanoEnvironment.Create(null);RenderSettings.customReflectionTexture=sentinel;RenderSettings.reflectionIntensity=.62f;
                    UnityEngine.Object.DestroyImmediate(env.gameObject);
                    if(RenderSettings.customReflectionTexture!=sentinel||Mathf.Abs(RenderSettings.reflectionIntensity-.62f)>.0001f)throw new Exception("Cleanup overwrote a newer environment owner");
                }
                finally {RenderSettings.customReflectionTexture=null;RenderSettings.defaultReflectionMode=DefaultReflectionMode.Skybox;UnityEngine.Object.DestroyImmediate(sentinel);}
                report.AppendLine("Lifecycle: three create/destroy cycles, previous texture/mode/intensity restoration, newer owner preservation passed");
                File.WriteAllText(folder+"/validation.txt",report.ToString());Debug.Log("[EnvironmentReview] "+report);
            }
            finally{camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(material);}
        }
        public static void Inspect()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/hero-environment/inspection"));Directory.CreateDirectory(folder);
            var world=new GameWorld();var env=UnityEngine.Object.FindFirstObjectByType<VolcanoEnvironment>();
            if(!env||!env.Reflection||RenderSettings.customReflectionTexture!=env.Reflection)throw new Exception("Arena environment not bound");
            if(ShaderUtil.ShaderHasError(Resources.Load<Shader>("VolcanoReflection")))throw new Exception("Reflection shader compilation failed");
            var rt=env.Reflection;var report=new StringBuilder();var previous=RenderTexture.active;
            var baseMeans=new Color[6];
            try
            {
                for(int mip=0;mip<rt.mipmapCount;mip++)
                {
                    int size=Math.Max(1,rt.width>>mip);var image=new Texture2D(size,size,TextureFormat.RGBAFloat,false,true);
                    var request=AsyncGPUReadback.Request(rt,mip,TextureFormat.RGBAFloat);request.WaitForCompletion();
                    if(request.hasError||request.layerCount!=6)throw new Exception("Cubemap GPU readback failed");
                    for(int face=0;face<6;face++)
                    {
                        var data=request.GetData<Color>(face).ToArray();
                        if(data.Length!=size*size)throw new Exception("Wrong mip readback dimensions");
                        image.SetPixels(data);image.Apply();
                        var center=image.GetPixel(size/2,size/2);var mean=Color.clear;
                        foreach(var color in data)
                        {if(float.IsNaN(color.r+color.g+color.b)||float.IsInfinity(color.r+color.g+color.b))throw new Exception("Invalid environment pixel");mean+=color/(size*size);}
                        if(mean.r+mean.g+mean.b<.1f)throw new Exception("Unlit cubemap face");
                        if(mip==0)baseMeans[face]=mean;
                        else if(Vector4.Distance(baseMeans[face],mean)>.004f)throw new Exception("Reflection mip brightness changed unexpectedly");
                        report.AppendLine($"mip={mip} face={(CubemapFace)face} center={center} mean={mean}");
                        if(mip==0)File.WriteAllBytes(folder+"/"+(CubemapFace)face+".png",image.EncodeToPNG());
                    }
                    UnityEngine.Object.DestroyImmediate(image);
                }
                File.WriteAllText(folder+"/faces.txt",report.ToString());Debug.Log("[EnvironmentReview]\n"+report);
            }
            finally{RenderTexture.active=previous;}
        }
    }
}
