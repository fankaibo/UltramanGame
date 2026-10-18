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
    public static class TigaLensReview
    {
        public static void Before()=>Run("before");
        public static void After()=>Run("after");
        static void Run(string version)
        {
            var folder=Path.GetFullPath(Application.dataPath+"/../../artifacts/tiga-lens-20261011/"+version);Directory.CreateDirectory(folder+"/frames");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(113);
            var world=new GameWorld();var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);hero.Update(new Battle(),world.Camera,0,0,4);
            var head=hero.Root.GetComponentsInChildren<Transform>().First(t=>t.name=="head");
            var materials=hero.Root.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Distinct().ToArray();
            var eyes=materials.Where(m=>m.name=="TigaEyesGlow").ToArray();if(eyes.Length!=1)throw new Exception("Expected original eye material");
            var sourceSkins=Resources.Load<GameObject>("Characters/Tiga/Tiga").GetComponentsInChildren<Renderer>();
            var meshReport=new StringBuilder();
            foreach(var skin in hero.Root.GetComponentsInChildren<Renderer>())
            {
                Mesh MeshOf(Renderer r)=>r is SkinnedMeshRenderer sk?sk.sharedMesh:r.GetComponent<MeshFilter>().sharedMesh;
                var source=MeshOf(sourceSkins.First(s=>s.name==skin.name));var current=MeshOf(skin);
                if(!source.vertices.SequenceEqual(current.vertices)||!source.triangles.SequenceEqual(current.triangles)||!source.uv.SequenceEqual(current.uv)||!source.boneWeights.SequenceEqual(current.boneWeights))throw new Exception("Source geometry was changed: "+skin.name);
                var old=source.normals;var now=current.normals;int changed=0;for(int i=0;i<old.Length;i++)if(Vector3.Distance(old[i],now[i])>.00001f)changed++;
                if(skin.name!="TigaEyes"&&changed!=0)throw new Exception("Non-lens normals changed");
                if(version=="after"&&skin.name=="TigaEyes")
                {
                    if(changed==0)throw new Exception("Lens smoothing was not applied to its actual renderer");
                    for(int part=0;part<skin.sharedMaterials.Length;part++)if(skin.sharedMaterials[part].name!="TigaEyesGlow")
                        foreach(int i in source.GetTriangles(part))if(old[i]!=now[i])throw new Exception("Eye rim normals changed");
                }
                meshReport.AppendLine($"{skin.name} vertices={current.vertexCount} changedNormals={changed} submeshes={current.subMeshCount}");
            }
            File.WriteAllText(folder+"/meshes.txt",meshReport.ToString());
            if(version=="after"&&(!eyes[0].shader.isSupported||ShaderUtil.ShaderHasError(eyes[0].shader)||eyes[0].shader.name!="Training/TigaLens"))throw new Exception("Lens shader missing/invalid");
            var camera=new GameObject("Tiga lens inspection").AddComponent<Camera>();camera.enabled=false;camera.allowHDR=true;camera.renderingPath=RenderingPath.Forward;
            camera.cullingMask=1<<ContactShadows.ActorLayer;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.035f,.05f);camera.fieldOfView=31;camera.aspect=4f/3;
            var rt=new RenderTexture(960,720,24,RenderTextureFormat.ARGBFloat){antiAliasing=4};rt.Create();camera.targetTexture=rt;
            bool fog=RenderSettings.fog;RenderSettings.fog=false;var bones=new StringBuilder();
            foreach(var t in hero.Root.GetComponentsInChildren<Transform>())bones.AppendLine(FormattableString.Invariant($"{t.name},{t.position.x:R},{t.position.y:R},{t.position.z:R},{t.rotation.x:R},{t.rotation.y:R},{t.rotation.z:R},{t.rotation.w:R}"));
            File.WriteAllText(folder+"/bones.csv",bones.ToString());
            var report=new StringBuilder();var side=Vector3.Cross(Vector3.up,world.BattleAxis);var pivot=head.position-Vector3.up*.12f;
            try
            {
                for(int f=0;f<90;f++)
                {
                    float angle=Mathf.Lerp(-60,60,f/89f)*Mathf.Deg2Rad;
                    camera.transform.position=pivot+(world.BattleAxis*Mathf.Cos(angle)+side*Mathf.Sin(angle))*2.1f+Vector3.up*.08f;camera.transform.LookAt(pivot);
                    RangedContactReview.Save(camera,rt,folder+"/frames/"+f.ToString("D4")+".jpg");
                    if(f==15||f==45||f==75)CharacterReview.Save(camera,rt,folder+"/view-"+f+".png");
                }
                // Fixed camera, no animation: repeated GPU evaluation must not
                // glitter over time, and transparent photos must not leak light.
                foreach(var r in hero.Root.GetComponentsInChildren<Renderer>())r.enabled=r.sharedMaterials.Contains(eyes[0]);
                Read(camera,rt);var a=Read(camera,rt);var b=Read(camera,rt);double repeat=0;for(int i=0;i<a.Length;i++)repeat=Math.Max(repeat,Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b));
                if(repeat>.0001)throw new Exception("Unstable lens render: "+repeat);
                if(version=="after")
                {
                    var mat=eyes[0];float old=mat.GetFloat("_LensAudit");mat.SetFloat("_LensAudit",1);
                    var emission=Read(camera,rt);mat.SetFloat("_LensAudit",old);
                    int pixels=0;float low=10,high=0;foreach(var c in emission)if(c.r>.08f&&c.g>.06f&&c.r>c.b*1.15f){pixels++;low=Mathf.Min(low,c.r);high=Mathf.Max(high,c.r);}
                    if(pixels<100||high-low<.12f||high>1.3f)throw new Exception($"Lens range missing or clipped: {pixels} {low} {high}");
                    report.AppendLine($"lensPixels={pixels} emissionRedRange={low:F4}..{high:F4}");
                    hero.SetPresentationOpacity(0);var transparent=Read(camera,rt);hero.Root.gameObject.SetActive(false);var empty=Read(camera,rt);double leak=0;
                    for(int i=0;i<empty.Length;i++)leak=Math.Max(leak,Math.Abs(transparent[i].r-empty[i].r)+Math.Abs(transparent[i].g-empty[i].g)+Math.Abs(transparent[i].b-empty[i].b));
                    if(leak>.001f)throw new Exception("Transparent lens leaks light: "+leak);
                    hero.Root.gameObject.SetActive(true);hero.SetPresentationOpacity(1);report.AppendLine($"transparentLightLeak={leak:F8} rimNormals=unchanged otherNormals=unchanged");
                }
                report.AppendLine($"shader={eyes[0].shader.name} views=90 repeatError={repeat:F8} originalGeometry=true");File.WriteAllText(folder+"/validation.txt",report.ToString());Debug.Log("[TigaLensReview] "+report);
            }
            finally{RenderSettings.fog=fog;camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
        }
        static Color[] Read(Camera camera,RenderTexture rt)
        {
            camera.Render();RenderTexture.active=rt;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGBAFloat,false,true);image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();var result=image.GetPixels();UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=null;return result;
        }
    }
}
