using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;
namespace UltramanGame.Editor
{
    public static class RangedSmokeReview
    {
        public static void Validate(){Gpu();SkillDischargeReview.Check();RangedLifecycleReview.Validate();}
        public static void Before()=>Run(false);
        public static void After()=>Run(true);
        static void Gpu()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            string folder=Path.GetFullPath("../artifacts/a171-ranged-smoke/gpu");Directory.CreateDirectory(folder);
            var camera=new GameObject("Smoke depth camera").AddComponent<Camera>();camera.transform.position=new Vector3(0,0,-3);camera.fieldOfView=35;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.25f,.3f,.35f);camera.depthTextureMode=DepthTextureMode.Depth;camera.allowHDR=false;
            var rt=new RenderTexture(320,320,24);rt.Create();camera.targetTexture=rt;camera.aspect=1;
            var pixels=new Texture2D(320,320,TextureFormat.RGB24,false);var root=new GameObject("Smoke review").transform;var smoke=new RangedImpactSmoke(root);
            int objects=root.GetComponentsInChildren<Transform>(true).Length;var report=new StringBuilder();var tint=new Color(.18f,.7f,1);
            Color32[] Read(string name){camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,320,320),0,0);pixels.Apply();File.WriteAllBytes(folder+"/"+name+".png",pixels.EncodeToPNG());return pixels.GetPixels32();}
            int Changed(Color32[] a,Color32[] b,int threshold=0){int count=0;for(int i=0;i<a.Length;i++)if(Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b)>threshold)count++;return count;}
            try
            {
                foreach(int angle in new[]{0,45,90})
                {
                    camera.transform.position=Quaternion.Euler(0,angle,0)*new Vector3(0,0,-3);camera.transform.LookAt(Vector3.zero);
                    smoke.Clear();var empty=Read("empty-"+angle);smoke.Hit(Vector3.zero,Vector3.forward,tint);smoke.Tick(.18f);var live=Read("live-"+angle);
                    smoke.Tick(0);smoke.Tick(-1);int repeat=Changed(live,Read("repeat-"+angle));int count=Changed(empty,live,8);
                    smoke.Tick(1);if(smoke.Visible!=0||Changed(empty,Read("expired-"+angle))!=0||count<100||repeat!=0)throw new Exception("Smoke visibility/freeze/expiry failed");
                    report.AppendLine($"angle={angle} changedPixels={count} repeat={repeat} expiration=passed");
                }
                camera.transform.position=new Vector3(0,0,-3);camera.transform.LookAt(Vector3.zero);
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(0,0,-1.5f);wall.transform.localScale=new Vector3(5,5,.1f);
                var opaque=new Material(Shader.Find("Standard")){color=new Color(.3f,.35f,.4f)};wall.GetComponent<Renderer>().sharedMaterial=opaque;
                smoke.Clear();var covered=Read("wall-only");smoke.Hit(Vector3.zero,Vector3.forward,tint);smoke.Tick(.18f);
                int leak=Changed(covered,Read("behind-wall"));if(leak!=0)throw new Exception("Smoke leaks through scene depth: "+leak);
                UnityEngine.Object.DestroyImmediate(wall);UnityEngine.Object.DestroyImmediate(opaque);
                Vector3 position=Vector3.zero,scale=Vector3.zero;
                foreach(int hz in new[]{15,30,60})
                {
                    smoke.Clear();smoke.Hit(Vector3.zero,Vector3.forward,tint);for(int i=0;i<hz/5;i++)smoke.Tick(1f/hz);
                    var puff=root.GetChild(0);if(hz>15&&(Vector3.Distance(position,puff.position)>.00001f||Vector3.Distance(scale,puff.localScale)>.00001f))throw new Exception("Rate changed smoke path");position=puff.position;scale=puff.localScale;
                }
                smoke.Clear();for(int i=0;i<40;i++){smoke.Hit(Vector3.right*(i%3*.1f),Vector3.forward,tint);smoke.Tick(.025f);}
                Read("overlap");if(smoke.Visible!=4||root.GetComponentsInChildren<Transform>(true).Length!=objects)throw new Exception("Smoke pool changed");smoke.Clear();if(smoke.Visible!=0)throw new Exception("Smoke clear failed");
                foreach(var m in UnityEditor.ShaderUtil.GetShaderMessages(Resources.Load<Shader>("RangedImpactSmoke")))if(m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)throw new Exception(m.message);
                report.AppendLine("depthLeak=0 rates=15/30/60 pool=4 objects=stable clear=passed");File.WriteAllText(folder+"/validation.txt",report.ToString());
            }
            finally{camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);}
        }
        static void Run(bool verify)
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--contact-output");
            string output=index>=0?args[index+1]:Path.Combine(Application.dataPath,"../../artifacts/a171-ranged-smoke");
            foreach(int hz in verify?new[]{15,30,60}:new[]{60})
            {
                string folder=Path.GetFullPath(Path.Combine(output,verify?"after":"before",hz.ToString()));Directory.CreateDirectory(folder);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(1988);
                var world=new GameWorld();var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);
                var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);world.SetHeroProfile("Tiga");
                var state=new Battle(50);state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
                while(state.Phase!=GamePhase.Battle)state.Tick(.02f,new PlayerInput{Tracking=true});state.GiveInstructionTime(30);while(state.TryCue(out _)){}
                for(int f=0;f<40;f++){hero.Update(state,world.Camera,1/60f,f/60f);enemy.Update(state,world.Camera,1/60f,f/60f);world.Tick(state,1/60f,f/60f);}
                var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
                int objects=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
                float health=50,dt=1f/hz;int hits=0;
                var csv=new StringBuilder("frame,phase,action,age,shotAge,health,energy,hits,visible\n");
                try
                {
                    for(int f=0;f<hz*4;f++)
                    {
                        var input=new PlayerInput{Tracking=true};
                        if(f==hz/2||f==hz||f==hz*3/2){input.RangedAttack=true;input.LeftPunch=f==hz;input.RightPunch=!input.LeftPunch;input.AttackSpeed=1.7f;}
                        state.Tick(dt,input);while(state.TryCue(out var cue))world.Cue(cue,state);
                        hero.Update(state,world.Camera,dt,f*dt);enemy.Update(state,world.Camera,dt,f*dt);
                        if(state.EnemyHealth<health){hits++;world.Hit(false,state);}health=state.EnemyHealth;world.Tick(state,dt,f*dt);
                        csv.AppendLine(FormattableString.Invariant($"{f},{state.Phase},{state.Action},{state.ActionAge:R},{state.Shot.Age:R},{health},{state.Energy},{hits},{world.Projectile.ImpactVisible}"));
                        if(hz==60&&f%2==0)CharacterReview.Save(world.Camera,rt,Path.Combine(folder,$"{f/2:D4}.jpg"));
                        if(hz==60&&f==36)
                        {
                            var wake=GameObject.Find("Light bullet short trail").GetComponent<LineRenderer>();
                            var mesh=new Mesh();wake.BakeMesh(mesh,world.Camera,true);
                            var detail=new StringBuilder();
                            for(int i=0;i<wake.positionCount;i++)detail.AppendLine("point "+wake.GetPosition(i).ToString("R"));
                            var vertices=mesh.vertices;var uv=mesh.uv;
                            for(int i=0;i<vertices.Length;i++)detail.AppendLine($"vertex {vertices[i].ToString("R")} uv {uv[i].ToString("R")}");
                            File.WriteAllText(Path.Combine(folder,"wake-mesh.txt"),detail.ToString());UnityEngine.Object.DestroyImmediate(mesh);
                        }
                    }
                    if(hits!=3||world.Projectile.Impacts!=3||state.EnemyHealth!=47||state.Energy!=3)throw new Exception("Contact changed combat receipts");
                    if(world.Projectile.ImpactVisible)throw new Exception("Contact effects did not expire");
                    if(objects!=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length)throw new Exception("Per-hit objects allocated");
                    world.Projectile.Impact(world.BeamTarget,world.BattleAxis);state.Pause();world.Tick(state,0,4);
                    if(world.Projectile.ImpactVisible)throw new Exception("Pause retained hit effects");world.ResetPresentation();
                    File.WriteAllText(Path.Combine(folder,"trace.csv"),csv.ToString());
                    Debug.Log($"[RangedSmokeReview] PASS hz={hz} hits={hits} health={health} pool=stable pause=clear");
                }
                finally{world.Camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
            }
        }
    }
}
