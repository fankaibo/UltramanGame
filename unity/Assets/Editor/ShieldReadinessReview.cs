using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace UltramanGame.Editor
{
    public static class ShieldReadinessReview
    {
        public static void After(){ShieldFilmReview.PreparationAfter();Surface();}
        static void Surface()
        {
            string folder=Path.GetFullPath("../artifacts/a174-shield-readiness/gpu");Directory.CreateDirectory(folder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var baseline=ShaderUtil.CreateShaderAsset(File.ReadAllText(Path.GetFullPath("../artifacts/a174-shield-readiness/before-EnergyShield.shader")).Replace("Training/EnergyShield","Training/ShieldReadinessBaseline"));
            var prior=new Material(baseline);var material=new Material(Resources.Load<Shader>("EnergyShield"));
            var shield=GameObject.CreatePrimitive(PrimitiveType.Sphere);var renderer=shield.GetComponent<Renderer>();shield.transform.localScale=new Vector3(1.9f,2.15f,.38f);
            var camera=new GameObject("Shield readiness camera").AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=1.3f;camera.aspect=1;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.055f,.07f,.10f);camera.allowHDR=false;
            var target=new RenderTexture(320,320,24);target.Create();camera.targetTexture=target;var image=new Texture2D(320,320,TextureFormat.RGB24,false);
            Color32[] Read(string name){camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,320,320),0,0);image.Apply();File.WriteAllBytes(folder+"/"+name+".png",image.EncodeToPNG());return image.GetPixels32();}
            int Difference(Color32[] a,Color32[] b){int max=0;for(int i=0;i<a.Length;i++)max=Math.Max(max,Math.Max(Math.Abs(a[i].r-b[i].r),Math.Max(Math.Abs(a[i].g-b[i].g),Math.Abs(a[i].b-b[i].b))));return max;}
            double Energy(Color32[] a,Color32[] b){double total=0;for(int i=0;i<a.Length;i++)total+=Math.Max(0,a[i].r-b[i].r)+Math.Max(0,a[i].g-b[i].g)+Math.Max(0,a[i].b-b[i].b);return total;}
            var report=new StringBuilder();
            try
            {
                foreach(int angle in new[]{0,35,65})
                {
                    camera.transform.position=Quaternion.Euler(0,angle,0)*new Vector3(0,0,-4);camera.transform.LookAt(Vector3.zero);
                    foreach(var m in new[]{prior,material}){m.SetFloat("_Clock",1.25f);m.SetFloat("_HitAge",10);m.SetVector("_HitPoint",new Vector4(.13f,.08f,0,0));}
                    shield.SetActive(false);var blank=Read(angle+"-blank");shield.SetActive(true);renderer.sharedMaterial=prior;var old=Read(angle+"-old");
                    renderer.sharedMaterial=material;material.SetFloat("_Readiness",1);var threat=Read(angle+"-threat");int fullError=Difference(old,threat);
                    material.SetFloat("_Readiness",.35f);var calm=Read(angle+"-ready");double ratio=Energy(calm,blank)/Energy(old,blank);int visible=Difference(calm,blank);
                    material.SetFloat("_HitAge",.12f);var readyImpact=Read(angle+"-ready-impact");int contrast=Difference(calm,readyImpact);int freeze=Difference(readyImpact,Read(angle+"-repeat"));
                    material.SetFloat("_Readiness",1);var impact=Read(angle+"-threat-impact");prior.SetFloat("_HitAge",.12f);renderer.sharedMaterial=prior;int hitError=Difference(impact,Read(angle+"-old-impact"));
                    renderer.sharedMaterial=material;material.SetFloat("_Readiness",.35f);material.SetFloat("_HitAge",.8f);int expire=Difference(calm,Read(angle+"-expired"));
                    if(fullError>1||hitError>1||ratio>.65||ratio<.15||visible<15||contrast<25||freeze!=0||expire!=0)throw new Exception($"Readiness surface failed {angle} full={fullError} hit={hitError} ratio={ratio} visible={visible} contact={contrast} freeze={freeze} expire={expire}");
                    report.AppendLine(FormattableString.Invariant($"angle={angle} readyEnergyRatio={ratio:F4} readyPeak={visible} readyContact={contrast} fullError={fullError} hitError={hitError} freeze={freeze} expiry={expire}"));
                }
                foreach(var shader in new[]{baseline,material.shader})if(ShaderUtil.ShaderHasError(shader))throw new Exception("Shield shader error");
                File.WriteAllText(folder+"/validation.txt",report.ToString());Debug.Log("[ShieldReadinessReview] passed\n"+report);
            }
            finally{camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(material);UnityEngine.Object.DestroyImmediate(prior);UnityEngine.Object.DestroyImmediate(baseline);}
        }
    }
}
