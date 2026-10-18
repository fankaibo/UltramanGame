using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class SlamDustReview
    {
        static string Folder { get {var a=Environment.GetCommandLineArgs();int at=Array.IndexOf(a,"--slam-dust-output");
            if(at<0||at+1>=a.Length)throw new ArgumentException("Explicit --slam-dust-output required");return Path.GetFullPath(a[at+1]);}}
        static string Hash(string path){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
        public static void Before(){if(Hash(Application.dataPath+"/Scripts/Runtime/GroundImpact.cs")!="2b106d41b69234b4676b1eadf2232b181483622ad91983cee4da9bb25bbb90f2")throw new Exception("Before requires A182 ground runtime");Render("before");}
        public static void After()=>Render("after");
        static void Render(string version)
        {
            string folder=Folder+"/"+version;Directory.CreateDirectory(folder+"/frames");var report=new StringBuilder();
            var hashes=new StringBuilder();foreach(string path in new[]{"Scripts/Runtime/GroundImpact.cs","Scripts/Runtime/SlamDustVolume.cs","Resources/SlamDustVolume.shader","Scripts/Core/Battle.cs"})
                if(File.Exists(Application.dataPath+"/"+path))hashes.AppendLine(path+" "+Hash(Application.dataPath+"/"+path));
            File.WriteAllText(folder+"/sources.txt",hashes.ToString());
            foreach(int hz in new[]{15,30,60})foreach(bool guard in new[]{true,false})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(183);
                var world=new GameWorld();var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);world.SetHeroProfile("Tiga");
                var b=new Battle(200);b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
                for(int n=0;n<15000;n++)
                {b.Tick(.02f,new PlayerInput{Tracking=true,Shield=true});if(b.Phase==GamePhase.Battle&&b.Enemy==EnemyPhase.Windup&&b.EnemyAttackCount==2&&b.WarningDuration-b.EnemyAge<1.20f)break;}
                if(b.EnemyAttackCount!=2||b.Enemy!=EnemyPhase.Windup)throw new Exception("Third attack not prepared");
                while(b.TryCue(out _)){}int blocks=b.Blocks,hits=b.HitsTaken,contacts=0;float health=b.EnemyHealth;
                hero.Update(b,world.Camera,0,0);enemy.Update(b,world.Camera,0,0);world.Tick(b,0,0);
                var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
                string key=$"{hz}-{(guard?"guard":"hurt")}";var csv=new StringBuilder("frame,enemy,enemyAge,action,actionAge,health,blocks,hits,groundContacts,groundCause\n");
                try
                {
                    for(int f=0;f<hz*6;f++)
                    {
                        float dt=1f/hz,t=f*dt;
                        b.Tick(world.BattleDelta(dt,b),new PlayerInput{Tracking=true,Shield=guard,GuardIntent=guard});while(b.TryCue(out var cue))world.Cue(cue,b);
                        hero.Update(b,world.Camera,dt,t);enemy.Update(b,world.Camera,dt,t);
                        if(b.EnemyHealth<health)world.Hit(false,b);health=b.EnemyHealth;world.Tick(b,dt,t);
                        if(world.GroundContactCause=="slam")contacts=Math.Max(contacts,world.GroundContactCount);
                        csv.AppendLine(FormattableString.Invariant($"{f},{b.Enemy},{b.EnemyAge:F5},{b.Action},{b.ActionAge:F5},{b.EnemyHealth},{b.Blocks},{b.HitsTaken},{world.GroundContactCount},{world.GroundContactCause}"));
                        if(hz==60&&f%2==0)CharacterReview.Save(world.Camera,rt,$"{folder}/frames/{(guard?0:180)+f/2:D4}.jpg");
                    }
                    string line=$"{key} blocks={b.Blocks-blocks} hurt={b.HitsTaken-hits} slamContact={contacts} health={b.EnemyHealth}";
                    if(b.Blocks-blocks!=(guard?1:0)||b.HitsTaken-hits!=(guard?0:1)||contacts<1)throw new Exception(line);
                    File.WriteAllText(folder+"/"+key+".csv",csv.ToString());
                    if(version=="after"&&File.ReadAllText(Folder+"/before/"+key+".csv")!=csv.ToString())throw new Exception("Ground effect changed gameplay: "+key);
                    // Actual world clear path used by pause and a fresh round.
                    b.Pause();world.Tick(b,.02f,7);if(world.ActiveGroundStones!=0||world.ActiveGroundDust!=0)throw new Exception("Pause retained ground effects");
                    world.ResetPresentation();world.Tick(new Battle(),.02f,8);if(world.ActiveGroundDust!=0)throw new Exception("New round retained dust");
                    report.AppendLine(line+" pause/newRound=passed");Debug.Log("[SlamDustReview] "+line);
                }
                finally{world.Camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
            }
            File.WriteAllText(folder+"/validation.txt",report.ToString());
        }
    }
}
