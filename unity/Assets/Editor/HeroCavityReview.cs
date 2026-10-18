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
    public static class HeroCavityReview
    {
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/hero-cavity-20261009/inspection"));
        public static void Release(){Validate();HeroMaterialReview.After();}
        public static void Validate()
        {
            Directory.CreateDirectory(Folder);File.Delete(Folder+"/validation.txt");var report=new StringBuilder();
            var shader=Resources.Load<Shader>("HeroSurface");
            if(!shader||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new Exception("Invalid hero surface shader");
            foreach(string id in new[]{"Zero","Mebius","Grigio","Tiga","Geed"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var world=new GameWorld();var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);
                hero.Update(new Battle(),world.Camera,0,0,3);
                var materials=hero.Root.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Distinct().ToArray();
                var treated=materials.Where(m=>m.HasProperty("_SurfaceCavityStrength")&&m.GetFloat("_SurfaceCavityStrength")>0).ToArray();
                int expected=id=="Zero"?3:id=="Mebius"?2:id=="Grigio"?1:0;
                if(treated.Length!=expected)throw new Exception(id+" cavity material count: "+treated.Length+" expected "+expected);
                foreach(var mat in treated)
                {
                    var map=(Texture2D)mat.GetTexture("_SurfaceCavity");
                    var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(map));
                    if(!map||map.width!=1024||map.height!=1024||importer.sRGBTexture||!importer.mipmapEnabled||importer.isReadable)
                        throw new Exception(id+" cavity must be a 1K linear mipmapped GPU texture");
                    if(mat.name.IndexOf("Eye",StringComparison.OrdinalIgnoreCase)>=0||mat.name.IndexOf("Timer",StringComparison.OrdinalIgnoreCase)>=0)
                        throw new Exception("Occlusion assigned to a separate light lens");
                }
                if(expected==0){report.AppendLine(id+": existing finish preserved, no added cavity map");continue;}
                // Freeze the evaluated geometry once, avoiding a new shadow
                // caster and submesh ordering for each numeric render.
                foreach(var skin in hero.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var mesh=new Mesh();skin.BakeMesh(mesh,true);skin.enabled=false;
                    var frozen=new GameObject("Frozen original hero",typeof(MeshFilter),typeof(MeshRenderer));
                    frozen.layer=skin.gameObject.layer;frozen.transform.SetParent(skin.transform,false);
                    frozen.GetComponent<MeshFilter>().sharedMesh=mesh;
                    frozen.GetComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;
                }
                foreach(var lamp in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))lamp.enabled=false;
                RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.65f,.65f);RenderSettings.reflectionIntensity=0;
                var camera=new GameObject("Cavity inspection camera").AddComponent<Camera>();camera.enabled=false;
                camera.cullingMask=1<<ContactShadows.ActorLayer;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
                camera.allowHDR=true;camera.renderingPath=RenderingPath.Forward;camera.fieldOfView=33;camera.aspect=1;
                camera.transform.position=world.HeroHome+world.BattleAxis*5.6f+Vector3.up*2.6f;
                camera.transform.LookAt(world.HeroHome+Vector3.up*2.1f);
                var target=new RenderTexture(512,512,24,RenderTextureFormat.ARGBHalf);target.Create();camera.targetTexture=target;
                bool fog=RenderSettings.fog;RenderSettings.fog=false;
                try
                {
                    camera.Render();camera.Render();
                    var on=Read(camera,target,id+"-on");var repeat=Read(camera,target,id+"-repeat");
                    double repeatError=Difference(on,repeat);
                    foreach(var mat in treated)mat.SetFloat("_SurfaceCavityStrength",0);
                    var off=Read(camera,target,id+"-off");int affected=0;double energy=0;
                    for(int p=0;p<on.Length;p++)
                    {
                        float delta=off[p].grayscale-on[p].grayscale;
                        if(delta>.002f){affected++;energy+=delta;}
                        if(delta<-.002f)throw new Exception(id+" cavity unexpectedly brightens the costume");
                    }
                    if(affected<100||energy<1||repeatError>.00001)throw new Exception(id+" cavity missing or unstable: "+affected+" "+energy+" "+repeatError);
                    foreach(var mat in materials)if(mat.HasProperty("_EmissionAudit"))
                    {
                        mat.SetFloat("_EmissionAudit",1);
                        mat.SetVector("_GuardPoint",new Vector4(world.HeroHome.x,2.2f,world.HeroHome.z,1.2f));
                        mat.SetColor("_GuardColor",new Color(.2f,.6f,1,.7f));
                    }
                    var emissionOff=Read(camera,target,id+"-emission-off");
                    foreach(var mat in treated)mat.SetFloat("_SurfaceCavityStrength",.82f);
                    var emissionOn=Read(camera,target,id+"-emission-on");double emissionError=Difference(emissionOn,emissionOff);
                    if(emissionError>.00001)throw new Exception(id+" eye/timer/shield emission altered: "+emissionError);
                    report.AppendLine($"{id}: maps={expected} affectedPixels={affected} cavityEnergy={energy:F4} repeatError={repeatError:F8} emissionError={emissionError:F8}");
                }
                finally{RenderSettings.fog=fog;camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);}
            }
            File.WriteAllText(Folder+"/validation.txt",report.ToString());Debug.Log("[HeroCavityReview]\n"+report);
        }
        static double Difference(Color[] a,Color[] b)
        {double sum=0;for(int i=0;i<a.Length;i++)sum+=Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b);return sum/a.Length;}
        static Color[] Read(Camera camera,RenderTexture target,string name)
        {
            CharacterReview.Save(camera,target,Folder+"/"+name+".png");RenderTexture.active=target;
            var image=new Texture2D(target.width,target.height,TextureFormat.RGBAFloat,false,true);
            image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();var pixels=image.GetPixels();
            UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=null;return pixels;
        }
    }
}
