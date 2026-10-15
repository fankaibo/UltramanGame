using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class EjectaCoolingReview
    {
        static string Root=>Path.GetFullPath(Application.dataPath+"/../../artifacts/ejecta-cooling-20261012");
        public static void Before(){Measure(false);VolcanoStageReview.Before();}
        public static void After(){Measure(true);VolcanoStageReview.After();VolcanicEjectaReview.Validate();}
        static void Measure(bool verify)
        {
            string folder=Root+(verify?"/after-thermal":"/before-thermal");Directory.CreateDirectory(folder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Thermal camera").AddComponent<Camera>();camera.transform.position=new Vector3(0,0,-3);
            camera.orthographic=true;camera.orthographicSize=.6f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);var mat=new Material(Resources.Load<Shader>("VolcanicSpark"));quad.GetComponent<Renderer>().sharedMaterial=mat;
            var mesh=UnityEngine.Object.Instantiate(quad.GetComponent<MeshFilter>().sharedMesh);quad.GetComponent<MeshFilter>().sharedMesh=mesh;
            var rt=new RenderTexture(128,128,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);rt.Create();camera.targetTexture=rt;
            var pixels=new Texture2D(128,128,TextureFormat.RGBAFloat,false,true);var report=new StringBuilder();
            double hotEnergy=0,coldEnergy=0,hotRatio=0,coldRatio=0;
            try
            {
                foreach(float heat in new[]{1f,.3f})
                {
                    Color c=Color.Lerp(new Color(.75f,.035f,.002f),new Color(2.4f,.64f,.075f),heat);c.a=1;
                    var colors=new Color[mesh.vertexCount];for(int i=0;i<colors.Length;i++)colors[i]=c;mesh.colors=colors;
                    camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,128,128),0,0);pixels.Apply();
                    double r=0,g=0,b=0;foreach(var p in pixels.GetPixels()){r+=p.r;g+=p.g;b+=p.b;}
                    double energy=r+g+b,ratio=g/Math.Max(.0001,r);
                    report.AppendLine(FormattableString.Invariant($"heat={heat:F2} rgbEnergy={energy:F4} greenOverRed={ratio:F5}"));
                    if(heat>.9f){hotEnergy=energy;hotRatio=ratio;}else{coldEnergy=energy;coldRatio=ratio;}
                    var preview=new Texture2D(128,128,TextureFormat.RGB24,false);preview.SetPixels(pixels.GetPixels());preview.Apply();
                    File.WriteAllBytes(folder+(heat>.9f?"/hot.png":"/cool.png"),preview.EncodeToPNG());UnityEngine.Object.DestroyImmediate(preview);
                }
                if(verify&&(hotEnergy<100||coldEnergy>=hotEnergy*.7||coldRatio>=hotRatio*.8))throw new Exception("Cooling does not reduce both radiance and color temperature: "+report);
                // Compare the real rock meshes across the entire cycle. The glow
                // changes, but neither flight nor terrain contacts may move.
                var root=new GameObject("Trajectory").transform;var ejecta=new VolcanicEjecta(root,new[]{Vector3.zero},(x,z)=>.1f*x);
                var rock=root.GetComponentInChildren<MeshFilter>().sharedMesh;var motion=new StringBuilder();
                for(int n=0;n<240;n++)
                {
                    ejecta.Tick(n/30f,camera);motion.Append(ejecta.ActiveBombs).Append(',').Append(ejecta.LandedBombs).Append(';');
                    foreach(var p in rock.vertices)motion.Append(FormattableString.Invariant($"{p.x:F6},{p.y:F6},{p.z:F6};"));
                }
                using(var sha=SHA256.Create())
                {
                    string hash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(motion.ToString()))).Replace("-","").ToLowerInvariant();
                    File.WriteAllText(folder+"/trajectory.sha256",hash);
                    if(verify&&File.ReadAllText(Root+"/before-thermal/trajectory.sha256")!=hash)throw new Exception("Rock trajectory/contact changed");
                    var source=new StringBuilder();foreach(string f in new[]{"Scripts/Runtime/VolcanicEjecta.cs","Resources/VolcanicSpark.shader","Resources/VolcanicBomb.shader","Editor/EjectaCoolingReview.cs"})
                        source.AppendLine(f+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Application.dataPath+"/"+f))));
                    File.WriteAllText(folder+"/sources.txt",source.ToString());
                    report.AppendLine("rockTrajectory240Frames="+hash);
                }
                report.AppendLine(verify?"PASS cooling and unchanged rock motion":"BASELINE measurement; cooling not asserted");
                File.WriteAllText(folder+"/validation.txt",report.ToString());Debug.Log("[EjectaCooling] "+report);
            }
            finally{camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(mat);}
        }
    }
}
