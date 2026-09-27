using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    // Same camera, light and runtime material on both sides of an asset repair.
    public static class HeroMeshReview
    {
        public static void Before()=>Render("before");
        public static void After()=>Render("after");
        public static void Release()
        {After();RosterPunchReview.Release();RosterReview.Render();}
        static void Render(string version)
        {
            string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/hero-mesh-review"));
            string folder=Path.Combine(output,version);Directory.CreateDirectory(folder);
            File.Delete(Path.Combine(folder,"validation.txt"));var report=new StringBuilder();
            foreach(string id in new[]{"Grigio","Geed"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                UnityEngine.Random.InitState(270927);
                string path="Characters/"+id+"/"+id;
                string curves=CurveFingerprint(path);
                File.WriteAllText(Path.Combine(folder,id+"-curves.txt"),curves);
                string curveResult=version=="before"?"baseline":CompareCurves(File.ReadAllText(Path.Combine(output,"before",id+"-curves.txt")),curves);
                var world=new GameWorld();var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);
                var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                enemy.Root.gameObject.SetActive(false);var state=new Battle();
                var go=new GameObject("Mesh inspection camera");var camera=go.AddComponent<Camera>();camera.enabled=false;
                camera.cullingMask=1<<ContactShadows.ActorLayer;camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=new Color(.025f,.035f,.05f);camera.aspect=4f/3;camera.fieldOfView=31;
                camera.renderingPath=RenderingPath.Forward;
                var light=new GameObject("Inspection fill").AddComponent<Light>();light.type=LightType.Directional;
                light.intensity=.65f;light.color=new Color(.86f,.91f,1);light.shadows=LightShadows.None;
                var target=new RenderTexture(1280,960,24){antiAliasing=4};target.Create();camera.targetTexture=target;
                bool fog=RenderSettings.fog;RenderSettings.fog=false;
                try
                {
                    foreach(int pose in new[]{0,3,4,7})foreach(bool back in new[]{false,true})
                    {
                        hero.Update(state,world.Camera,0,0,pose);
                        var side=Vector3.Cross(Vector3.up,world.BattleAxis);
                        camera.transform.position=world.HeroHome+world.BattleAxis*(back?-4.6f:4.6f)+side*1.3f+Vector3.up*2.7f;
                        camera.transform.LookAt(world.HeroHome+Vector3.up*2.45f);light.transform.rotation=camera.transform.rotation;
                        CharacterReview.Save(camera,target,Path.Combine(folder,$"{id}-{pose}-{(back?"back":"front")}.png"));
                    }
                    var skin=hero.Root.GetComponentInChildren<SkinnedMeshRenderer>();
                    var mesh=new Mesh();skin.BakeMesh(mesh,true);
                    if(mesh.vertexCount<10000||mesh.bounds.size.y<.1f)throw new Exception("Invalid repaired mesh: "+id);
                    foreach(var vertex in mesh.vertices)if(float.IsNaN(vertex.sqrMagnitude)||float.IsInfinity(vertex.sqrMagnitude))throw new Exception("Invalid vertex: "+id);
                    report.AppendLine($"{id}: vertices={mesh.vertexCount} poses=8 finiteVertices=passed curves={Hash(curves)} curveCheck={curveResult}");
                    UnityEngine.Object.DestroyImmediate(mesh);
                    using(var sha=System.Security.Cryptography.SHA256.Create())
                    {
                        var sources=new StringBuilder();
                        foreach(string file in new[]{"Resources/"+path+".fbx","Scripts/Runtime/RiggedActor.cs","Resources/HeroSurface.shader","Editor/HeroMeshReview.cs"})
                            sources.AppendLine(file+" "+Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Application.dataPath,file)))));
                        File.WriteAllText(Path.Combine(folder,id+"-sources.txt"),sources.ToString());
                    }
                }
                finally{RenderSettings.fog=fog;camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);}
            }
            File.WriteAllText(Path.Combine(folder,"validation.txt"),report.ToString());Debug.Log("[HeroMeshReview] "+report);
        }
        static string CurveFingerprint(string path)
        {
            var text=new StringBuilder();
            foreach(var clip in Resources.LoadAll<AnimationClip>(path).Where(c=>!c.name.StartsWith("__preview__")).OrderBy(c=>c.name,StringComparer.Ordinal))
            {
                text.AppendLine(clip.name+" "+clip.length.ToString("R",CultureInfo.InvariantCulture));
                foreach(var binding in AnimationUtility.GetCurveBindings(clip).OrderBy(b=>b.path+"/"+b.propertyName,StringComparer.Ordinal))
                {
                    text.AppendLine(binding.path+"/"+binding.propertyName);
                    foreach(var key in AnimationUtility.GetEditorCurve(clip,binding).keys)
                        text.AppendLine(FormattableString.Invariant($"{key.time:R} {key.value:R} {key.inTangent:R} {key.outTangent:R}"));
                }
            }
            return text.ToString();
        }
        static string Hash(string text)
        {using(var sha=System.Security.Cryptography.SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));}
        static string CompareCurves(string before,string after)
        {
            // Re-exporting the same Blender action introduces micro-unit
            // float differences. Names, bindings, times and key counts must
            // still match; compare values and slopes separately at tight limits.
            var a=before.Split('\n');var b=after.Split('\n');double maxValue=0,maxTangent=0;int keys=0;
            if(a.Length!=b.Length)throw new Exception("Animation key count changed");
            for(int i=0;i<a.Length;i++)
            {
                var x=a[i].Split(' ');var y=b[i].Split(' ');
                if(x.Length!=4||y.Length!=4||!double.TryParse(x[0],NumberStyles.Float,CultureInfo.InvariantCulture,out double time))
                {if(a[i]!=b[i])throw new Exception("Animation clip/binding changed: "+a[i]);continue;}
                keys++;
                if(time!=double.Parse(y[0],CultureInfo.InvariantCulture))throw new Exception("Animation key time changed");
                for(int k=1;k<4;k++)
                {
                    double oldValue=double.Parse(x[k],CultureInfo.InvariantCulture),newValue=double.Parse(y[k],CultureInfo.InvariantCulture);
                    double delta=oldValue==newValue?0:Math.Abs(oldValue-newValue);
                    if(double.IsNaN(delta)||double.IsInfinity(delta)||delta>(k==1?.00001:.0002))
                        throw new Exception("Animation curve changed beyond float export tolerance: "+a[i]+" -> "+b[i]);
                    if(k==1)maxValue=Math.Max(maxValue,delta);else maxTangent=Math.Max(maxTangent,delta);
                }
            }
            return FormattableString.Invariant($"passed keys={keys} maxValueDelta={maxValue:E3} maxTangentDelta={maxTangent:E3}");
        }
    }
}
