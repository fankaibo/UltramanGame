using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class OutpostDamageChecks
    {
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/outpost-reaction/inspection"));
        public static void Release(){Validate();OutpostReactionReview.After();}
        public static void Validate()
        {
            Directory.CreateDirectory(Folder);File.Delete(Folder+"/validation.txt");var report=new StringBuilder();Vector3[] reference=null;
            foreach(int rate in new[]{15,30,60})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var root=new GameObject("Reaction test");float Height(float x,float z)=>x*.025f+z*.01f;
                var site=VolcanicOutpost.Create(root.transform,Height);var damage=site.Damage;var mesh=damage.Geometry;
                Vector3[] original=mesh.vertices,samples=new Vector3[original.Length*3];int sample=0,objects=root.GetComponentsInChildren<Transform>(true).Length;
                float time=0,minClearance=100,maxTravel=0,normalError=0;
                damage.Shock(Vector3.zero,"slam");
                foreach(float stop in new[]{.8f,1.5f,2.8f})
                {
                    while(time<stop-.000001f)
                    {
                        float dt=Mathf.Min(1f/rate,stop-time);time+=dt;damage.Tick(null,dt);
                        var positions=mesh.vertices;var normals=mesh.normals;var tangents=mesh.tangents;
                        for(int v=0;v<positions.Length;v++)
                        {
                            var p=positions[v];if(float.IsNaN(p.sqrMagnitude)||float.IsInfinity(p.sqrMagnitude)||!mesh.bounds.Contains(p))throw new Exception($"Invalid debris or bounds: vertex={v} point={p} bounds={mesh.bounds}");
                            minClearance=Mathf.Min(minClearance,p.y-Height(p.x,p.z));maxTravel=Mathf.Max(maxTravel,Vector3.Distance(p,original[v]));
                            normalError=Mathf.Max(normalError,Mathf.Abs(Vector3.Dot(normals[v],(Vector3)tangents[v])));
                        }
                    }
                    var snapshot=mesh.vertices;Array.Copy(snapshot,0,samples,sample++*snapshot.Length,snapshot.Length);damage.Tick(null,0);
                    if(Difference(snapshot,mesh.vertices)>.00001f)throw new Exception("Zero time moved debris");
                }
                float difference=reference==null?0:Difference(reference,samples);if(reference==null)reference=samples;
                if(damage.Detached!=2||damage.ActiveClouds!=0||minClearance<.0088f||maxTravel<.4f||difference>.0001f||normalError>.0001f)
                    throw new Exception($"Debris motion: count={damage.Detached} dust={damage.ActiveClouds} ground={minClearance} travel={maxTravel} rate={difference} normal={normalError}");
                var settled=mesh.vertices;damage.Tick(null,1);if(Difference(settled,mesh.vertices)>.00001f)throw new Exception("Fallen debris failed to remain settled");
                int resting=0;var settledNormals=mesh.normals;var groundNormal=new Vector3(-.025f,1,-.01f).normalized;
                for(int first=0;first<settled.Length;first+=24)if(Vector3.Distance(settled[first],original[first])>.1f)
                {
                    float face=0;for(int v=first;v<first+24;v++)face=Mathf.Max(face,Vector3.Dot(settledNormals[v],groundNormal));
                    if(face<.9999f)throw new Exception("A fallen slab remains balanced on its edge");resting++;
                }
                if(resting!=2)throw new Exception("Missing resting slab poses");
                for(int n=0;n<24;n++)damage.Shock(Vector3.zero,"slam");damage.Tick(null,4);
                if(damage.Detached!=damage.Count||root.GetComponentsInChildren<Transform>(true).Length!=objects||damage.Geometry!=mesh)throw new Exception("Unbounded or incomplete destruction");
                damage.Reset();if(Difference(original,mesh.vertices)>.00001f||damage.Detached!=0||damage.ActiveClouds!=0||damage.Shocks!=0)throw new Exception("New round did not restore building");
                report.AppendLine($"{rate}Hz panels={damage.Count} minGround={minClearance:F6} travel={maxTravel:F4} rateDifference={difference:F6} normalError={normalError:F6} restingFaces={resting} frozen=passed settled=passed bounded=passed reset=passed");
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var world=new GameWorld();var state=new Battle();world.Tick(state,0,0);
            state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});for(int n=0;n<(Battle.TransformationSeconds+0.6f)/(.02f);n++)state.Tick(.02f,new PlayerInput{Tracking=true});
            world.OutpostDamage.Shock(Vector3.zero,"slam");world.Tick(state,.83f,1);
            var frozen=world.OutpostDamage.Geometry.vertices;float clock=world.OutpostDamage.Clock;state.Pause();world.Tick(state,.7f,2);
            if(world.OutpostDamage.Clock!=clock||Difference(frozen,world.OutpostDamage.Geometry.vertices)>.00001f)throw new Exception("Paused game moved the outpost");
            world.Tick(new Battle(),.2f,3);if(world.OutpostDamage.Detached!=0||world.OutpostDamage.Shocks!=0)throw new Exception("New battle retained destruction");
            report.AppendLine("GameWorld pause=passed newBattle=passed");
            Gpu(report);File.WriteAllText(Folder+"/validation.txt",report.ToString());Debug.Log("[OutpostDamageChecks]\n"+report);
            var sources=new StringBuilder();using(var sha=System.Security.Cryptography.SHA256.Create())
                foreach(string file in new[]{"Scripts/Runtime/OutpostDamage.cs","Scripts/Runtime/VolcanicOutpost.cs","Scripts/Runtime/GameWorld.cs","Scripts/Runtime/VolcanoStage.cs","Scripts/Runtime/GroundImpact.cs","Scripts/Runtime/CombatVfx.cs","Editor/OutpostDamageChecks.cs"})
                    sources.AppendLine(file+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Application.dataPath,file)))).Replace("-","").ToLowerInvariant());
            File.WriteAllText(Folder+"/sources.txt",sources.ToString());
        }
        static float Difference(Vector3[] a,Vector3[] b)
        {float error=0;for(int i=0;i<a.Length;i++)error=Mathf.Max(error,Vector3.Distance(a[i],b[i]));return error;}
        static void Gpu(StringBuilder report)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("Visible building reaction");var site=VolcanicOutpost.Create(root.transform,(x,z)=>0);
            var cam=new GameObject("Inspection camera").AddComponent<Camera>();cam.transform.position=new Vector3(-5.7f,1.8f,.4f);cam.transform.LookAt(new Vector3(-3.8f,.55f,4.4f));cam.fieldOfView=32;cam.aspect=16f/9;
            cam.depthTextureMode=DepthTextureMode.Depth;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.045f,.065f,.08f);
            RenderSettings.fog=false;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.3f,.35f,.4f);
            var light=new GameObject("Moon").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(40,-35,0);light.intensity=1.2f;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(0,-.08f,5);floor.transform.localScale=new Vector3(20,.16f,20);
            var mat=new Material(Shader.Find("Standard")){color=new Color(.16f,.18f,.19f)};floor.GetComponent<Renderer>().sharedMaterial=mat;
            var rt=new RenderTexture(960,540,24){antiAliasing=4};rt.Create();cam.targetTexture=rt;var pixels=new Texture2D(960,540,TextureFormat.RGB24,false);
            Color32[] Read(string name){cam.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,960,540),0,0);pixels.Apply();File.WriteAllBytes(Folder+"/"+name+".png",pixels.EncodeToPNG());return pixels.GetPixels32();}
            int Changed(Color32[] a,Color32[] b){int n=0;for(int i=0;i<a.Length;i++)if(a[i].r!=b[i].r||a[i].g!=b[i].g||a[i].b!=b[i].b)n++;return n;}
            try
            {
                var initial=Read("intact");site.Damage.Shock(Vector3.zero,"slam");site.Damage.Tick(cam,.8f);var fall=Read("falling");
                site.Damage.Tick(cam,0);int repeat=Changed(fall,Read("frozen"));site.Damage.Tick(cam,2.5f);var settled=Read("settled");
                site.Damage.Reset();int reset=Changed(initial,Read("reset"));int visible=Changed(initial,fall),persistent=Changed(initial,settled);
                if(visible<800||persistent<150||repeat!=0||reset!=0)throw new Exception($"Building GPU visibility={visible} persistent={persistent} freeze={repeat} reset={reset}");
                report.AppendLine($"GPU fallingPixels={visible} settledPixels={persistent} frozenDifference={repeat} resetDifference={reset}");
            }
            finally{cam.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(mat);}
        }
    }
}
