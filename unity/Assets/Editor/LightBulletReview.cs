using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class LightBulletReview
    {
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/light-bullet-20261012"));
        public static void After()
        {
            RangedCastReview.After();
            foreach(string file in new[]{"trace.csv","body.csv","extension.csv"})
                if(File.ReadAllText(Folder+"/before/Tiga-60/"+file)!=File.ReadAllText(Folder+"/after/Tiga-60/"+file))
                    throw new Exception("Projectile presentation changed state/body: "+file);
            Check();
        }
        public static void Check()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            string folder=Folder+"/gpu";Directory.CreateDirectory(folder);
            foreach(string name in new[]{"LightBullet","ProjectileWake"})
            {
                var shader=Resources.Load<Shader>(name);
                if(!shader||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new Exception("Invalid shader: "+name);
            }
            var camera=new GameObject("Projectile GPU camera").AddComponent<Camera>();camera.transform.position=new Vector3(0,0,-4);
            camera.orthographic=true;camera.orthographicSize=.8f;camera.aspect=1;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            var rt=new RenderTexture(256,256,24);rt.Create();camera.targetTexture=rt;
            var image=new Texture2D(256,256,TextureFormat.RGB24,false);
            var root=new GameObject("Projectile checks").transform;
            var bullet=RangedSkillAccent.Volume(root,"Volume probe",out var material);material.shader=Resources.Load<Shader>("LightBullet");
            material.color=new Color(.18f,.7f,1,1);material.SetFloat("_Age",.18f);bullet.localScale=new Vector3(.5f,.5f,1.16f);
            var report=new StringBuilder();
            Color32[] Read(string name)
            {camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,256,256),0,0);image.Apply();File.WriteAllBytes(folder+"/"+name+".png",image.EncodeToPNG());return image.GetPixels32();}
            int Changed(Color32[] a,Color32[] b,int threshold=0)
            {int n=0;for(int j=0;j<a.Length;j++)if(Math.Abs(a[j].r-b[j].r)+Math.Abs(a[j].g-b[j].g)+Math.Abs(a[j].b-b[j].b)>threshold)n++;return n;}
            try
            {
                bullet.gameObject.SetActive(false);var empty=Read("empty");bullet.gameObject.SetActive(true);
                foreach(int angle in new[]{0,45,90,135,180})
                {
                    bullet.rotation=Quaternion.Euler(0,angle,0);var first=Read("angle-"+angle);
                    int visible=Changed(empty,first,18),repeat=Changed(first,Read("repeat-"+angle));
                    material.SetFloat("_Age",.23f);int moving=Changed(first,Read("flow-"+angle),6);material.SetFloat("_Age",.18f);
                    if(visible<50||repeat!=0||moving<20)throw new Exception($"Volume invalid {angle} visible={visible} repeat={repeat} moving={moving}");
                    report.AppendLine($"angle={angle} visible={visible} repeat={repeat} moving={moving}");
                }
                var color=material.color;color.a=0;material.color=color;
                if(Changed(empty,Read("zero-alpha"))!=0)throw new Exception("Faded projectile still visible");
                color.a=1;material.color=color;
                var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.transform.position=new Vector3(0,0,-2);blocker.transform.localScale=new Vector3(4,4,.1f);
                var opaque=new Material(Shader.Find("Unlit/Color")){color=new Color(.2f,.3f,.4f)};blocker.GetComponent<Renderer>().sharedMaterial=opaque;
                var hidden=Read("occluded");bullet.gameObject.SetActive(false);
                if(Changed(hidden,Read("blocker-only"))!=0)throw new Exception("Projectile leaks through foreground");
                UnityEngine.Object.DestroyImmediate(blocker);UnityEngine.Object.DestroyImmediate(opaque);
                // Exercise the actual material selection, existing object pool,
                // zero-time ticking, cancellation and hero switching path.
                var fx=new HeroProjectile(root);int objects=root.GetComponentsInChildren<Transform>(true).Length;
                foreach(string hero in new[]{"Tiga","Zero","Mebius","Geed","Grigio","Tiga"})
                {
                    fx.SetHero(hero);
                    var core=root.Find("Light bullet moving energy").GetComponent<Renderer>().sharedMaterial;
                    if(core.shader.name!=(hero=="Tiga"?"Training/LightBullet":"Training/SkillEnergy"))throw new Exception("Wrong hero shader: "+hero);
                    foreach(int hz in new[]{15,30,60})
                    {
                        var b=new Battle();b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});b.GiveInstructionTime(20);
                        float dt=1f/hz;bool launched=false;
                        for(int j=0;j<hz;j++)
                        {
                            b.Tick(dt,new PlayerInput{Tracking=true,RightPunch=j==0,RangedAttack=j==0});
                            fx.Tick(b,camera,Vector3.left,Vector3.right,false,Vector3.up,dt);
                            launched|=fx.Visible;var tip=fx.Tip;fx.Tick(b,camera,Vector3.left,Vector3.right,false,Vector3.up,0);
                            if(Vector3.Distance(tip,fx.Tip)>.0001f)throw new Exception("Zero time changes path");
                        }
                        if(!launched||fx.Visible||b.Punches!=1)throw new Exception("Flight lifetime broken");
                        b.Pause();fx.Tick(b,camera,Vector3.left,Vector3.right,false,Vector3.up,0);
                        if(fx.Visible||fx.LaunchVisible||fx.ImpactVisible)throw new Exception("Pause retained effect");
                    }
                    if(root.GetComponentsInChildren<Transform>(true).Length!=objects)throw new Exception("Per-flight allocation");
                    report.AppendLine(hero+" 15/30/60Hz material/flight/zeroTime/pause/pool=pass");
                }
                File.WriteAllText(folder+"/validation.txt",report.ToString());Debug.Log("[LightBulletReview] PASS\n"+report);
            }
            finally{camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);}
        }
    }
}
