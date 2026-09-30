using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class VolcanicEjectaReview
    {
        static string OutputRoot
        {
            get
            {
                var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--ejecta-output");
                return at>=0&&at+1<args.Length?Path.GetFullPath(args[at+1]):Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/volcanic-ejecta"));
            }
        }
        public static void LandingBefore()=>RenderLanding("before");
        public static void LandingAfter(){RenderLanding("after");Validate();}
        public static void LandingRelease(){LandingAfter();VolcanoStageReview.After();}
        static void RenderLanding(string version)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            string folder=OutputRoot+"/landing-"+version;Directory.CreateDirectory(folder);
            var root=new GameObject("Landing inspection").transform;
            var eruption=new VolcanicEjecta(root,new[]{Vector3.zero},(x,z)=>0);
            var rocks=root.GetComponentInChildren<MeshFilter>();
            var flights=(Array)typeof(VolcanicEjecta).GetField("bombs",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(eruption);
            var first=flights.GetValue(0);var type=first.GetType();
            float landing=(float)type.GetField("Landing").GetValue(first),offset=(float)type.GetField("Offset").GetValue(first);
            var velocity=(Vector3)type.GetField("Velocity").GetValue(first);var point=velocity*landing;point.y=0;
            foreach(var renderer in root.GetComponentsInChildren<Renderer>())renderer.enabled=false;
            // Copy only the first fragment's actual runtime geometry and state;
            // isolate it from the other 63 trajectories for readable contact.
            var mesh=new Mesh();var display=new GameObject("First actual fragment",typeof(MeshFilter),typeof(MeshRenderer));
            display.GetComponent<MeshFilter>().sharedMesh=mesh;display.GetComponent<Renderer>().sharedMaterial=rocks.GetComponent<Renderer>().sharedMaterial;
            display.GetComponent<Renderer>().shadowCastingMode=rocks.GetComponent<Renderer>().shadowCastingMode;
            var camera=new GameObject("Landing camera").AddComponent<Camera>();camera.enabled=false;camera.allowHDR=true;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.045f,.055f,.07f);
            camera.transform.position=point+new Vector3(.55f,.30f,-.9f);camera.transform.LookAt(point+Vector3.up*.18f);camera.fieldOfView=40;camera.aspect=4f/3;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);var floorMaterial=new Material(Shader.Find("Standard"));floorMaterial.color=new Color(.20f,.23f,.26f);
            floor.GetComponent<Renderer>().sharedMaterial=floorMaterial;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.35f,.38f,.43f);
            var light=new GameObject("Moon").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(35,-30,0);light.intensity=1;
            float shadowDistance=QualitySettings.shadowDistance;
            light.shadows=LightShadows.Soft;light.shadowBias=.005f;light.shadowNormalBias=.01f;QualitySettings.shadowDistance=10;
            var rt=new RenderTexture(640,480,24){antiAliasing=4};rt.Create();camera.targetTexture=rt;
            var vertices=new Vector3[60];var normals=new Vector3[60];var colors=new Color[60];var uv=new Vector2[60];var triangles=new int[60];
            Array.Copy(rocks.sharedMesh.uv,uv,60);for(int i=0;i<60;i++)triangles[i]=i;
            try
            {
                for(int frame=0;frame<48;frame++)
                {
                    float time=landing-offset-.25f+frame/30f;
                    eruption.Tick(time,camera);
                    Array.Copy(rocks.sharedMesh.vertices,vertices,60);Array.Copy(rocks.sharedMesh.normals,normals,60);Array.Copy(rocks.sharedMesh.colors,colors,60);
                    mesh.vertices=vertices;mesh.normals=normals;mesh.colors=colors;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateBounds();
                    CharacterReview.Save(camera,rt,$"{folder}/{frame:D4}.png");
                }
                using(var sha=System.Security.Cryptography.SHA256.Create())
                {
                    var manifest=new StringBuilder();
                    foreach(var file in new[]{"Scripts/Runtime/VolcanicEjecta.cs","Resources/VolcanicBomb.shader","Editor/VolcanicEjectaReview.cs"})
                        manifest.AppendLine(file+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Application.dataPath,file)))).Replace("-","").ToLowerInvariant());
                    File.WriteAllText(folder+"/sources.txt",manifest.ToString());
                }
                Debug.Log($"[VolcanicLanding] {version} frames=48 centeredOnFirstContact={landing:F4}");
            }
            finally{QualitySettings.shadowDistance=shadowDistance;camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(floorMaterial);}
        }
        public static void Validate()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            string folder=OutputRoot+"/inspection";Directory.CreateDirectory(folder);
            File.Delete(folder+"/validation.txt");
            foreach(string name in new[]{"VolcanicBomb","VolcanicSpark"})
            {var shader=Resources.Load<Shader>(name);if(!shader||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new Exception("Invalid eruption shader: "+name);}
            var root=new GameObject("Eruption inspection").transform;
            var camera=new GameObject("Eruption inspection camera").AddComponent<Camera>();camera.enabled=false;camera.allowHDR=true;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.015f,.021f,.032f);
            camera.transform.position=new Vector3(0,1.6f,-6);camera.transform.LookAt(new Vector3(0,1.4f,0));camera.fieldOfView=45;camera.aspect=16f/9;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.35f,.38f,.43f);
            var light=new GameObject("Moon").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(35,-30,0);light.intensity=1;
            var ejecta=new VolcanicEjecta(root,new[]{new Vector3(-1.2f,0,0),new Vector3(1.2f,0,.3f)},(x,z)=>0);
            var renderers=root.GetComponentsInChildren<Renderer>();var filters=root.GetComponentsInChildren<MeshFilter>();
            if(renderers.Length!=2||filters.Length!=2)throw new Exception("Eruption particles are not batched into two fixed meshes");
            var rt=new RenderTexture(640,360,24,RenderTextureFormat.ARGB32){antiAliasing=4};rt.Create();camera.targetTexture=rt;
            var pixels=new Texture2D(640,360,TextureFormat.RGB24,false);var report=new StringBuilder();
            Color32[] Read(string name)
            {camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,640,360),0,0);pixels.Apply();File.WriteAllBytes(folder+"/"+name+".png",pixels.EncodeToPNG());return pixels.GetPixels32();}
            int Changed(Color32[] a,Color32[] b,int threshold)
            {int n=0;for(int i=0;i<a.Length;i++)if(Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b)>threshold)n++;return n;}
            var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.transform.position=new Vector3(0,1.6f,-2);blocker.transform.localScale=new Vector3(10,8,.2f);
            var opaque=new Material(Shader.Find("Unlit/Color"));opaque.color=new Color(.12f,.22f,.32f);blocker.GetComponent<Renderer>().sharedMaterial=opaque;blocker.SetActive(false);
            try
            {
                float minimum=100;int low=1000,high=0;var meshes=new Mesh[2];for(int i=0;i<2;i++)meshes[i]=filters[i].sharedMesh;
                for(int n=0;n<480;n++)
                {
                    ejecta.Tick(n/60f,camera);low=Math.Min(low,ejecta.ActiveBombs);high=Math.Max(high,ejecta.ActiveBombs);
                    for(int i=0;i<2;i++)if(filters[i].sharedMesh!=meshes[i])throw new Exception("Per-frame particle mesh allocation");
                    foreach(var p in filters[0].sharedMesh.vertices)
                    {if(float.IsNaN(p.sqrMagnitude)||float.IsInfinity(p.sqrMagnitude)||!filters[0].sharedMesh.bounds.Contains(p))throw new Exception("Invalid particle vertex or culling bounds");minimum=Mathf.Min(minimum,p.y);}
                }
                ejecta.Tick(.65f,camera);foreach(var r in renderers)r.enabled=false;var clear=Read("clear");foreach(var r in renderers)r.enabled=true;
                var first=Read("flight");ejecta.Tick(.65f,camera);var frozen=Read("frozen");
                int visible=Changed(clear,first,6),drift=Changed(first,frozen,0);
                ejecta.Tick(1.20f,camera);int moving=Changed(first,Read("later"),6);
                ejecta.Tick(.65f,camera);int rewind=Changed(first,Read("rewound"),0);
                // The two meshes must depth-test against an actual opaque object,
                // not merely suppress themselves when the camera faces away.
                blocker.SetActive(true);foreach(var r in renderers)r.enabled=false;var blocked=Read("opaque-front");foreach(var r in renderers)r.enabled=true;
                int leaked=Changed(blocked,Read("opaque-front-particles"),0);blocker.SetActive(false);
                camera.transform.position=new Vector3(4.5f,1.6f,-4);camera.transform.LookAt(new Vector3(0,1.4f,0));ejecta.Tick(.65f,camera);
                Read("oblique-flight");
                if(visible<150||moving<150||drift!=0||rewind!=0||leaked!=0||minimum<-.02f||low<1||high<15)
                    throw new Exception($"Eruption GPU/flight check: visible={visible} moving={moving} drift={drift} rewind={rewind} leaked={leaked} ground={minimum} active={low}..{high}");
                string contacts=ContactChecks();
                report.AppendLine($"passed: visiblePixels={visible} movingPixels={moving} frozenDifference={drift} rewindDifference={rewind} foregroundLeak={leaked} minGround={minimum:F5} activeBombs={low}..{high} fixedMeshes=2 {contacts}");
                File.WriteAllText(folder+"/validation.txt",report.ToString());Debug.Log("[VolcanicEjectaReview] "+report);
            }
            finally{camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(opaque);}
        }
        static string ContactChecks()
        {
            float lowest=1,largestGap=0,largestContactStep=0;int cases=0;
            foreach(float slope in new[]{0f,.18f,-.18f})
            {
                float Height(float x,float z)=>x*slope-z*slope*.45f;
                var root=new GameObject("Slope contact inspection");root.SetActive(false);
                try
                {
                    var ejecta=new VolcanicEjecta(root.transform,new[]{Vector3.zero},Height);
                    var mesh=root.GetComponentsInChildren<MeshFilter>(true)[0].sharedMesh;
                    var flights=(Array)typeof(VolcanicEjecta).GetField("bombs",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(ejecta);
                    foreach(int index in new[]{0,7,31})
                    {
                        var flight=flights.GetValue(index);var type=flight.GetType();
                        float landing=(float)type.GetField("Landing").GetValue(flight),offset=(float)type.GetField("Offset").GetValue(flight);
                        ejecta.Tick(landing-offset-.0001f,null);var before=mesh.vertices;
                        ejecta.Tick(landing-offset+.0001f,null);var after=mesh.vertices;
                        for(int v=index*60;v<(index+1)*60;v++)largestContactStep=Mathf.Max(largestContactStep,Vector3.Distance(before[v],after[v]));
                        foreach(int hz in new[]{15,30,60})
                        {
                            for(int frame=0;frame<=hz;frame++)
                            {
                                float age=frame/(float)hz;ejecta.Tick(landing-offset+age,null);
                                var vertices=mesh.vertices;float gap=1;
                                for(int v=index*60;v<(index+1)*60;v++)
                                {float clearance=vertices[v].y-Height(vertices[v].x,vertices[v].z);lowest=Mathf.Min(lowest,clearance);gap=Mathf.Min(gap,clearance);}
                                if(age>.31f&&age<.65f)largestGap=Mathf.Max(largestGap,gap);
                                if(age>.32f&&age<.65f&&Vector3.Distance(vertices[index*60],vertices[index*60+1])<.01f)
                                    throw new Exception("Landed rock shrank before cooling");
                            }
                            cases++;
                        }
                    }
                }
                finally{UnityEngine.Object.DestroyImmediate(root);}
            }
            if(lowest<-.002f||largestGap>.012f||largestContactStep>.003f)
                throw new Exception($"Rock contact failure: ground={lowest} gap={largestGap} step={largestContactStep}");
            return $"contactCases={cases} slopeGround={lowest:F5} settledGap={largestGap:F5} contactStep={largestContactStep:F6}";
        }
    }
}
