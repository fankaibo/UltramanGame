using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class SlamDustVolumeReview
    {
        public static void Run()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--slam-dust-output");
            if(at<0||at+1>=args.Length)throw new ArgumentException("Explicit --slam-dust-output required");
            string folder=Path.GetFullPath(args[at+1])+"/gpu";Directory.CreateDirectory(folder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Dust inspection camera").AddComponent<Camera>();camera.enabled=false;
            camera.depthTextureMode=DepthTextureMode.Depth;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.035f,.05f);
            camera.fieldOfView=43;camera.aspect=1;
            var root=new GameObject("Dust lifetime owner");var effect=new SlamDustVolume(root.transform);
            var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.transform.localScale=new Vector3(20,20,.1f);
            var opaque=new Material(Shader.Find("Standard")){color=new Color(.12f,.18f,.25f)};blocker.GetComponent<Renderer>().sharedMaterial=opaque;blocker.SetActive(false);
            var target=new RenderTexture(480,480,24);target.Create();camera.targetTexture=target;
            var pixels=new Texture2D(480,480,TextureFormat.RGB24,false);var report=new StringBuilder();
            Color32[] Read(string name)
            {camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,480,480),0,0);pixels.Apply();File.WriteAllBytes(folder+"/"+name+".png",pixels.EncodeToPNG());return pixels.GetPixels32();}
            int Changed(Color32[] a,Color32[] b,int threshold=3)
            {int n=0;for(int i=0;i<a.Length;i++)if(Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b)>threshold)n++;return n;}
            try
            {
                var shader=Resources.Load<Shader>("SlamDustVolume");if(!shader||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new Exception("Dust shader unavailable");
                foreach(int angle in new[]{0,45,90,135})
                {
                    var focus=new Vector3(0,.4f,1.5f);camera.transform.position=focus+Quaternion.Euler(0,angle,0)*new Vector3(0,1.6f,-7);
                    camera.transform.LookAt(focus);effect.Clear();var clear=Read(angle+"-clear");
                    effect.Hit(Vector3.zero,Vector3.forward,3);effect.Tick(.30f);var hit=Read(angle+"-dust");
                    int visible=Changed(clear,hit);if(visible<500)throw new Exception("Missing dust "+angle+" "+visible);
                    effect.Tick(0);int frozen=Changed(hit,Read(angle+"-frozen"),0);if(frozen!=0)throw new Exception("Paused dust moved");
                    effect.Tick(.23f);int motion=Changed(hit,Read(angle+"-later"));if(motion<300)throw new Exception("Dust static");
                    blocker.transform.SetPositionAndRotation(camera.transform.position+camera.transform.forward,camera.transform.rotation);blocker.SetActive(true);
                    var blocked=Read(angle+"-blocked-dust");effect.Clear();int leak=Changed(blocked,Read(angle+"-blocked-clear"),0);blocker.SetActive(false);
                    if(leak!=0)throw new Exception("Dust leaked through opaque surface "+leak);
                    effect.Hit(Vector3.zero,Vector3.forward,3);effect.Tick(SlamDustVolume.Lifetime+.1f);
                    if(effect.Visible||Changed(clear,Read(angle+"-expired"),0)!=0)throw new Exception("Expired dust remains");
                    report.AppendLine($"angle={angle} visible={visible} motion={motion} frozen={frozen} depthLeak={leak} expired=passed");
                }
                int objects=root.GetComponentsInChildren<Transform>(true).Length;Color32[] reference=null;
                foreach(int hz in new[]{15,30,60})
                {
                    effect.Clear();effect.Hit(Vector3.zero,Vector3.forward,3);
                    // Compare at exactly .4 seconds, rather than at unlike frame boundaries.
                    for(int f=0;f<hz*2/5;f++)effect.Tick(1f/hz);
                    var sample=Read(hz+"-common-time");int different=reference==null?0:Changed(reference,sample,3);reference=sample;
                    if(different>10)throw new Exception("Frame-rate dependent density "+different);
                    effect.Tick(2);if(effect.Visible)throw new Exception("Frame-rate expiry failed");
                    for(int n=0;n<12;n++){effect.Hit(Vector3.zero,n%2==0?Vector3.zero:Vector3.up,n%3==0?0:3);effect.Tick(.21f);effect.Clear();if(effect.Visible)throw new Exception("Reset left dust");}
                    if(root.GetComponentsInChildren<Transform>(true).Length!=objects)throw new Exception("Per-hit object allocation");
                    report.AppendLine($"hz={hz} commonTimeDifference={different} zeroAxis=passed reset=passed objects={objects}");
                }
                // Ordinary step bursts retain their billboard count; slam uses
                // one volume and the same 48 fragments. Clear handles both.
                var owner=new GameObject("Ground integration");var ground=new GroundImpact(owner.transform);
                ground.Burst(Vector3.zero,Vector3.forward,"step");ground.Tick(camera,0);
                if(ground.ActiveStones!=18||ground.ActiveClouds!=12)throw new Exception("Ordinary ground burst changed");
                ground.Clear();ground.Burst(Vector3.zero,Vector3.forward,"slam",3);ground.Tick(camera,.2f);
                if(ground.ActiveStones!=48||ground.ActiveClouds!=1)throw new Exception("Slam pool mismatch");
                ground.Clear();ground.Tick(camera,.2f);if(ground.ActiveClouds!=0||ground.ActiveStones!=0)throw new Exception("Clear revived effects");
                UnityEngine.Object.DestroyImmediate(owner);report.AppendLine("ordinary=18/12 slam=48/1 clear=passed");
                File.WriteAllText(folder+"/validation.txt",report.ToString());Debug.Log("[SlamDustVolumeReview] "+report);
            }
            finally{camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(opaque);UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
