using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class RangedPresentationReview
    {
        public static void Before(){Run("before","Tiga",60,true);Run("before","Zero",60,true);}
        public static void After()
        {foreach(string id in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})foreach(int hz in new[]{15,30,60})Run("after",id,hz,hz==60&&(id=="Tiga"||id=="Zero"));}
        static void Run(string version,string id,int hz,bool movie)
        {
            string folder=Path.GetFullPath("../artifacts/ranged-presentation-20261008/"+version+"/"+id+"-"+hz);Directory.CreateDirectory(folder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(1053);
            var world=new GameWorld();var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);
            if(!hero.IsRigged||!enemy.IsRigged)throw new Exception("Actual model missing");world.BindActors(hero,enemy);world.SetHeroProfile(id);
            var b=new Battle();b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});b.GiveInstructionTime(20);while(b.TryCue(out _)){}
            var rt=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
            for(int f=0;f<40;f++){hero.Update(b,world.Camera,1/60f,f/60f);enemy.Update(b,world.Camera,1/60f,f/60f);world.Tick(b,1/60f,f/60f);}
            if(movie)Directory.CreateDirectory(folder+"/frames");
            var csv=new StringBuilder("frame,action,age,shotAge,punches,health,energy,sequence,visible\n");float dt=1f/hz,health=50;int hits=0;bool flight=false,back=false,launchLight=false,impactLight=false;
            int objects=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
            int[] starts={Mathf.RoundToInt(.4f*hz),2*hz,Mathf.RoundToInt(3.4f*hz)};float[] speeds={.65f,1,1.7f};
            try
            {
                for(int f=0;f<hz*5;f++)
                {
                    var input=new PlayerInput{Tracking=true};for(int i=0;i<3;i++)if(f==starts[i]){input.RangedAttack=true;input.LeftPunch=i==1;input.RightPunch=i!=1;input.AttackSpeed=speeds[i];}
                    b.Tick(dt,input);while(b.TryCue(out var cue))world.Cue(cue,b);
                    hero.Update(b,world.Camera,dt,f*dt);enemy.Update(b,world.Camera,dt,f*dt);
                    if(b.EnemyHealth<health){hits++;world.Hit(false,b);}health=b.EnemyHealth;
                    world.Tick(b,dt,f*dt);
                    if(version=="after")
                    {
                        launchLight|=world.Projectile.LaunchVisible;impactLight|=world.Projectile.ImpactVisible;
                        if(world.Projectile.Impacts!=hits)throw new Exception("Impact count differs from actual damage");
                        if(id=="Zero"&&world.Projectile.Visible)
                            for(int i=0;i<2;i++)if(Vector3.Distance(world.Projectile.BladePosition(i),world.Projectile.BladeTrailTip(i))>.0001f)throw new Exception("Detached blade wake");
                        if(b.Shot.Sequence==1&&world.Projectile.LaunchVisible&&b.ActionAge<.1f&&hz==60)CharacterReview.Save(world.Camera,rt,folder+"/launch.png");
                        if(b.Punches==1&&world.Projectile.ImpactVisible&&b.Shot.Age>.34f&&b.Shot.Age<.34f+dt*.66f)CharacterReview.Save(world.Camera,rt,folder+"/impact-volume.png");
                    }
                    if(b.Shot.Sequence==1&&world.Projectile.Visible)
                    {
                        if(!flight&&b.Shot.Age>.22f&&b.Shot.Age<.30f){flight=true;CharacterReview.Save(world.Camera,rt,folder+"/flight.png");}
                        if(!back&&b.Shot.Age>.35f&&id=="Zero"){back=true;CharacterReview.Save(world.Camera,rt,folder+"/return.png");}
                    }
                    if(b.Punches==1&&b.Shot.Age>.30f&&b.Shot.Age<.30f+dt*.66f)CharacterReview.Save(world.Camera,rt,folder+"/contact.png");
                    if(movie&&f%2==0)CharacterReview.Save(world.Camera,rt,folder+"/frames/"+(f/2).ToString("D4")+".png");
                    csv.AppendLine(FormattableString.Invariant($"{f},{b.Action},{b.ActionAge:F5},{b.Shot.Age:F5},{b.Punches},{b.EnemyHealth},{b.Energy},{b.Shot.Sequence},{world.Projectile.Visible}"));
                }
                if(b.Punches!=3||hits!=3||world.Projectile.Launches!=3||!flight||id=="Zero"&&!back)throw new Exception("Missing shot lifecycle "+id+"-"+hz);
                if(version=="after"&&(!launchLight||!impactLight||world.Projectile.LaunchVisible||world.Projectile.ImpactVisible))throw new Exception("Missing launch/impact or effect survived its lifetime");
                if(UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length!=objects)throw new Exception("Per-shot objects allocated");
                b.Pause();world.Tick(b,0,5);if(world.Projectile.Visible||world.Projectile.ImpactVisible||world.Projectile.LaunchVisible)throw new Exception("Pause left projectile visible");
                world.ResetPresentation();
                File.WriteAllText(folder+"/trace.csv",csv.ToString());
                string result=$"{id}-{hz} hits={hits} launches={world.Projectile.Launches} impacts={world.Projectile.Impacts} health={b.EnemyHealth} energy={b.Energy} flight={flight} return={back} launchVolume={launchLight} impactVolume={impactLight} pool=stable pause=pass";
                File.WriteAllText(folder+"/validation.txt",result);Debug.Log("[RangedPresentation] "+result);
            }
            finally{world.Camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
            var sources=new StringBuilder();foreach(string dir in new[]{"Scripts","Resources"})foreach(string file in Directory.GetFiles(Application.dataPath+"/"+dir,"*",SearchOption.AllDirectories))
            {if(!file.EndsWith(".cs")&&!file.EndsWith(".shader"))continue;using(var sha=System.Security.Cryptography.SHA256.Create())sources.AppendLine(file.Substring(Application.dataPath.Length+1)+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file))).Replace("-","").ToLowerInvariant());}
            File.WriteAllText(folder+"/sources.txt",sources.ToString());
        }
    }
}
