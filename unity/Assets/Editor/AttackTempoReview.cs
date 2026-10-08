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
    public static class AttackTempoReview
    {
        public static void Render()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/attack-tempo-20261008"));
            Directory.CreateDirectory(folder);File.Delete(folder+"/validation.txt");var report=new StringBuilder();
            var sources=new StringBuilder();
            foreach(string path in Directory.GetFiles(Application.dataPath+"/Scripts","*.cs",SearchOption.AllDirectories))
                using(var sha=SHA256.Create())sources.AppendLine(path.Substring(Application.dataPath.Length+1)+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant());
            File.WriteAllText(folder+"/render-source.txt",sources.ToString());
            foreach(string name in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})foreach(float speed in new[]{.65f,1.7f})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(1008);
                string mode=speed<1?"slow":"fast",dest=folder+"/"+name+"-"+mode;Directory.CreateDirectory(dest);
                var world=new GameWorld();var state=new Battle();
                var hero=new AnimatedActor(name,world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);
                if(!hero.IsRigged)throw new Exception("Missing actual model: "+name);
                world.BindActors(hero,enemy);world.SetHeroProfile(name);
                state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
                for(int i=0;i<240;i++)state.Tick(.02f,new PlayerInput{Tracking=true});state.GiveInstructionTime(20);while(state.TryCue(out _)){}
                var rt=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
                for(int i=0;i<30;i++){hero.Update(state,world.Camera,1/60f,i/60f);enemy.Update(state,world.Camera,1/60f,i/60f);world.Tick(state,1/60f,i/60f);}
                float health=state.EnemyHealth,contact=-1,recovery=-1,gap=0,minY=100;int flightFrames=0;bool saved=false;
                var csv=new StringBuilder("frame,action,age,shotAge,punches,flight,handX,handY,handZ,tipX,tipY,tipZ\n");var mesh=new Mesh();
                try
                {
                    for(int f=0;f<90;f++)
                    {
                        const float dt=1/60f;float time=f*dt;
                        state.Tick(dt,new PlayerInput{Tracking=true,RightPunch=f==15,RangedAttack=true,AttackSpeed=speed});
                        while(state.TryCue(out var cue))world.Cue(cue,state);
                        hero.Update(state,world.Camera,dt,time);enemy.Update(state,world.Camera,dt,time);
                        if(state.EnemyHealth<health){world.Hit(false,state);contact=(f-14)*dt;}health=state.EnemyHealth;
                        world.Tick(state,dt,time);
                        if(f>15&&state.Action==HeroAction.None&&recovery<0)recovery=(f-14)*dt;
                        if(world.Projectile.Visible)
                        {
                            flightFrames++;gap=Mathf.Max(gap,Vector3.Distance(world.Projectile.Origin,world.BeamTarget));
                            if(!saved&&AttackTempo.Travel(state.Shot.Age)>.40f&&AttackTempo.Travel(state.Shot.Age)<.85f)
                            {CharacterReview.Save(world.Camera,rt,dest+"/flight.png");saved=true;}
                        }
                        if(f%3==0&&state.IsRangedPunch)
                            foreach(var skin in hero.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                            {skin.BakeMesh(mesh,true);foreach(var v in mesh.vertices)minY=Mathf.Min(minY,skin.transform.TransformPoint(v).y);}
                        if(name=="Tiga"&&f%2==0)CharacterReview.Save(world.Camera,rt,dest+"/"+(f/2).ToString("D4")+".png");
                        var h=hero.HandPosition;var tip=world.Projectile.Tip;
                        csv.AppendLine(FormattableString.Invariant($"{f},{state.Action},{state.ActionAge:F5},{state.Shot.Age:F5},{state.Punches},{world.Projectile.Visible},{h.x:F5},{h.y:F5},{h.z:F5},{tip.x:F5},{tip.y:F5},{tip.z:F5}"));
                    }
                    string line=$"{name} speed={speed:F2} hits={state.Punches} launches={world.Projectile.Launches} contact={contact:F4} recovery={recovery:F4} flightFrames={flightFrames} gap={gap:F3} ground={minY:F3}";
                    Debug.Log("[AttackTempoReview] "+line);report.AppendLine(line);File.WriteAllText(dest+"/sequence.csv",csv.ToString());
                    if(state.Punches!=1||world.Projectile.Launches!=1||!saved||flightFrames<3||gap<.6f||minY<-.05f||
                        Mathf.Abs(contact-AttackTempo.RangedHitSeconds/speed)>.018f||Mathf.Abs(recovery-AttackTempo.RangedSeconds/speed)>.018f)
                        throw new Exception("Remote skill mismatch: "+line);
                    state.Pause();world.Tick(state,0,2);if(world.Projectile.Visible)throw new Exception("Paused projectile remains visible");
                    world.ResetPresentation();if(world.Projectile.Visible)throw new Exception("Reset projectile remains visible");
                }
                finally{world.Camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(mesh);}
            }
            File.WriteAllText(folder+"/validation.txt",report.ToString());
        }
    }
}
