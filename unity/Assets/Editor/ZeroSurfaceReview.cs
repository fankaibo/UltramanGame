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
    public static class ZeroSurfaceReview
    {
        const string Keyword="_AUTHORED_SURFACE";
        static string Folder=>Path.GetFullPath(Application.dataPath+"/../../artifacts/zero-authored-surface-20261010");
        public static void Before()=>Run(false);
        public static void After()=>Run(true);
        static void Run(bool after)
        {
            string folder=Folder+(after?"/after":"/before");Directory.CreateDirectory(folder);
            var report=new StringBuilder();var bones=new StringBuilder();
            var shader=Resources.Load<Shader>("HeroSurface");
            if(!shader||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new Exception("Hero shader invalid");
            foreach(string id in new[]{"Zero","Tiga","Mebius","Grigio","Geed"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(1075);
                var world=new GameWorld();var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);
                var state=new Battle();hero.Update(state,world.Camera,0,0,0);
                var materials=hero.Root.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Distinct().ToArray();
                var mapped=materials.Where(m=>m.IsKeywordEnabled(Keyword)).ToArray();
                int expected=after&&id=="Zero"?3:0;
                if(mapped.Length!=expected)throw new Exception(id+" authored map count="+mapped.Length+" expected="+expected);
                foreach(var mat in mapped)
                {
                    var normal=(Texture2D)mat.GetTexture("_AuthoredNormal");var spec=(Texture2D)mat.GetTexture("_AuthoredSpecular");
                    foreach(var map in new[]{normal,spec})
                    {
                        var import=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(map));
                        if(!map||map.width!=2048||map.height!=2048||import.sRGBTexture||!import.mipmapEnabled||import.isReadable)
                            throw new Exception("Invalid linear 2K surface data");
                        if(map==normal&&(import.textureType!=TextureImporterType.NormalMap||import.flipGreenChannel))
                            throw new Exception("Normal convention must be converted once in source decoder");
                    }
                }
                foreach(var skin in hero.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                    if(skin.sharedMaterials.Any(m=>mapped.Contains(m))&&skin.sharedMesh.tangents.Length!=skin.sharedMesh.vertexCount)
                        throw new Exception("Mapped mesh missing tangent basis");
                var cam=new GameObject("Original surface review").AddComponent<Camera>();cam.enabled=false;cam.allowHDR=true;
                cam.cullingMask=1<<ContactShadows.ActorLayer;cam.clearFlags=CameraClearFlags.SolidColor;
                cam.backgroundColor=new Color(.025f,.035f,.05f);cam.fieldOfView=32;cam.aspect=4f/3;cam.renderingPath=RenderingPath.Forward;
                var rt=new RenderTexture(960,720,24,RenderTextureFormat.ARGBHalf){antiAliasing=4};rt.Create();cam.targetTexture=rt;
                bool fog=RenderSettings.fog;RenderSettings.fog=false;
                void Aim(float angle,float distance=5.8f)
                {cam.transform.position=world.HeroHome+Quaternion.AngleAxis(angle,Vector3.up)*world.BattleAxis*distance+Vector3.up*2.5f;cam.transform.LookAt(world.HeroHome+Vector3.up*1.95f);}
                try
                {
                    foreach(int pose in new[]{0,3,4,7})
                    {
                        hero.Update(state,world.Camera,0,0,pose);
                        foreach(var bone in hero.Root.GetComponentsInChildren<Transform>())
                            bones.AppendLine(id+","+pose+","+bone.name+","+bone.position.ToString("F5")+","+bone.rotation.ToString("F5"));
                        foreach(int angle in new[]{20,160})
                        {Aim(angle);CharacterReview.Save(cam,rt,$"{folder}/{id}-{pose}-{angle}.png");}
                    }
                    if(id!="Zero")continue;
                    hero.Update(state,world.Camera,0,0,0);Aim(20,4.2f);
                    CharacterReview.Save(cam,rt,folder+"/Zero-detail.png");
                    if(after)
                    {
                        cam.Render();cam.Render();var on=Read(cam,rt);var repeat=Read(cam,rt);
                        foreach(var mat in mapped)mat.DisableKeyword(Keyword);
                        var off=Read(cam,rt);float delta=Difference(on,off),repeatError=Difference(on,repeat);
                        if(delta<.0005f||delta>.15f||repeatError>.000001f)throw new Exception($"Missing/unstable surface: delta={delta} repeat={repeatError}");
                        foreach(var mat in materials)if(mat.HasProperty("_EmissionAudit"))mat.SetFloat("_EmissionAudit",1);
                        var e0=Read(cam,rt);foreach(var mat in mapped)mat.EnableKeyword(Keyword);var e1=Read(cam,rt);
                        float emission=Difference(e0,e1);if(emission>.000001f)throw new Exception("Lens/guard/rim emission changed");
                        foreach(var mat in materials)if(mat.HasProperty("_EmissionAudit"))mat.SetFloat("_EmissionAudit",0);
                        report.AppendLine($"Zero: maps={mapped.Length} imageDifference={delta:F8} repeatError={repeatError:F8} emissionError={emission:F8}");
                    }
                    Directory.CreateDirectory(folder+"/frames");
                    for(int f=0;f<120;f++){Aim(-20+f*1.8f,4.8f);CharacterReview.Save(cam,rt,$"{folder}/frames/{f:D4}.png");}
                }
                finally{RenderSettings.fog=fog;cam.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
            }
            File.WriteAllText(folder+"/bones.csv",bones.ToString());
            if(after&&File.ReadAllText(Folder+"/before/bones.csv")!=bones.ToString())throw new Exception("Source surface changed bones");
            File.WriteAllText(folder+"/validation.txt",report.ToString());
            Debug.Log("[ZeroSurfaceReview] "+(after?"after":"before")+" passed\n"+report);
        }
        static Color[] Read(Camera camera,RenderTexture target)
        {
            camera.Render();RenderTexture.active=target;
            var image=new Texture2D(target.width,target.height,TextureFormat.RGBAFloat,false,true);
            image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();var p=image.GetPixels();
            UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=null;return p;
        }
        static float Difference(Color[] a,Color[] b)
        {double sum=0;for(int i=0;i<a.Length;i++)sum+=Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b);return (float)(sum/a.Length);}
    }
}
