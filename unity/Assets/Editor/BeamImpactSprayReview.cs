using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class BeamImpactSprayReview
    {
        public static void Run()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--contact-output");
            if(at<0||at+1>=args.Length)throw new ArgumentException("Explicit --contact-output required");
            string folder=Path.GetFullPath(args[at+1]);Directory.CreateDirectory(folder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Contact inspection").AddComponent<Camera>();camera.enabled=false;
            camera.depthTextureMode=DepthTextureMode.Depth;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.035f,.05f);
            camera.fieldOfView=43;camera.aspect=1;
            var root=new GameObject("Contact lifetime owner");var effect=new BeamImpactVolume(root.transform);
            var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.transform.localScale=new Vector3(10,10,.1f);
            var opaque=new Material(Shader.Find("Standard")){color=new Color(.12f,.18f,.25f)};blocker.GetComponent<Renderer>().sharedMaterial=opaque;blocker.SetActive(false);
            var target=new RenderTexture(480,480,24);target.Create();camera.targetTexture=target;
            var pixels=new Texture2D(480,480,TextureFormat.RGB24,false);var report=new StringBuilder();
            Color32[] Read(string name)
            {camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,480,480),0,0);pixels.Apply();File.WriteAllBytes(folder+"/"+name+".png",pixels.EncodeToPNG());return pixels.GetPixels32();}
            int Changed(Color32[] a,Color32[] b,int threshold=3)
            {int n=0;for(int i=0;i<a.Length;i++)if(Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b)>threshold)n++;return n;}
            var contact=new Vector3(0,2,0);
            try
            {
                foreach(string shader in new[]{"BeamImpactVolume","BeamContactSpray"})
                    if(ShaderUtil.ShaderHasError(Resources.Load<Shader>(shader)))throw new Exception("Shader error: "+shader);
                foreach(int angle in new[]{0,45,90,135})
                {
                    camera.transform.position=contact+Quaternion.Euler(0,angle,0)*new Vector3(0,.25f,-5);
                    camera.transform.LookAt(contact);effect.Clear();var clear=Read(angle+"-clear");
                    effect.Hit(contact,Vector3.forward);effect.Tick(.20f);var hit=Read(angle+"-hit");
                    int visible=Changed(clear,hit),active=effect.SprayCount;
                    if(visible<500||active<20)throw new Exception("Missing impact/spray "+angle+" "+visible+" "+active);
                    effect.Tick(0);int frozen=Changed(hit,Read(angle+"-frozen"),0);
                    if(frozen!=0)throw new Exception("Paused effect moved");
                    effect.Tick(.18f);int motion=Changed(hit,Read(angle+"-later"));
                    if(motion<300)throw new Exception("Contact effect static");
                    blocker.transform.SetPositionAndRotation(camera.transform.position+camera.transform.forward, camera.transform.rotation);blocker.SetActive(true);
                    var blocked=Read(angle+"-blocked-effect");effect.Clear();int leak=Changed(blocked,Read(angle+"-blocked-clear"),0);blocker.SetActive(false);
                    if(leak!=0)throw new Exception("Impact leaked through foreground "+leak);
                    effect.Hit(contact,Vector3.forward);effect.Tick(BeamImpactVolume.Lifetime+.1f);
                    if(effect.Visible||effect.SprayCount!=0||Changed(clear,Read(angle+"-expired"),0)!=0)throw new Exception("Expired effect remains");
                    report.AppendLine($"angle={angle} visible={visible} active={active} motion={motion} frozen={frozen} depthLeak={leak} expired=passed");
                }
                int objects=root.GetComponentsInChildren<Transform>(true).Length;
                foreach(int hz in new[]{15,30,60})
                {
                    int max=0;effect.Clear();effect.Hit(contact,Vector3.up);
                    for(int i=0;i<hz*3;i++){effect.Tick(1f/hz);max=Math.Max(max,effect.SprayCount);}
                    if(max<20||effect.Visible)throw new Exception("Frame-rate lifetime/vertical axis failed");
                    for(int n=0;n<12;n++){effect.Hit(contact,Vector3.forward);effect.Tick(.21f);effect.Clear();if(effect.Visible)throw new Exception("Reset left spray");}
                    if(root.GetComponentsInChildren<Transform>(true).Length!=objects)throw new Exception("Per-hit object allocation");
                    report.AppendLine($"hz={hz} activePeak={max} vertical=passed reset=passed objects={objects}");
                }
                File.WriteAllText(folder+"/validation.txt",report.ToString());Debug.Log("[BeamImpactSprayReview] "+report);
            }
            finally
            {camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(opaque);UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
