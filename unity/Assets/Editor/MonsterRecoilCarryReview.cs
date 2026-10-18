using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;
namespace UltramanGame.Editor
{
    public static class MonsterRecoilCarryReview
    {
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/recoil-carry-20261011"));
        public static void Before()=>Run(false);
        public static void After(){Run(true);Lifecycle();}
        static Transform Bone(AnimatedActor a,string name){foreach(var t in a.Root.GetComponentsInChildren<Transform>())if(t.name==name)return t;throw new Exception(name);}
        public static void MeleeBefore()=>Melee(false);
        public static void MeleeAfter(){Melee(true);StaggerReview.CheckRecovery();}
        static void Melee(bool check)
        {
            var trace=new StringBuilder();var report=new StringBuilder();
            foreach(int hz in new[]{15,30,60})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var w=new GameWorld();var b=new Battle(200);b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});b.GiveInstructionTime(20);
                var e=new AnimatedActor("Golza",w.EnemyHome,w.HeroHome,true);e.Update(b,w.Camera,0,0);var head=Bone(e,"bip_head");float headStep=0,rootStep=0,angle=0,dt=1f/hz;
                for(int f=0;f<hz*5;f++)
                {
                    var old=e.Root.position;var h=head.position;var q=head.rotation;
                    int beat=Mathf.RoundToInt(.30f*hz);b.Tick(dt,new PlayerInput{Tracking=true,LeftPunch=f%(beat*2)==0,RightPunch=f%(beat*2)==beat});e.Update(b,w.Camera,dt,f*dt);
                    rootStep=Mathf.Max(rootStep,Vector3.Distance(old,e.Root.position));headStep=Mathf.Max(headStep,Vector3.Distance(h,head.position));angle=Mathf.Max(angle,Quaternion.Angle(q,head.rotation));
                    var p=e.Root.position;var rot=e.Root.rotation;var l=e.FootPosition(true);var r=e.FootPosition(false);
                    trace.AppendLine(FormattableString.Invariant($"{hz},{f},{b.Punches},{b.EnemyHealth},{b.Energy},{b.Action},{b.ActionAge:F5},{p.x:F5},{p.y:F5},{p.z:F5},{rot.x:F5},{rot.y:F5},{rot.z:F5},{rot.w:F5},{l.x:F5},{l.y:F5},{l.z:F5},{r.x:F5},{r.y:F5},{r.z:F5}"));
                }
                if(b.Punches<10)throw new Exception("Melee sequence incomplete: "+b.Punches);
                report.AppendLine($"{hz}Hz hits={b.Punches} maximumRootStep={rootStep:F5} maximumHeadStep={headStep:F5} maximumHeadAngle={angle:F5}");
            }
            string name=check?"melee-after":"melee-before";File.WriteAllText(Folder+"/"+name+".csv",trace.ToString());File.WriteAllText(Folder+"/"+name+".txt",report.ToString());
            if(check&&trace.ToString()!=File.ReadAllText(Folder+"/melee-before.csv"))throw new Exception("Upper-body change modified melee gameplay, root or feet");
            Debug.Log("[RecoilCarryMelee] PASS\n"+report);
        }
        public static void TerminalPause()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var w=new GameWorld();var e=new AnimatedActor("Golza",w.EnemyHome,w.HeroHome,true);var b=new Battle(10);
            b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});
            for(int n=0;n<8;n++){b.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});for(int f=0;f<30;f++)b.Tick(.02f,new PlayerInput{Tracking=true});b.GiveInstructionTime(20);}
            const float dt=1/60f;float time=0;var head=Bone(e,"bip_head");
            for(int f=0;f<120;f++)
            {
                b.Tick(dt,new PlayerInput{Tracking=true,RangedAttack=true,AttackSpeed=1.7f,LeftPunch=f==0,RightPunch=f==19});e.Update(b,w.Camera,dt,time);time+=dt;
                if(b.Finishing)break;
            }
            if(!b.Finishing)throw new Exception("No final ordinary hit");
            var rig=typeof(AnimatedActor).GetField("rigged",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(e);
            var carry=typeof(RiggedActor).GetField("recoilTorsoCarry",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            if(!(bool)carry.GetValue(rig))throw new Exception("Terminal hit did not carry recoil");
            var rotation=head.rotation;var position=head.position;float age=b.ActionAge;
            b.Pause();for(int f=0;f<60;f++){b.Tick(dt,default);e.Update(b,w.Camera,dt,time);time+=dt;}
            if(!(bool)carry.GetValue(rig)||Quaternion.Angle(rotation,head.rotation)>.05f||Vector3.Distance(position,head.position)>.0001f||age!=b.ActionAge)throw new Exception("Terminal pause discarded visible recoil");
            while(b.Phase==GamePhase.Paused)b.Tick(dt,new PlayerInput{Tracking=true});e.Update(b,w.Camera,0,time);
            float angle=Quaternion.Angle(rotation,head.rotation);if(angle>.1f)throw new Exception("Terminal resume snapped head: "+angle);
            int victories=0;for(int f=0;f<120;f++){b.Tick(dt,new PlayerInput{Tracking=true});while(b.TryCue(out var c))if(c==GameCue.Victory)victories++;e.Update(b,w.Camera,dt,time);time+=dt;}
            if(b.Phase!=GamePhase.Victory||victories!=1)throw new Exception("Final hit did not complete once");
            File.WriteAllText(Folder+"/terminal-pause.txt",$"PASS frozenClock=true resumeHeadAngle={angle:F5} victories={victories}\n");Debug.Log("[RecoilCarryTerminalPause] PASS");
        }
        static void Lifecycle()
        {
            var report=new StringBuilder();
            foreach(int hz in new[]{15,30,60})foreach(string mode in new[]{"pause","new-round","preview","beam"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var w=new GameWorld();var h=new AnimatedActor("Tiga",w.HeroHome,w.EnemyHome);var e=new AnimatedActor("Golza",w.EnemyHome,w.HeroHome,true);w.BindActors(h,e);
                var b=new Battle(200);b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});
                if(mode=="beam")for(int n=0;n<15;n++){b.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});for(int f=0;f<30;f++)b.Tick(.02f,new PlayerInput{Tracking=true});b.GiveInstructionTime(20);}
                b.GiveInstructionTime(20);while(b.TryCue(out _)){}int start=b.Punches;float dt=1f/hz,time=0;
                var rig=typeof(AnimatedActor).GetField("rigged",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(e);
                var carry=typeof(RiggedActor).GetField("recoilTorsoCarry",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                void Step(PlayerInput input,int preview=-1){b.Tick(dt,input);while(b.TryCue(out var cue))w.Cue(cue,b);h.Update(b,w.Camera,dt,time);e.Update(b,w.Camera,dt,time,preview);w.Tick(b,dt,time);time+=dt;}
                for(int f=0;f<hz*2;f++)
                {
                    Step(new PlayerInput{Tracking=true,LeftPunch=f==0,RightPunch=f==Mathf.RoundToInt(.32f*hz),RangedAttack=true,AttackSpeed=1.7f});
                    if(b.Punches==start+2&&(bool)carry.GetValue(rig))break;
                }
                if(!(bool)carry.GetValue(rig))throw new Exception("Never entered continuous recoil "+mode);
                if(mode=="pause")b.Pause();if(mode=="new-round")b=new Battle();bool beam=false;
                for(int f=0;f<hz*3;f++)
                {
                    Step(new PlayerInput{Tracking=true,Beam=mode=="beam"&&f<hz},mode=="preview"?0:-1);beam|=b.Action==HeroAction.Beam;
                    if(mode!="beam"&&(bool)carry.GetValue(rig))throw new Exception("Carry survived "+mode);
                }
                if((bool)carry.GetValue(rig)||mode=="beam"&&!beam)throw new Exception("Exclusive recovery failed "+mode);
                var waiting=new Battle();var fresh=new AnimatedActor("Golza",w.EnemyHome,w.HeroHome,true);
                for(int f=0;f<hz;f++){e.Update(waiting,w.Camera,dt,time);fresh.Update(waiting,w.Camera,dt,time);time+=dt;}
                float error=Quaternion.Angle(Bone(e,"bip_head").rotation,Bone(fresh,"bip_head").rotation);
                if(error>.06f)throw new Exception("Carry contaminated next-round head "+error);
                report.AppendLine($"{hz}Hz {mode} cleared=true beam={beam} newRoundHeadError={error:F5}");
            }
            File.WriteAllText(Folder+"/lifecycle.txt",report.ToString());Debug.Log("[RecoilCarryLifecycle] PASS\n"+report);
        }
        static void Run(bool check)
        {
            string folder=Folder+(check?"/after":"/before");Directory.CreateDirectory(folder);var report=new StringBuilder();var gameplay=new StringBuilder();
            foreach(int hz in new[]{15,30,60})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(115);
                var world=new GameWorld();var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                var b=new Battle(200);b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});b.GiveInstructionTime(20);while(b.TryCue(out _)){}
                var head=Bone(enemy,"bip_head");var chest=Bone(enemy,"bip_spine_2");float dt=1f/hz,time=0,maxHitAngle=0,maxHeadStep=0,maxAllAngle=0;int hits=0;float maxWrist=0,minFloor=100,repeat=0;var mesh=new Mesh();
                var target=new RenderTexture(1280,720,24){antiAliasing=4};target.Create();world.Camera.targetTexture=target;world.Camera.aspect=16f/9;
                string path=folder+"/"+hz;Directory.CreateDirectory(path);if(hz==60)Directory.CreateDirectory(path+"/frames");
                var trace=new StringBuilder("frame,hits,hit,headStep,headAngle,chestAngle\n");
                try
                {
                    for(int f=-hz;f<hz*5;f++)
                    {
                        var input=new PlayerInput{Tracking=true};
                        for(int n=0;n<8;n++)if(f==Mathf.RoundToInt((.35f+n*.32f)*hz)){input.LeftPunch=n%2==0;input.RightPunch=n%2==1;input.RangedAttack=true;input.AttackSpeed=1.7f;}
                        var h=head.position;var hr=head.rotation;var cr=chest.rotation;float health=b.EnemyHealth;
                        b.Tick(dt,input);while(b.TryCue(out var cue))world.Cue(cue,b);hero.Update(b,world.Camera,dt,time);enemy.Update(b,world.Camera,dt,time);if(b.EnemyHealth<health)world.Hit(false,b);world.Tick(b,dt,time);time+=dt;
                        if(check&&f>=0)
                        {
                            foreach(string side in new[]{"L","R"})
                            {
                                var wrist=Bone(enemy,"bip_hand_"+side);var lower=Bone(enemy,"bip_lowerArm_"+side);Vector3 palm=Vector3.zero;
                                foreach(string finger in new[]{"index","middle","ring","pinky"})palm+=Bone(enemy,"bip_"+finger+"_0_"+side).position;
                                maxWrist=Mathf.Max(maxWrist,Vector3.Angle(palm/4-wrist.position,wrist.position-lower.position));
                            }
                            if(f%Mathf.Max(1,hz/10)==0)foreach(var skin in enemy.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                            {skin.BakeMesh(mesh,true);foreach(var v in mesh.vertices)minFloor=Mathf.Min(minFloor,skin.transform.TransformPoint(v).y);}
                            var position=head.position;var rotation=head.rotation;enemy.Update(b,world.Camera,0,time-dt);
                            repeat=Mathf.Max(repeat,Vector3.Distance(position,head.position));if(Quaternion.Angle(rotation,head.rotation)>.05f)throw new Exception("Repeated head rotation drift");
                        }
                        float headStep=Vector3.Distance(h,head.position),headAngle=Quaternion.Angle(hr,head.rotation),chestAngle=Quaternion.Angle(cr,chest.rotation);bool hit=b.EnemyHealth<health;
                        if(f>=0)
                        {
                            if(hit){hits++;maxHitAngle=Mathf.Max(maxHitAngle,headAngle,chestAngle);}
                            maxHeadStep=Mathf.Max(maxHeadStep,headStep);maxAllAngle=Mathf.Max(maxAllAngle,headAngle,chestAngle);
                            gameplay.AppendLine(FormattableString.Invariant($"{hz},{f},{b.Action},{b.ActionAge:F5},{b.EnemyHealth},{b.Energy},{b.Punches},{b.Enemy},{b.EnemyAge:F5}"));
                            trace.AppendLine(FormattableString.Invariant($"{f},{hits},{hit},{headStep:F5},{headAngle:F5},{chestAngle:F5}"));
                            if(hz==60&&f%2==0)RangedContactReview.Save(world.Camera,target,$"{path}/frames/{f/2:D4}.jpg");
                        }
                    }
                    if(hits!=8||b.EnemyHealth!=192)throw new Exception("Incomplete volley");
                    if(check&&(maxHitAngle>1||maxAllAngle>(hz==15?42.56f:hz==30?33.16f:26.21f)||maxWrist>36||minFloor<-.035f||repeat>.00001f))throw new Exception($"Recoil bounds failed: {hz} angle={maxHitAngle} peak={maxAllAngle} wrist={maxWrist} floor={minFloor} repeat={repeat}");
                    report.AppendLine($"{hz}Hz hits={hits} maxContactAngle={maxHitAngle:F5} maxHeadStep={maxHeadStep:F5} maxAllAngle={maxAllAngle:F5} wrist={maxWrist:F3} floor={minFloor:F5} repeat={repeat:F6}");File.WriteAllText(path+"/motion.csv",trace.ToString());
                }
                finally{world.Camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(mesh);}
            }
            File.WriteAllText(folder+"/gameplay.csv",gameplay.ToString());File.WriteAllText(folder+"/validation.txt",report.ToString());
            if(check&&File.ReadAllText(Folder+"/before/gameplay.csv")!=gameplay.ToString())throw new Exception("Reaction changed gameplay");
            Debug.Log("[RecoilCarryReview] PASS\n"+report);
        }
    }
}
