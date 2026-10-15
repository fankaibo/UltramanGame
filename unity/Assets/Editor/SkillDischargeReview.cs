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
    public static class SkillDischargeReview
    {
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/skill-discharge-20261012"));
        public static void After()
        {
            RangedCastReview.After();
            foreach(string file in new[]{"trace.csv","body.csv","extension.csv"})
                if(File.ReadAllText(Folder+"/before/Tiga-60/"+file)!=File.ReadAllText(Folder+"/after/Tiga-60/"+file))
                    throw new Exception("Discharge changed state/body: "+file);
            Check();
        }
        public static void Check()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            string folder=Folder+"/gpu";Directory.CreateDirectory(folder);
            var shader=Resources.Load<Shader>("SkillDischarge");
            if(!shader||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new Exception("Invalid discharge shader");
            var camera=new GameObject("Discharge GPU camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1;camera.aspect=1;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            var rt=new RenderTexture(256,256,24);rt.Create();camera.targetTexture=rt;var image=new Texture2D(256,256,TextureFormat.RGB24,false);
            var root=new GameObject("Discharge review").transform;var fx=new RangedSkillAccent(root);fx.SetTigaStyle(true);fx.MuzzleLighting=true;
            int objects=root.GetComponentsInChildren<Transform>(true).Length;var report=new StringBuilder();Color color=new Color(.18f,.7f,1);
            Color32[] Read(string name)
            {camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,256,256),0,0);image.Apply();File.WriteAllBytes(folder+"/"+name+".png",image.EncodeToPNG());return image.GetPixels32();}
            int Changed(Color32[] a,Color32[] b,int threshold=0)
            {int n=0;for(int j=0;j<a.Length;j++)if(Math.Abs(a[j].r-b[j].r)+Math.Abs(a[j].g-b[j].g)+Math.Abs(a[j].b-b[j].b)>threshold)n++;return n;}
            int Enabled(string prefix)
            {int n=0;foreach(var line in root.GetComponentsInChildren<LineRenderer>())if(line.name.StartsWith(prefix)&&line.enabled)n++;return n;}
            try
            {
                foreach(int angle in new[]{0,45,90,135,180})
                {
                    camera.transform.position=Quaternion.Euler(0,angle,0)*new Vector3(0,0,-4);camera.transform.LookAt(Vector3.zero);
                    fx.Clear();var empty=Read("empty-"+angle);
                    fx.Launch(true,.08f,Vector3.zero,Vector3.forward,color);var launch=Read("launch-"+angle);
                    fx.Launch(true,.08f,Vector3.zero,Vector3.forward,color);int freeze=Changed(launch,Read("launch-repeat-"+angle));
                    if(Enabled("Launch curl")!=0)throw new Exception("Tiga retained wire coils");
                    fx.Clear();fx.Hit(Vector3.zero,Vector3.forward,color);fx.TickImpact(.025f);var hit=Read("hit-"+angle);
                    fx.TickImpact(0);int freezeHit=Changed(hit,Read("hit-repeat-"+angle));
                    fx.TickImpact(.10f);int flow=Changed(hit,Read("outward-"+angle),6);
                    int launchPixels=Changed(empty,launch,12),hitPixels=Changed(empty,hit,12);
                    if(launchPixels<30||hitPixels<30||flow<30||freeze!=0||freezeHit!=0)throw new Exception($"Effect invalid {angle} launch={launchPixels} hit={hitPixels} flow={flow} freeze={freeze}/{freezeHit}");
                    if(Enabled("Ranged contact wave")!=0||Enabled("Directional energy fragment")!=0||Enabled("Contact spark")==0)throw new Exception("Wrong Tiga contact layers");
                    fx.TickImpact(1);if(fx.ImpactVisible||Changed(empty,Read("expired-"+angle))!=0)throw new Exception("Discharge failed to expire");
                    report.AppendLine($"angle={angle} launch={launchPixels} hit={hitPixels} flow={flow} freeze={freeze}/{freezeHit} expired=pass");
                }
                camera.transform.position=new Vector3(0,0,-4);camera.transform.LookAt(Vector3.zero);
                var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.transform.position=new Vector3(0,0,-2);blocker.transform.localScale=new Vector3(4,4,.1f);
                var opaque=new Material(Shader.Find("Unlit/Color")){color=new Color(.2f,.3f,.4f)};blocker.GetComponent<Renderer>().sharedMaterial=opaque;
                fx.Clear();var blocked=Read("blocker-only");fx.Launch(true,.08f,Vector3.zero,Vector3.forward,color);fx.Hit(Vector3.zero,Vector3.forward,color);fx.TickImpact(.025f);
                if(Changed(blocked,Read("occluded"))!=0)throw new Exception("Discharge leaks through foreground");
                UnityEngine.Object.DestroyImmediate(blocker);UnityEngine.Object.DestroyImmediate(opaque);
                fx.SetTigaStyle(false);fx.Launch(true,.08f,Vector3.zero,Vector3.forward,color);fx.Hit(Vector3.zero,Vector3.forward,color);fx.TickImpact(.025f);
                if(Enabled("Launch curl")!=2||Enabled("Ranged contact wave")!=1||Enabled("Directional energy fragment")!=10)throw new Exception("Other heroes lost original layers");
                fx.SetTigaStyle(true);if(fx.ImpactVisible||fx.LaunchVisible||Enabled("Contact spark")!=0)throw new Exception("Style switch retains effects");
                for(int i=0;i<40;i++){fx.Hit(Vector3.zero,Vector3.forward,color);fx.TickImpact(.025f);}
                if(root.GetComponentsInChildren<Transform>(true).Length!=objects)throw new Exception("Discharge pool grew");
                fx.Clear();if(fx.ImpactVisible||fx.LaunchVisible)throw new Exception("Clear retains effects");
                report.AppendLine("occlusion=pass wireCoils/disc/rays=hidden sparks=retained otherHeroes=restored clear/pool40=pass");
                var shotRoot=new GameObject("Flight contact handoff").transform;var shot=new HeroProjectile(shotRoot);shot.SetHero("Tiga");
                foreach(int hz in new[]{15,30,60})
                {
                    var b=new Battle();b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});b.GiveInstructionTime(20);
                    bool flew=false,hit=false;float dt=1f/hz;
                    for(int f=0;f<hz;f++)
                    {
                        b.Tick(dt,new PlayerInput{Tracking=true,RightPunch=f==0,RangedAttack=f==0});
                        if(b.LastDamageRanged){shot.Impact(Vector3.right,Vector3.right);hit=true;}
                        shot.Tick(b,camera,Vector3.left,Vector3.right,false,Vector3.up,dt);
                        bool head=shotRoot.Find("Light bullet moving energy").gameObject.activeSelf;
                        if(!hit&&head)flew=true;
                        if(hit&&head)throw new Exception("Flight head survived actual contact");
                    }
                    if(!flew||!hit||shot.Impacts!=1||shot.Launches!=1)throw new Exception("Flight/contact handoff incomplete");
                    report.AppendLine($"handoff hz={hz} flight=visible contact=headHidden impacts=1 nextRound=reset");
                }
                File.WriteAllText(folder+"/validation.txt",report.ToString());Debug.Log("[SkillDischargeReview] PASS\n"+report);
            }
            finally{camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);}
        }
    }
}
