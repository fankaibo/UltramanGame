using System;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class MonsterFaceReview
    {
        public static void Before()=>Render("before",60,true);
        public static void After(){Render("after",60,true);Render("after",15,false);Render("after",30,false);}
        public static void Flow()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/monster-recovery"));Directory.CreateDirectory(folder);
            File.Delete(folder+"/flow-validation.txt");var report=new System.Text.StringBuilder();
            foreach(int rate in new[]{15,30,60})foreach(string mode in new[]{"punch","pause","new-round","instruction"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var world=new GameWorld();var b=Ready(1);var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                hero.Update(b,world.Camera,0,0);enemy.Update(b,world.Camera,0,0);world.Tick(b,1,0);
                bool interrupted=false,cleared=false;float dt=1f/rate,health=b.EnemyHealth,drift=0,step=0;
                Vector3 previous=enemy.HandPosition;
                for(int f=0;f<rate*5;f++)
                {
                    var input=new PlayerInput{Tracking=mode!="pause"||!interrupted,Shield=!interrupted};
                    if(!interrupted&&b.Enemy==EnemyPhase.Recover&&b.EnemyAge>.6f)
                    {
                        if(enemy.RecoveryWeight<.8f)throw new Exception("No recovery exercised");interrupted=true;input.Shield=false;
                        if(mode=="punch")input.LeftPunch=true;
                        else if(mode=="pause"){b.Pause();input.Tracking=false;}
                        else if(mode=="new-round")b=new Battle();
                        else b.GiveInstructionTime(2);
                    }
                    b.Tick(world.BattleDelta(dt,b),input);while(b.TryCue(out var cue))world.Cue(cue,b);
                    hero.Update(b,world.Camera,dt,f*dt);enemy.Update(b,world.Camera,dt,f*dt);
                    if(b.EnemyHealth<health){world.Hit(false,b);if(enemy.RecoveryWeight!=0)throw new Exception("Recovery competed with hit");cleared=true;}health=b.EnemyHealth;world.Tick(b,dt,f*dt);
                    if(interrupted&&mode!="punch"){if(enemy.RecoveryWeight!=0)throw new Exception("Recovery survived interruption");cleared=true;}
                    var p=enemy.HandPosition;step=Mathf.Max(step,Vector3.Distance(previous,p));previous=p;
                    enemy.Update(b,world.Camera,0,f*dt);drift=Mathf.Max(drift,Vector3.Distance(p,enemy.HandPosition));
                }
                string line=$"{rate}Hz {mode} interrupted={interrupted} cleared={cleared} punches={b.Punches} health={b.EnemyHealth} step={step:F5} repeat={drift:F6}";
                Debug.Log("[MonsterRecoveryFlow] "+line);
                if(!interrupted||!cleared||drift>.0001f||b.Punches!=(mode=="punch"?1:0)||b.EnemyHealth!=(mode=="punch"?49:50))throw new Exception(line);
                report.AppendLine(line+" passed");
            }
            File.WriteAllText(folder+"/flow-validation.txt",report.ToString());
        }
        static Transform Bone(Transform root,string name)
        {foreach(var b in root.GetComponentsInChildren<Transform>())if(b.name==name)return b;throw new Exception(name);}
        static Battle Ready(int attack)
        {
            var b=new Battle();b.Tick(.01f,new PlayerInput{Tracking=true,Transform=true});
            for(int i=0;i<10000;i++)
            {if(b.Enemy==EnemyPhase.Windup&&b.EnemyAttackCount==attack-1&&b.WarningDuration-b.EnemyAge<=.15f)break;b.Tick(.01f,new PlayerInput{Tracking=true,Shield=true});}
            while(b.TryCue(out _)){}return b;
        }
        static void Render(string version,int rate,bool movie)
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/monster-recovery/"+version));Directory.CreateDirectory(folder+"/frames");
            int image=0;var report=new System.Text.StringBuilder();
            for(int attack=1;attack<=4;attack++)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(930);
                var world=new GameWorld();var b=Ready(attack);var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);
                var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                hero.Update(b,world.Camera,0,0);enemy.Update(b,world.Camera,0,0);world.Tick(b,1,0);
                var head=Bone(enemy.Root,"bip_head");var jaw=Bone(enemy.Root,"jaw");
                var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
                var sequence=new System.Text.StringBuilder("frame,enemy,age,health,blocks,hurt\n");
                var motion=new System.Text.StringBuilder("frame,leftX,leftY,leftZ,rightX,rightY,rightZ,footLX,footLY,footLZ,footRX,footRY,footRZ,jawX,jawY,jawZ,jawW,headX,headY,headZ,headW\n");
                float drift=0,angleError=0,dt=1f/rate,maxStep=0;int recover=0;
                Vector3 oldL=enemy.StrikeOrigin(HeroAction.LeftPunch),oldR=enemy.HandPosition;
                try
                {
                    for(int f=0;f<rate*4;f++)
                    {
                        float time=f*dt;b.Tick(world.BattleDelta(dt,b),new PlayerInput{Tracking=true,Shield=true});while(b.TryCue(out var cue))world.Cue(cue,b);
                        hero.Update(b,world.Camera,dt,time);enemy.Update(b,world.Camera,dt,time);world.Tick(b,dt,time);
                        var l=enemy.StrikeOrigin(HeroAction.LeftPunch);var r=enemy.HandPosition;var fl=enemy.FootPosition(true);var fr=enemy.FootPosition(false);
                        var h=head.rotation;var j=jaw.localRotation;maxStep=Mathf.Max(maxStep,Vector3.Distance(l,oldL),Vector3.Distance(r,oldR));oldL=l;oldR=r;
                        enemy.Update(b,world.Camera,0,time);
                        drift=Mathf.Max(drift,Vector3.Distance(l,enemy.StrikeOrigin(HeroAction.LeftPunch)),Vector3.Distance(r,enemy.HandPosition));
                        angleError=Mathf.Max(angleError,Quaternion.Angle(h,head.rotation),Quaternion.Angle(j,jaw.localRotation));
                        sequence.AppendLine(FormattableString.Invariant($"{f},{b.Enemy},{b.EnemyAge:F5},{b.EnemyHealth},{b.Blocks},{b.HitsTaken}"));
                        motion.AppendLine(FormattableString.Invariant($"{f},{l.x:F5},{l.y:F5},{l.z:F5},{r.x:F5},{r.y:F5},{r.z:F5},{fl.x:F5},{fl.y:F5},{fl.z:F5},{fr.x:F5},{fr.y:F5},{fr.z:F5},{j.x:F6},{j.y:F6},{j.z:F6},{j.w:F6},{h.x:F6},{h.y:F6},{h.z:F6},{h.w:F6}"));
                        if(movie&&f%2==0)CharacterReview.Save(world.Camera,rt,$"{folder}/frames/{image++:D4}.png");
                        if(b.Enemy==EnemyPhase.Recover)
                        {
                            recover++;
                            foreach(float age in new[]{.65f,1.3f})if(b.EnemyAge>=age&&b.EnemyAge<age+dt*1.01f)
                                CharacterReview.Save(world.Camera,rt,$"{folder}/attack-{attack}-{rate}-{age:F2}.png");
                        }
                    }
                    string line=$"attack={attack} rate={rate} blocks={b.Blocks} health={b.EnemyHealth} recover={recover} repeat={drift:F6} repeatAngle={angleError:F6} maxStep={maxStep:F4}";
                    Debug.Log("[MonsterRecoveryReview] "+line);report.AppendLine(line);
                    File.WriteAllText($"{folder}/attack-{attack}-{rate}-sequence.csv",sequence.ToString());File.WriteAllText($"{folder}/attack-{attack}-{rate}-motion.csv",motion.ToString());
                    if(b.Blocks!=attack||b.EnemyHealth!=50||b.HitsTaken!=0||recover<rate||drift>.0001f||angleError>.08f)throw new Exception(line);
                }
                finally{world.Camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
            }
            File.WriteAllText($"{folder}/{rate}-validation.txt",report.ToString());
            if(movie)
            {
                var hashes=new System.Text.StringBuilder();using(var sha=System.Security.Cryptography.SHA256.Create())
                foreach(string file in new[]{"Scripts/Runtime/RiggedActor.cs","Scripts/Runtime/GameWorld.cs","Scripts/Core/MonsterRecoveryMotion.cs"})
                {string path=Path.Combine(Application.dataPath,file);if(File.Exists(path))hashes.AppendLine(file+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant());}
                File.WriteAllText(folder+"/sources.txt",hashes.ToString());
            }
        }
        public static void Probe()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var world=new GameWorld();var b=new Battle();var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);
            enemy.Update(b,world.Camera,0,0,0);
            Transform jaw=null;foreach(var bone in enemy.Root.GetComponentsInChildren<Transform>())if(bone.name=="jaw")jaw=bone;
            if(!jaw)throw new Exception("Missing jaw");
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/monster-face/probe"));Directory.CreateDirectory(folder);
            var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
            world.Tick(b,1,0);world.Camera.transform.position=world.EnemyHome-world.BattleAxis*4+Vector3.up*3.6f+enemy.Root.right*1.4f;
            world.Camera.transform.LookAt(world.EnemyHome+Vector3.up*3.45f);world.Camera.fieldOfView=32;
            Quaternion original=jaw.rotation;
            try
            {
                foreach(int angle in new[]{-30,-20,-10,0,10,20})
                {jaw.rotation=Quaternion.AngleAxis(angle,enemy.Root.right)*original;CharacterReview.Save(world.Camera,rt,folder+"/jaw-"+angle+".png");}
                File.WriteAllText(folder+"/jaw.txt",$"name={jaw.name} axis={enemy.Root.right} parent={jaw.parent.name}");
            }
            finally{world.Camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
        }
    }
}
