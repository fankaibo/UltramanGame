using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class GroundAshReview
    {
        public static void Check()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            string folder=Path.GetFullPath("../artifacts/a179-ground-ash/gpu");Directory.CreateDirectory(folder);
            var camera=new GameObject("Ground ash camera").AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=48;camera.aspect=16f/9;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.045f,.055f,.07f);camera.depthTextureMode=DepthTextureMode.Depth;
            var target=new RenderTexture(640,360,24);target.Create();camera.targetTexture=target;
            var pixels=new Texture2D(640,360,TextureFormat.RGB24,false);
            var noise=VolcanoStage.CreatePlumeNoise();var material=new Material(Resources.Load<Shader>("LowGroundAsh"));material.SetTexture("_Noise",noise);
            var ash=GameObject.CreatePrimitive(PrimitiveType.Cube);ash.transform.position=new Vector3(0,.65f,0);ash.transform.localScale=new Vector3(19,1.7f,8);
            var renderer=ash.GetComponent<Renderer>();renderer.sharedMaterial=material;
            var report=new StringBuilder();
            Color32[] Read(string name){camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,640,360),0,0);pixels.Apply();File.WriteAllBytes(folder+"/"+name+".png",pixels.EncodeToPNG());return pixels.GetPixels32();}
            int Changed(Color32[] a,Color32[] b,int threshold=0){int n=0;for(int i=0;i<a.Length;i++)if(Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b)>threshold)n++;return n;}
            try
            {
                foreach(int angle in new[]{0,45,75})
                {
                    camera.transform.position=Quaternion.Euler(0,angle,0)*new Vector3(0,2.4f,-9);camera.transform.LookAt(new Vector3(0,.4f,0));
                    material.SetFloat("_Clock",3);renderer.enabled=false;var clear=Read("clear-"+angle);renderer.enabled=true;
                    var live=Read("live-"+angle);int visible=Changed(clear,live,6),repeat=Changed(live,Read("repeat-"+angle));
                    material.SetFloat("_Clock",5);int flow=Changed(live,Read("later-"+angle),3);
                    if(visible<1500||repeat!=0||flow<500)throw new Exception($"Ground ash visible={visible} repeat={repeat} flow={flow}");
                    report.AppendLine($"angle={angle} visible={visible} repeat={repeat} flow={flow} passed");
                }
                camera.transform.position=new Vector3(0,.7f,-7);camera.transform.LookAt(new Vector3(0,.7f,0));
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(0,.7f,-4.3f);wall.transform.localScale=new Vector3(22,5,.1f);
                renderer.enabled=false;var front=Read("opaque-front");renderer.enabled=true;int leak=Changed(front,Read("opaque-front-ash"));
                if(leak!=0)throw new Exception("Ash leaks onto foreground surfaces: "+leak);
                UnityEngine.Object.DestroyImmediate(wall);
                camera.transform.position=new Vector3(0,.55f,0);camera.transform.rotation=Quaternion.identity;
                renderer.enabled=false;var insideClear=Read("inside-clear");renderer.enabled=true;int inside=Changed(insideClear,Read("inside-ash"),6);
                if(inside<1500)throw new Exception("Ash disappears inside the volume");
                int objects=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length;
                for(int i=0;i<180;i++)material.SetFloat("_Clock",i/30f);
                if(UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length!=objects)throw new Exception("Ash update allocated objects");
                if(ShaderUtil.ShaderHasError(material.shader))throw new Exception("Ground ash shader compilation failed");
                report.AppendLine($"foregroundLeak=0 inside={inside} objects=stable shader=passed");
                File.WriteAllText(folder+"/validation.txt",report.ToString());Debug.Log("[GroundAshReview] "+report);
            }
            finally{camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(material);UnityEngine.Object.DestroyImmediate(noise);}
        }
    }
}
