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
        public static void Validate()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/volcanic-ejecta/inspection"));Directory.CreateDirectory(folder);
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
                report.AppendLine($"passed: visiblePixels={visible} movingPixels={moving} frozenDifference={drift} rewindDifference={rewind} foregroundLeak={leaked} minGround={minimum:F5} activeBombs={low}..{high} fixedMeshes=2");
                File.WriteAllText(folder+"/validation.txt",report.ToString());Debug.Log("[VolcanicEjectaReview] "+report);
            }
            finally{camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(opaque);}
        }
    }
}
