using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class MonsterRockDustReview
    {
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/rock-dust-20261011"));
        public static void Before()=>Run(false);
        public static void After(){Run(true);FlowStops();}
        static int Clouds(GameWorld w)=>(int)(typeof(MonsterRock).GetProperty("ActiveDust")?.GetValue(w.Rock)??0);
        public static void Lifecycle()
        {
            var report=new StringBuilder();
            foreach(int hz in new[]{15,30,60})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var root=new GameObject("Dust inspection").transform;var camera=new GameObject("Camera").AddComponent<Camera>();
                var dust=new BoulderDust(root);var children=root.GetComponentsInChildren<Renderer>(true);
                if(children.Length!=18)throw new Exception("Unbounded dust pool");
                for(int f=0;f<=hz;f++)
                {
                    float age=f/(float)hz;var p=new Vector3(age*3,2+Mathf.Sin(age*3),0);
                    dust.Tick(2,age,p,age>=MonsterRockMotion.Contact,new Vector3(2,2,0),camera);
                    var positions=new Vector3[18];var scales=new Vector3[18];var visible=new bool[18];
                    for(int i=0;i<18;i++){positions[i]=children[i].transform.position;scales[i]=children[i].transform.localScale;visible[i]=children[i].gameObject.activeSelf;}
                    int count=dust.Visible;dust.Tick(2,age,p,age>=MonsterRockMotion.Contact,new Vector3(2,2,0),camera);
                    if(count!=dust.Visible)throw new Exception("Zero-time emitted dust");
                    camera.transform.position+=Vector3.right*.01f;
                    dust.Tick(2,age,p,age>=MonsterRockMotion.Contact,new Vector3(2,2,0),camera);
                    for(int i=0;i<18;i++)if(Vector3.Distance(positions[i],children[i].transform.position)>.000001f||Vector3.Distance(scales[i],children[i].transform.localScale)>.000001f||visible[i]!=children[i].gameObject.activeSelf)throw new Exception("Repeated clock or camera motion dragged dust");
                }
                dust.Tick(2,2,new Vector3(3,2,0),true,new Vector3(2,2,0),camera);
                if(dust.Visible!=0)throw new Exception("Dust failed to expire");
                dust.Clear();dust.Tick(2,.90f,new Vector3(3,2,0),true,new Vector3(2,2,0),camera);
                if(dust.Visible!=0)throw new Exception("Pause reconstructed old impact");
                dust.Clear();dust.Tick(3,0,Vector3.zero,false,Vector3.zero,camera);
                if(dust.Visible!=0||root.GetComponentsInChildren<Renderer>(true).Length!=18)throw new Exception("Restart retained dust or grew pool");
                report.AppendLine($"{hz}Hz fixedPool=18 zeroTime=passed cameraWorldPositions=passed decay=passed pauseResume=passed restart=passed");
            }
            File.WriteAllText(Folder+"/lifecycle.txt",report.ToString());Debug.Log("[RockDustLifecycle] PASS\n"+report);
        }
        static void FlowStops()
        {
            foreach(string mode in new[]{"pause","showcase","reset","new-round"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var b=new Battle();b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
                while(!(b.EnemyAttackCount==1&&b.Enemy==EnemyPhase.Windup))b.Tick(.02f,new PlayerInput{Tracking=true,Shield=true});
                while(b.TryCue(out _)){}
                var w=new GameWorld();var h=new AnimatedActor("Tiga",w.HeroHome,w.EnemyHome);var e=new AnimatedActor("Golza",w.EnemyHome,w.HeroHome,true);w.BindActors(h,e);
                float time=0;
                while(!(b.EnemyAttackCount==2&&b.Enemy==EnemyPhase.Attack&&b.EnemyAge>.55f))
                {
                    b.Tick(.02f,new PlayerInput{Tracking=true,Shield=true});while(b.TryCue(out var c))w.Cue(c,b);
                    h.Update(b,w.Camera,.02f,time);e.Update(b,w.Camera,.02f,time);w.Tick(b,.02f,time);time+=.02f;
                }
                if(w.Rock.ActiveDust==0)throw new Exception("No dust before "+mode);
                if(mode=="pause")b.Pause();if(mode=="showcase")w.Showcase=true;if(mode=="reset")w.ResetPresentation();if(mode=="new-round")b=new Battle();
                if(mode!="reset")w.Tick(b,0,time);
                if(w.Rock.ActiveDust!=0)throw new Exception("Dust survived "+mode);
                Debug.Log("[RockDustFlowStop] PASS "+mode);
            }
        }
        static void Run(bool check)
        {
            string folder=Folder+(check?"/after":"/before");Directory.CreateDirectory(folder);
            var trace=new StringBuilder();var report=new StringBuilder();
            foreach(int hz in new[]{15,30,60})foreach(bool blocked in new[]{true,false})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(114);
                var b=new Battle();b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
                while(!(b.EnemyAttackCount==1&&b.Enemy==EnemyPhase.Windup))b.Tick(.02f,new PlayerInput{Tracking=true,Shield=true});
                while(b.TryCue(out _)){}
                var world=new GameWorld();var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
                float dt=1f/hz,time=0;int frame=0,movie=0,maxDust=0;bool flight=false,impact=false,linger=false;float peak=0,peakAge=0;
                string path=folder+"/"+(blocked?"guard":"hurt")+"-"+hz;Directory.CreateDirectory(path);bool render=hz==60;if(render)Directory.CreateDirectory(path+"/frames");
                try
                {
                    for(int f=0;f<hz*12;f++)
                    {
                        b.Tick(dt,new PlayerInput{Tracking=true,Shield=blocked});while(b.TryCue(out var cue))world.Cue(cue,b);
                        hero.Update(b,world.Camera,dt,time);enemy.Update(b,world.Camera,dt,time);world.Tick(b,dt,time);time+=dt;
                        bool capture=b.Enemy==EnemyPhase.Windup&&b.WarningDuration-b.EnemyAge<1.5f||b.EnemyAttackCount==2;
                        if(capture)
                        {
                            var p=world.Rock.Position;var h=hero.HandPosition;var e=enemy.HandPosition;
                            trace.AppendLine(FormattableString.Invariant($"{hz},{blocked},{frame},{b.Enemy},{b.EnemyAge:F5},{b.Action},{b.ActionAge:F5},{b.Blocks},{b.HitsTaken},{p.x:F5},{p.y:F5},{p.z:F5},{h.x:F5},{h.y:F5},{h.z:F5},{e.x:F5},{e.y:F5},{e.z:F5}"));
                            int clouds=Clouds(world);maxDust=Mathf.Max(maxDust,clouds);
                            if(blocked&&b.Enemy==EnemyPhase.Attack)
                            {
                                var lens=world.Camera.GetComponent<CinematicCamera>();
                                var motion=(Vector4)typeof(CinematicCamera).GetField("motion",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(lens);
                                if(motion.x>peak){peak=motion.x;peakAge=b.EnemyAge;}
                            }
                            if(world.Rock.Flying&&clouds>0)flight=true;
                            if(world.Rock.Fragments&&clouds>0)impact=true;
                            if(b.Enemy==EnemyPhase.Recover&&clouds>0)linger=true;
                            if(check&&b.Enemy==EnemyPhase.Windup&&clouds!=0)throw new Exception("Dust before throw");
                            if(render&&frame%2==0)RangedContactReview.Save(world.Camera,rt,$"{path}/frames/{movie++:D4}.jpg");
                            if(render&&b.Enemy==EnemyPhase.Attack&&Mathf.Abs(b.EnemyAge-.55f)<dt*.51f)CharacterReview.Save(world.Camera,rt,path+"/flight.png");
                            if(render&&b.Enemy==EnemyPhase.Attack&&Mathf.Abs(b.EnemyAge-.90f)<dt*.51f)CharacterReview.Save(world.Camera,rt,path+"/contact.png");
                            frame++;
                        }
                        if(b.EnemyAttackCount==2&&b.Enemy==EnemyPhase.Rest)break;
                    }
                    if(b.EnemyAttackCount!=2||b.Blocks!=(blocked?2:1)||b.HitsTaken!=(blocked?0:1)||world.Rock.Launches!=1)throw new Exception("Rock battle did not complete");
                    if(check&&blocked&&Mathf.Abs(peakAge-MonsterRockMotion.Contact)>1.01f/hz)throw new Exception("Lens peak precedes stone contact: "+peakAge);
                    if(check&&(!flight||!impact||!linger||Clouds(world)!=0||maxDust>18))throw new Exception($"Dust lifecycle missing flight={flight} impact={impact} linger={linger} end={Clouds(world)} max={maxDust}");
                    report.AppendLine($"{hz}Hz blocked={blocked} launches=1 blocks={b.Blocks} hurt={b.HitsTaken} frames={movie} maxDust={maxDust} lensPeak={peakAge:F5} flight={flight} impact={impact} linger={linger} remaining={Clouds(world)}");
                }
                finally{world.Camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
            }
            File.WriteAllText(folder+"/trace.csv",trace.ToString());File.WriteAllText(folder+"/validation.txt",report.ToString());
            if(check&&File.ReadAllText(Folder+"/before/trace.csv")!=trace.ToString())throw new Exception("Dust changed battle, stone or actor trajectories");
            Debug.Log("[RockDustReview] PASS\n"+report);
        }
    }
}
