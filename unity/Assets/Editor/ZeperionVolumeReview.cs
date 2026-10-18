using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Runtime;
namespace UltramanGame.Editor
{
    public static class ZeperionVolumeReview
    {
        public static void Check()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            string folder=Path.GetFullPath("../artifacts/a172-beam-volume/gpu");Directory.CreateDirectory(folder);
            var camera=new GameObject("Beam volume camera").AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=42;camera.aspect=16f/9;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.055f,.07f,.10f);camera.depthTextureMode=DepthTextureMode.Depth;camera.allowHDR=false;
            var rt=new RenderTexture(480,270,24);rt.Create();camera.targetTexture=rt;
            var pixels=new Texture2D(480,270,TextureFormat.RGB24,false);var root=new GameObject("Beam volume fixture").transform;var beam=new BeamStream(root);
            var tint=new Color(.25f,.72f,1);var accent=new Color(.9f,.98f,1);var origin=new Vector3(-1.2f,0,0);var tip=-origin;
            var line=root.Find("Zeperion traveling stream").GetComponent<LineRenderer>();var volume=root.Find("Zeperion spatial discharge");
            int objects=root.GetComponentsInChildren<Transform>(true).Length;var report=new StringBuilder();
            Color32[] Read(string name){camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,480,270),0,0);pixels.Apply();File.WriteAllBytes(folder+"/"+name+".png",pixels.EncodeToPNG());return pixels.GetPixels32();}
            int Changed(Color32[] a,Color32[] b,int threshold=0){int n=0;for(int i=0;i<a.Length;i++)if(Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b)>threshold)n++;return n;}
            try
            {
                foreach(int angle in new[]{0,45,75,90,135})
                {
                    camera.transform.position=Quaternion.Euler(0,angle,0)*new Vector3(0,0,-4);camera.transform.LookAt(Vector3.zero);
                    beam.SetProfile(tint,accent,false,true);var blank=Read("blank-"+angle);
                    beam.Tick(camera,origin,tip,.75f,true,2);var live=Read("live-"+angle);Vector3 position=volume.position,scale=volume.localScale;Quaternion rotation=volume.rotation;
                    if(line.enabled||!volume.gameObject.activeSelf||!beam.Visible)throw new Exception("Tiga did not switch to volume");
                    if(Vector3.Distance(volume.TransformPoint(Vector3.back*.5f),origin)>.00001f||Vector3.Distance(volume.TransformPoint(Vector3.forward*.5f),tip)>.00001f)throw new Exception("Volume endpoints detached");
                    beam.Tick(camera,origin,tip,.75f,true,2);int repeat=Changed(live,Read("repeat-"+angle));
                    beam.Tick(camera,origin,tip,.75f,true,2.13f);int flow=Changed(live,Read("flow-"+angle),3);
                    int visible=Changed(blank,live,8);if(repeat!=0||flow<20||visible<50)throw new Exception($"Volume pixels failed angle={angle} visible={visible} repeat={repeat} flow={flow}");
                    if(position!=volume.position||scale!=volume.localScale||Quaternion.Angle(rotation,volume.rotation)>.0001f)throw new Exception("Flow moves beam transform");
                    beam.Clear();if(beam.Visible||volume.gameObject.activeSelf||Changed(blank,Read("clear-"+angle))!=0)throw new Exception("Volume did not clear");
                    report.AppendLine($"angle={angle} visible={visible} repeat={repeat} flow={flow} endpoints=passed clear=passed");
                }
                camera.transform.position=new Vector3(0,0,-4);camera.transform.LookAt(Vector3.zero);
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(0,0,-1.2f);wall.transform.localScale=new Vector3(5,5,.1f);
                var material=new Material(Shader.Find("Standard")){color=new Color(.3f,.35f,.4f)};wall.GetComponent<Renderer>().sharedMaterial=material;
                beam.Clear();var covered=Read("wall-only");beam.Tick(camera,origin,tip,.75f,true,2);
                int leak=Changed(covered,Read("behind-wall"));if(leak!=0)throw new Exception("Beam volume leaks through scene depth: "+leak);
                UnityEngine.Object.DestroyImmediate(wall);UnityEngine.Object.DestroyImmediate(material);
                for(int i=0;i<40;i++)beam.Tick(camera,origin,tip,.3f+(i%12)*.08f,true,i/30f);
                if(root.GetComponentsInChildren<Transform>(true).Length!=objects)throw new Exception("Per-tick beam objects allocated");
                beam.SetProfile(tint,accent,true,false);if(beam.Visible)throw new Exception("Hero switch retained beam");beam.Tick(camera,origin,tip,.75f,true,2);
                if(!line.enabled||volume.gameObject.activeSelf||!root.Find("Twin finisher traveling stream").GetComponent<LineRenderer>().enabled)throw new Exception("Other hero style lost");
                beam.Tick(camera,origin,tip,.75f,false,2);if(beam.Visible)throw new Exception("Cancel retained beam");
                foreach(var msg in UnityEditor.ShaderUtil.GetShaderMessages(Resources.Load<Shader>("ZeperionVolume")))if(msg.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)throw new Exception(msg.message);
                report.AppendLine("depthLeak=0 objects=stable heroSwitch/twin/cancel=passed");File.WriteAllText(folder+"/validation.txt",report.ToString());Debug.Log("[ZeperionVolumeReview] passed");
            }
            finally{camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);}
        }
    }
}
