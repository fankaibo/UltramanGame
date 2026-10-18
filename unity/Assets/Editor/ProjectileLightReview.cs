using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor.SceneManagement;
using UltramanGame.Core;
using UltramanGame.Runtime;
namespace UltramanGame.Editor
{
    public static class ProjectileLightReview
    {
        static string Folder=>Path.GetFullPath("../artifacts/a175-projectile-light");
        static Battle Start()
        {
            var b=new Battle(50);b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
            while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});b.GiveInstructionTime(30);return b;
        }
        public static void After(){RangedSmokeReview.After();Lifecycle();Surface();}
        static void Lifecycle()
        {
            var report=new StringBuilder();
            foreach(string id in new[]{"Tiga","Zero","Mebius","Geed","Grigio"})foreach(int hz in new[]{15,30,60})foreach(float speed in new[]{.65f,1.7f})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var root=new GameObject("Flight light test").transform;var camera=new GameObject("Camera").AddComponent<Camera>();
                camera.transform.position=new Vector3(0,2,-6);var fx=new HeroProjectile(root);fx.SetHero(id);var b=Start();
                var lamp=root.Find("Ranged travelling spill").GetComponent<Light>();int objects=root.GetComponentsInChildren<Transform>(true).Length;
                bool lit=false;int hits=0;float dt=1f/hz;
                void Draw(float step=0)=>fx.Tick(b,camera,new Vector3(-2,2,0),new Vector3(2,2,0),false,Vector3.up*3,step);
                for(int f=0;f<hz*2;f++)
                {
                    b.Tick(dt,new PlayerInput{Tracking=true,RightPunch=f==0,RangedAttack=f==0,AttackSpeed=speed});
                    if(b.LastDamageRanged){hits++;fx.Impact(new Vector3(2,2,0),Vector3.right);}Draw(dt);
                    if(lamp.enabled)
                    {
                        lit=true;if(id!="Tiga"||!fx.Visible||Vector3.Distance(lamp.transform.position,fx.Tip)>.00001f||hits!=0)
                            throw new Exception("Travelling light detached or continued after contact");
                    }
                    float power=lamp.intensity;var position=lamp.transform.position;Draw();
                    if(power!=lamp.intensity||position!=lamp.transform.position)throw new Exception("Zero time changed light");
                }
                if(lit!=(id=="Tiga")||hits!=1||lamp.enabled||lamp.intensity!=0)throw new Exception("Flight light lifetime failed");
                b=Start();Draw();b.Tick(dt,new PlayerInput{Tracking=true,LeftPunch=true,RangedAttack=true,AttackSpeed=speed});Draw(dt);
                for(int n=0;n<hz&&!lamp.enabled&&id=="Tiga";n++){b.Tick(dt,new PlayerInput{Tracking=true});Draw(dt);}
                b.Pause();Draw();if(lamp.enabled||lamp.intensity!=0)throw new Exception("Pause left light");
                fx.SetHero(id=="Tiga"?"Zero":"Tiga");if(lamp.enabled||lamp.intensity!=0)throw new Exception("Hero switch left light");
                b=Start();Draw();if(lamp.enabled||objects!=root.GetComponentsInChildren<Transform>(true).Length)throw new Exception("Round/pool mismatch");
                report.AppendLine($"{id} hz={hz} speed={speed} lit={lit} hits={hits} path/freeze/contact/expiry/pause/switch/newRound/pool=pass");
            }
            File.WriteAllText(Folder+"/lifecycle.txt",report.ToString());Debug.Log("[ProjectileLightReview] lifecycle passed");
        }
        static void Surface()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(1988);
            var world=new GameWorld();var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);
            var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);world.SetHeroProfile("Tiga");
            var b=Start();while(b.TryCue(out _)){}var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
            var lamp=GameObject.Find("Ranged travelling spill").GetComponent<Light>();string folder=Folder+"/surface";Directory.CreateDirectory(folder);
            var report=new StringBuilder();int shot=0;float[] ages={.16f,.21f,.265f};
            try
            {
                for(int f=0;f<150;f++)
                {
                    float dt=1f/60;b.Tick(dt,new PlayerInput{Tracking=true,RightPunch=f==40,RangedAttack=f==40,AttackSpeed=.65f});
                    while(b.TryCue(out var cue))world.Cue(cue,b);hero.Update(b,world.Camera,dt,f*dt);enemy.Update(b,world.Camera,dt,f*dt);
                    if(b.LastDamageRanged)world.Hit(false,b);world.Tick(b,dt,f*dt);
                    if(shot>=ages.Length||!lamp.enabled||b.Shot.Age<ages[shot])continue;
                    string name=new[]{"depart","middle","approach"}[shot++];
                    CharacterReview.Save(world.Camera,rt,folder+"/"+name+"-on.png");lamp.enabled=false;
                    CharacterReview.Save(world.Camera,rt,folder+"/"+name+"-off.png");lamp.enabled=true;
                    report.AppendLine(FormattableString.Invariant($"{name} age={b.Shot.Age:R} light={lamp.intensity:R} position={lamp.transform.position} tip={world.Projectile.Tip}"));
                }
                if(shot!=3)throw new Exception("Missing actual character illumination views");
                File.WriteAllText(folder+"/validation.txt",report.ToString());Debug.Log("[ProjectileLightReview] surface captures passed");
            }
            finally{world.Camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
        }
    }
}
