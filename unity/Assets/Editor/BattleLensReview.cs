using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    // Identical real combat inputs before/after: alternating fast/slow ranged
    // casts, high guard, an enemy attack, and close-range punches.
    public static class BattleLensReview
    {
        public static void Before()=>Run("before",60,true);
        public static void BeamBefore()=>Beam("before",60,true);
        public static void BeamAfter()
        {foreach(int rate in new[]{15,30,60})Beam("after",rate,rate==60);After();}
        public static void After()
        {foreach(int rate in new[]{15,30,60})Run("after",rate,rate==60);}
        static void Beam(string version,int rate,bool movie)
        {
            string folder=Path.GetFullPath("../artifacts/a152-battle-lens/"+version+"/beam-"+rate);
            Directory.CreateDirectory(folder+"/frames");File.Delete(folder+"/passed.txt");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(152);
            var world=new GameWorld();var b=new Battle(100);
            var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});
            b.GiveInstructionTime(20);
            for(int p=0;p<15;p++){b.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});for(int n=0;n<30;n++)b.Tick(.02f,new PlayerInput{Tracking=true});}
            while(b.TryCue(out _)){}
            var camera=world.Camera;camera.aspect=16f/9;
            var rt=new RenderTexture(960,540,24){antiAliasing=2};rt.Create();camera.targetTexture=rt;
            float dt=1f/rate,health=b.EnemyHealth;int absent=0,cuts=0,beams=0;string last="wide";
            var csv=new StringBuilder("frame,shot,action,age,health,heroX,heroY,heroZ,enemyX,enemyY,enemyZ,visible\n");
            try
            {
                for(int f=0;f<rate*5;f++)
                {
                    b.Tick(world.BattleDelta(dt,b),new PlayerInput{Tracking=true,Beam=f==rate/2});while(b.TryCue(out var cue)){if(cue==GameCue.Beam)beams++;world.Cue(cue,b);}
                    hero.Update(b,camera,dt,f*dt);enemy.Update(b,camera,dt,f*dt);
                    if(b.EnemyHealth<health)world.Hit(true,b);health=b.EnemyHealth;world.Tick(b,dt,f*dt);
                    string shot=world.HeroShot?"hero":world.BeamReactionCloseup?"enemy":"wide";
                    var h=camera.WorldToViewportPoint(hero.Root.position+Vector3.up*2.1f);var e=camera.WorldToViewportPoint(enemy.BeamSurfaceContact);
                    bool Inside(Vector3 point)=>point.z>0&&point.x>.02f&&point.x<.98f&&point.y>.02f&&point.y<.98f;
                    bool visible=shot=="hero"?Inside(h):shot=="enemy"?Inside(e):Inside(h)||Inside(e);
                    if(!visible)absent++;
                    if(shot!=last){cuts++;if(movie)CharacterReview.Save(camera,rt,folder+"/cut-"+cuts+"-"+shot+".png");}last=shot;
                    csv.AppendLine(FormattableString.Invariant($"{f},{shot},{b.Action},{b.ActionAge:F4},{health},{h.x:F4},{h.y:F4},{h.z:F4},{e.x:F4},{e.y:F4},{e.z:F4},{visible}"));
                    if(movie&&f%2==0)CharacterReview.Save(camera,rt,folder+"/frames/"+(f/2).ToString("D4")+".jpg");
                }
                File.WriteAllText(folder+"/trace.csv",csv.ToString());
                string report=$"{rate}Hz beams={beams} cuts={cuts} absentFrames={absent} health={health} phase={b.Phase}";
                Debug.Log("[BattleLensBeam] "+version+" "+report);
                if(beams!=1||cuts!=4||b.Phase!=GamePhase.Battle)throw new Exception("Missing cut coverage: "+report);
                if(version=="after"&&absent>0)throw new Exception("Cut lost subject: "+report);
                File.WriteAllText(folder+"/passed.txt",report);
            }
            finally{camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
        }
        static void Run(string version,int rate,bool movie)
        {
            string folder=Path.GetFullPath("../artifacts/a152-battle-lens/"+version+"/"+rate);
            Directory.CreateDirectory(folder+"/frames");File.Delete(folder+"/passed.txt");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            UnityEngine.Random.InitState(152);
            var world=new GameWorld();var b=new Battle(100);
            var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);
            var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);
            world.BindActors(hero,enemy);
            b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
            while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});
            while(b.TryCue(out _)){}
            float dt=1f/rate,health=b.EnemyHealth,time=0,next=.4f;
            int shots=0,blocks=0;
            var camera=world.Camera;camera.aspect=16f/9;
            var rt=new RenderTexture(960,540,24){antiAliasing=2};rt.Create();camera.targetTexture=rt;
            for(int f=0;f<rate;f++)
            {hero.Update(b,camera,dt,time);enemy.Update(b,camera,dt,time);world.Tick(b,dt,time);time+=dt;}
            var trace=new StringBuilder("frame,action,enemy,punches,health,energy,blocks,angleSpeed,lensSpeed,x,y,z,fov\n");
            float maxAngle=0,maxLens=0,maxPosition=0;
            var rotation=camera.transform.rotation;var position=camera.transform.position;float lens=camera.fieldOfView;
            try
            {
                for(int f=0;f<rate*16;f++)
                {
                    float age=f*dt;var input=new PlayerInput{Tracking=true};
                    if(age<4||(age>9&&b.Blocks>0))
                    {
                        if(age>=next)
                        {input.LeftPunch=shots%2==0;input.RightPunch=!input.LeftPunch;input.RangedAttack=age<4;input.AttackSpeed=shots%2==0?.65f:1.7f;shots++;next=age+.65f;}
                        else if(age>1&&age<4&&!b.IsPunch)input.Shield=true;
                    }
                    else input.Shield=true;
                    b.Tick(world.BattleDelta(dt,b),input);while(b.TryCue(out var cue))world.Cue(cue,b);
                    hero.Update(b,camera,dt,time);enemy.Update(b,camera,dt,time);
                    if(b.EnemyHealth<health)world.Hit(false,b);health=b.EnemyHealth;
                    world.Tick(b,dt,time);time+=dt;
                    float angular=Quaternion.Angle(rotation,camera.transform.rotation)/dt;
                    float lensSpeed=Mathf.Abs(lens-camera.fieldOfView)/dt;
                    maxAngle=Mathf.Max(maxAngle,angular);maxLens=Mathf.Max(maxLens,lensSpeed);
                    maxPosition=Mathf.Max(maxPosition,Vector3.Distance(position,camera.transform.position)/dt);
                    rotation=camera.transform.rotation;position=camera.transform.position;lens=camera.fieldOfView;
                    trace.AppendLine(FormattableString.Invariant($"{f},{b.Action},{b.Enemy},{b.Punches},{health},{b.Energy},{b.Blocks},{angular:F4},{lensSpeed:F4},{position.x:F4},{position.y:F4},{position.z:F4},{lens:F4}"));
                    if(movie&&f%2==0)CharacterReview.Save(camera,rt,folder+"/frames/"+(f/2).ToString("D4")+".jpg");
                    // Repainting the same frame must not advance a camera blend.
                    world.Tick(b,0,time-dt);
                    if(Vector3.Distance(position,camera.transform.position)>.0001f||Quaternion.Angle(rotation,camera.transform.rotation)>.05f||Mathf.Abs(lens-camera.fieldOfView)>.0001f)
                        throw new Exception("Zero-time repaint moved camera");
                    blocks=b.Blocks;
                }
                                File.WriteAllText(folder+"/trace.csv",trace.ToString());
                string report=$"{rate}Hz punches={b.Punches} blocks={blocks} angleSpeed={maxAngle:F3} lensSpeed={maxLens:F3} positionSpeed={maxPosition:F3}";
                Debug.Log("[BattleLensReview] "+version+" "+report);
                if(b.Punches<7||blocks<1)throw new Exception("Missing attack/defence coverage: "+report);
                if(version=="after"&&(maxAngle>55.1f||maxLens>24.1f||maxPosition>13.6f))throw new Exception("Discontinuous combat lens: "+report);
                File.WriteAllText(folder+"/passed.txt",report);
            }
            finally{camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
        }
    }
}
