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
    public static class TigaSuitReview
    {
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/tiga-suit/inspection"));
        public static void Release()
        {
            Validate();HeroMaterialReview.After();FinisherReview.After();
            GuardImpactReview.Render();GuardImpactReview.LightInterruptions();
        }
        public static void Validate()
        {
            Directory.CreateDirectory(Folder);File.Delete(Folder+"/validation.txt");var report=new StringBuilder();
            var shader=Resources.Load<Shader>("HeroSurface");
            if(!shader||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new Exception("Invalid costume shader");
            string path="Assets/Resources/Characters/Tiga/TigaBodyOcclusion.png";
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            var cavity=Resources.Load<Texture2D>("Characters/Tiga/TigaBodyOcclusion");
            if(importer.sRGBTexture||!importer.mipmapEnabled||cavity.width!=2048||cavity.height!=2048)
                throw new Exception("Cavity map must be a 2K linear data texture with mipmaps");
            // Check the actual full model, not a replacement test material.
            foreach(string id in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var world=new GameWorld();var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);
                var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);enemy.Root.gameObject.SetActive(false);
                hero.Update(new Battle(),world.Camera,0,0,3);world.Tick(new Battle(),0,0);
                var materials=hero.Root.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Distinct().ToArray();
                var treated=materials.Where(m=>m.HasProperty("_CostumeFinish")&&m.GetFloat("_CostumeFinish")>.5f).ToArray();
                if(treated.Length!=(id=="Tiga"?1:0)||treated.Any(m=>m.name!="TigaSuit"||m.GetTexture("_CostumeOcclusion")!=cavity))
                    throw new Exception("Costume finish leaked into another surface: "+id);
                if(id!="Tiga"){report.AppendLine(id+": original material configuration preserved");continue;}
                // Hold the same rendered geometry across all samples. Replacing
                // shadow casters on each read can reorder equal-depth fragments
                // and would measure snapshot churn rather than shader stability.
                foreach(var skin in hero.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    int slot=Array.FindIndex(skin.sharedMaterials,m=>m==treated[0]);
                    skin.enabled=false;if(slot<0)continue;
                    var mesh=new Mesh();skin.BakeMesh(mesh,true);
                    // Isolate costume triangles from nearly coplanar trim.
                    // Changing a texture may reorder opaque material batches;
                    // trim depth ties must not masquerade as an AO response.
                    mesh.triangles=mesh.GetTriangles(slot);
                    var frozen=new GameObject("Frozen costume sample",typeof(MeshFilter),typeof(MeshRenderer));
                    frozen.layer=skin.gameObject.layer;frozen.transform.SetParent(skin.transform,false);
                    frozen.GetComponent<MeshFilter>().sharedMesh=mesh;
                    var surface=frozen.GetComponent<MeshRenderer>();surface.sharedMaterial=treated[0];
                    surface.shadowCastingMode=skin.shadowCastingMode;surface.receiveShadows=skin.receiveShadows;skin.enabled=false;
                }
                // Isolate the map's ambient-light response from per-camera
                // light selection and the reflection probe's first upload.
                // Separate arena/guard renders retain the real scene lighting.
                foreach(var lamp in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))lamp.enabled=false;
                RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight=new Color(.65f,.65f,.65f);RenderSettings.reflectionIntensity=0;
                var camera=new GameObject("Costume material inspection").AddComponent<Camera>();camera.enabled=false;
                camera.cullingMask=1<<ContactShadows.ActorLayer;camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=Color.black;camera.allowHDR=true;camera.renderingPath=RenderingPath.Forward;camera.fieldOfView=31;camera.aspect=1;
                camera.transform.position=world.HeroHome-world.BattleAxis*5.6f+Vector3.up*2.3f;
                camera.transform.LookAt(world.HeroHome+Vector3.up*1.9f);
                // Numeric reads use one sample per pixel. MSAA resolve can
                // mix neighboring material edges differently after rebinding
                // a texture; visual arena captures still use 4x MSAA.
                var target=new RenderTexture(512,512,24,RenderTextureFormat.ARGBHalf);target.Create();camera.targetTexture=target;
                bool fog=RenderSettings.fog;RenderSettings.fog=false;
                try
                {
                    camera.Render();camera.Render();
                    var mat=treated[0];var enabled=Read(camera,target,"cavity-on");
                    var repeat=Read(camera,target,"cavity-repeat");
                    double repeatDifference=Difference(enabled,repeat,out int repeatPixels);
                    if(repeatDifference>.00001)throw new Exception("Frozen costume sample flickers: "+repeatDifference);
                    mat.SetTexture("_CostumeOcclusion",Texture2D.whiteTexture);
                    var disabled=Read(camera,target,"cavity-off");double darkened=0;int affected=0;
                    for(int p=0;p<enabled.Length;p++)
                    {
                        float delta=disabled[p].grayscale-enabled[p].grayscale;
                        if(delta>.002f){darkened+=delta;affected++;}
                        if(delta<-.002f)throw new Exception("Occlusion brightened skin");
                    }
                    if(affected<250||darkened<2)throw new Exception("Baked cavity has no visible GPU effect");
                    mat.SetTexture("_CostumeOcclusion",cavity);
                    // The finish must not invent emission or change shield
                    // contact falloff. Compare the true emission pass directly.
                    var contact=world.HeroHome-world.BattleAxis*.5f+Vector3.up*2.3f;
                    mat.SetVector("_GuardPoint",new Vector4(contact.x,contact.y,contact.z,1.1f));
                    mat.SetColor("_GuardColor",new Color(.2f,.6f,1,.7f));
                    foreach(var m in materials)if(m.HasProperty("_EmissionAudit"))m.SetFloat("_EmissionAudit",1);
                    var emissionOn=Read(camera,target,null);mat.SetFloat("_CostumeFinish",0);
                    var emissionOff=Read(camera,target,null);
                    if(Difference(emissionOn,emissionOff,out _)> .00001)throw new Exception("Costume finish altered emission");
                    report.AppendLine($"Tiga: cavityPixels={affected} cavityEnergy={darkened:F4} repeatError={repeatDifference:F8} emission-preserved=passed texture=2048-linear-mipmapped");
                }
                finally{RenderSettings.fog=fog;camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);}
            }
            File.WriteAllText(Folder+"/validation.txt",report.ToString());Debug.Log("[TigaSuitReview]\n"+report);
        }
        static double Difference(Color[] a,Color[] b,out int changed)
        {double sum=0;changed=0;for(int i=0;i<a.Length;i++){float d=Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b);sum+=d;if(d>.003f)changed++;}return sum/a.Length;}
        static Color[] Read(Camera camera,RenderTexture target,string name)
        {
            if(name!=null)CharacterReview.Save(camera,target,Folder+"/"+name+".png");
            // Save snapshots skinned meshes in batch mode. Reuse its same
            // render target to inspect linear HDR pixels after that render.
            else CharacterReview.Save(camera,target,Folder+"/emission-sample.png");
            RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGBAFloat,false,true);
            image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();var pixels=image.GetPixels();
            UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=null;return pixels;
        }
    }
}
