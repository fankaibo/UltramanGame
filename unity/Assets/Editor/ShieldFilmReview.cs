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
    public static class ShieldFilmReview
    {
        static string Root=>Path.GetFullPath(Application.dataPath+"/../../artifacts/shield-film-20261012");
        public static void Before(){Stage("before");Surface("before");}
        public static void After(){Stage("after");Surface("after");}
        static void Stage(string version)
        {
            string folder=Root+"/"+version;Directory.CreateDirectory(folder+"/frames");File.Delete(folder+"/validation.txt");
            var trace=new StringBuilder();var report=new StringBuilder();
            foreach(int hz in new[]{15,30,60})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(137);
                var w=new GameWorld();var h=new AnimatedActor("Tiga",w.HeroHome,w.EnemyHome);var e=new AnimatedActor("Golza",w.EnemyHome,w.HeroHome,true);w.BindActors(h,e);w.SetHeroProfile("Tiga");
                var b=new Battle();b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
                while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});
                for(int f=0;f<1500;f++){b.Tick(.02f,new PlayerInput{Tracking=true,Shield=true});if(b.Enemy==EnemyPhase.Windup&&b.WarningDuration-b.EnemyAge<2.1f)break;}
                if(b.Enemy!=EnemyPhase.Windup)throw new Exception("No warning prepared");while(b.TryCue(out _)){}
                h.Update(b,w.Camera,0,0);e.Update(b,w.Camera,0,0);w.Tick(b,1,0);
                var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();w.Camera.targetTexture=rt;w.Camera.aspect=16f/9;
                int objects=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
                float dt=1f/hz,contact=-1;bool captured=false;
                try
                {
                    for(int f=0;f<hz*6;f++)
                    {
                        b.Tick(w.BattleDelta(dt,b),new PlayerInput{Tracking=true,Shield=true});while(b.TryCue(out var cue))w.Cue(cue,b);
                        h.Update(b,w.Camera,dt,f*dt);e.Update(b,w.Camera,dt,f*dt);w.Tick(b,dt,f*dt);
                        if(b.Blocks>0&&contact<0)contact=f*dt;
                        if(version=="after"&&contact>=0)
                            foreach(var line in UnityEngine.Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None))
                                if((line.name=="Arcade contact shockwave"||line.name=="Arcade impact ray")&&line.enabled)
                                    throw new Exception("Block retained a detached ring or radial marker");
                        var p=w.Camera.transform.position;var r=w.Camera.transform.rotation;
                        trace.AppendLine(FormattableString.Invariant($"{hz},{f},{b.Phase},{b.Enemy},{b.EnemyAge:R},{b.Action},{b.ActionAge:R},{b.Shield},{b.EnemyHealth},{b.Blocks},{b.HitsTaken},{p.x:R},{p.y:R},{p.z:R},{r.x:R},{r.y:R},{r.z:R},{r.w:R},{w.Camera.fieldOfView:R},{h.Root.position.x:R},{h.Root.position.y:R},{h.Root.position.z:R}"));
                        if(hz==60&&f%2==0)RangedContactReview.Save(w.Camera,rt,folder+"/frames/"+(f/2).ToString("D4")+".jpg");
                        if(hz==60&&f==45)CharacterReview.Save(w.Camera,rt,folder+"/ready.png");
                        if(!captured&&contact>=0&&f*dt-contact>=.12f){if(hz==60)CharacterReview.Save(w.Camera,rt,folder+"/impact.png");captured=true;}
                    }
                    if(b.Blocks!=1||b.HitsTaken!=0||!captured)throw new Exception("Missing actual block");
                    int after=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
                    if(objects!=after)throw new Exception("Effects allocated objects during guard");
                    b.Pause();w.Tick(b,dt,6);if(GameObject.Find("Light shield"))throw new Exception("Shield survives pause");
                    w.ResetPresentation();w.Tick(new Battle(),0,0);if(GameObject.Find("Light shield"))throw new Exception("Shield survives new round");
                    report.AppendLine($"{hz}Hz block=1 hurt=0 objects={objects} stable pause=clear newRound=clear");
                }
                finally{w.Camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
            }
            File.WriteAllText(folder+"/state-camera.csv",trace.ToString());
            if(version=="after"&&File.ReadAllText(Root+"/before/state-camera.csv")!=trace.ToString())throw new Exception("Shield changed gameplay, actor root or camera");
            File.WriteAllText(folder+"/validation.txt",report.ToString());Debug.Log("[ShieldFilmStage] PASS "+version+"\n"+report);
        }
        static void Surface(string version)
        {
            string folder=Root+"/"+version+"/surface";Directory.CreateDirectory(folder);File.Delete(folder+"/validation.txt");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var shader=Resources.Load<Shader>("EnergyShield");if(!shader||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new Exception("Shield shader unsupported");
            var material=new Material(shader);var shield=GameObject.CreatePrimitive(PrimitiveType.Sphere);shield.GetComponent<Renderer>().sharedMaterial=material;shield.transform.localScale=new Vector3(1.9f,2.15f,.38f);
            var c=new GameObject("Shield surface camera").AddComponent<Camera>();c.orthographic=true;c.orthographicSize=1.3f;c.aspect=1;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.055f,.07f,.10f);c.allowHDR=false;
            var rt=new RenderTexture(320,320,24);rt.Create();c.targetTexture=rt;var image=new Texture2D(320,320,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            Color32[] Capture(string name){c.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,320,320),0,0);image.Apply();if(name!=null)File.WriteAllBytes(folder+"/"+name+".png",image.EncodeToPNG());return image.GetPixels32();}
            int Difference(Color32[] a,Color32[] b){int max=0;for(int i=0;i<a.Length;i++)max=Math.Max(max,Math.Max(Math.Abs(a[i].r-b[i].r),Math.Max(Math.Abs(a[i].g-b[i].g),Math.Abs(a[i].b-b[i].b))));return max;}
            var metrics=new StringBuilder("angle,centralWash,edgePeak,impactContrast,freezeError,depthError,expiryError\n");
            try
            {
                foreach(int angle in new[]{0,35,65})
                {
                    c.transform.position=Quaternion.Euler(0,angle,0)*new Vector3(0,0,-4);c.transform.LookAt(Vector3.zero);
                    shield.SetActive(false);var background=Capture(null);shield.SetActive(true);material.SetFloat("_Clock",1.25f);material.SetFloat("_HitAge",10);
                    var idle=Capture(angle+"-idle");float wash=0;int n=0;for(int y=136;y<184;y++)for(int x=136;x<184;x++){int i=y*320+x;wash+=Math.Max(0,idle[i].b-background[i].b);n++;}wash/=n;
                    int peak=Difference(background,idle);
                    material.SetVector("_HitPoint",new Vector4(.13f,.08f,0,0));material.SetFloat("_HitAge",.12f);var impact=Capture(angle+"-impact");int contrast=Difference(idle,impact);var frozen=Capture(null);int freeze=Difference(impact,frozen);
                    material.SetFloat("_HitAge",.8f);int expiry=Difference(idle,Capture(null));material.SetFloat("_HitAge",.12f);
                    var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=c.transform.position*.5f;wall.transform.rotation=c.transform.rotation;wall.transform.localScale=new Vector3(6,6,.1f);var opaque=new Material(Shader.Find("Unlit/Color")){color=new Color(.2f,.1f,.07f)};wall.GetComponent<Renderer>().sharedMaterial=opaque;
                    shield.SetActive(false);var covered=Capture(null);shield.SetActive(true);int depth=Difference(covered,Capture(null));UnityEngine.Object.DestroyImmediate(wall);UnityEngine.Object.DestroyImmediate(opaque);
                    if(peak<25||contrast<25||freeze!=0||depth!=0||expiry!=0)throw new Exception($"Surface failed {angle} peak={peak} contact={contrast} freeze={freeze} depth={depth} expiry={expiry}");
                    if(version=="after")
                    {
                        var old=File.ReadAllLines(Root+"/before/surface/metrics.csv");float oldWash=0;foreach(var line in old)if(line.StartsWith(angle+","))oldWash=float.Parse(line.Split(',')[1],System.Globalization.CultureInfo.InvariantCulture);
                        if(wash>oldWash*.65f)throw new Exception($"Center still washes out scene {angle}: {wash}/{oldWash}");
                    }
                    metrics.AppendLine(FormattableString.Invariant($"{angle},{wash:F4},{peak},{contrast},{freeze},{depth},{expiry}"));
                }
                foreach(var message in ShaderUtil.GetShaderMessages(shader))if(message.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)throw new Exception(message.message);
                File.WriteAllText(folder+"/validation.txt","PASS visibility, center transparency, contact, frozen clock and foreground depth\n");Debug.Log("[ShieldFilmSurface] PASS "+version+"\n"+metrics);
            }
            finally{File.WriteAllText(folder+"/metrics.csv",metrics.ToString());RenderTexture.active=previous;c.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(material);}
        }
    }
}
