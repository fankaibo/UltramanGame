using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor.SceneManagement;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class MonsterRetreatReview
    {
        static string Folder=>Path.GetFullPath(Application.dataPath+"/../../artifacts/monster-retreat-20261012");
        public static void Release(){After();Flow();MeleeCounter();}
        public static void Before(){foreach(int attack in new[]{1,5})Run("before",60,attack,true);}
        public static void After(){foreach(int hz in new[]{15,30,60})foreach(int attack in new[]{1,5})Run("after",hz,attack,hz==60);}
        public static void Flow(){ClawPoseReview.Stability();Lifecycle();Counter();}
        public static void Lifecycle()
        {
            foreach(int hz in new[]{15,30,60})foreach(float cut in new[]{.12f,.31f,.93f,1.15f,1.35f})foreach(bool reset in new[]{false,true})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var world=new GameWorld();var b=new Battle();var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);
                var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                float dt=1f/hz,time=0;
                void Step(PlayerInput input)
                {
                    time+=dt;b.Tick(dt,input);while(b.TryCue(out var cue))world.Cue(cue,b);
                    hero.Update(b,world.Camera,dt,time);enemy.Update(b,world.Camera,dt,time);world.Tick(b,dt,time);
                    int steps=world.MonsterRushSteps;world.Tick(b,0,time);
                    if(steps!=world.MonsterRushSteps)throw new Exception("Repeated frame duplicated a footfall");
                }
                Step(new PlayerInput{Tracking=true,Transform=true});int waits=0;
                while(!((b.Enemy==EnemyPhase.Attack||b.Enemy==EnemyPhase.Recover)&&enemy.RushAge>=cut)&&waits++<hz*40)Step(new PlayerInput{Tracking=true,Shield=true});
                if(waits>=hz*40)throw new Exception("Interrupt fixture missed rush");
                int count=world.MonsterRushSteps;
                if(reset){b=new Battle();world.ResetPresentation();count=0;}else b.Pause();
                Step(new PlayerInput{Tracking=reset});
                // ResetPresentation and the paused world clear transient VFX
                // counters. Subsequent frames must keep that cleared state.
                count=0;
                for(int f=1;f<hz;f++)Step(new PlayerInput{Tracking=reset});
                if(world.MonsterRushSteps!=count)throw new Exception("Interrupted rush made stale footfalls");
                if(!reset)
                {
                    for(int f=0;f<Mathf.CeilToInt(hz*1.5f);f++)Step(new PlayerInput{Tracking=true});
                    if(b.Phase!=GamePhase.Battle||world.MonsterRushSteps!=count)throw new Exception($"Resume phase={b.Phase} steps={world.MonsterRushSteps} expected={count}");
                }
                Debug.Log($"[MonsterRushFlow] hz={hz} cut={cut} reset={reset} pause/resume/newRound/duplicate=passed");
            }
        }
        public static void Counter()=>RunCounter(true);
        public static void MeleeCounter()=>RunCounter(false);
        static void RunCounter(bool ranged)
        {
            var report=new StringBuilder();
            foreach(int hz in new[]{15,30,60})foreach(float cut in new[]{.02f,.20f,.40f})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var world=new GameWorld();var b=new Battle();var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);
                var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                float dt=1f/hz,time=0;int budget=hz*40;
                void Step(PlayerInput input){time+=dt;b.Tick(dt,input);while(b.TryCue(out var cue))world.Cue(cue,b);hero.Update(b,world.Camera,dt,time);enemy.Update(b,world.Camera,dt,time);world.Tick(b,dt,time);}
                Step(new PlayerInput{Tracking=true,Transform=true});
                while(!(b.Enemy==EnemyPhase.Recover&&b.EnemyAge>=cut)&&budget-->0)Step(new PlayerInput{Tracking=true,Shield=true});
                if(budget<=0)throw new Exception("Counter fixture missed retreat");
                var l=enemy.FootPosition(true);var r=enemy.FootPosition(false);var p=enemy.Root.position;
                var rt=hz==60&&cut==.02f?new RenderTexture(1280,720,24):null;
                if(rt){rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;Directory.CreateDirectory(Folder+(ranged?"/counter-frames":"/counter-melee-frames"));}
                float footSpeed=0,bodySpeed=0,ground=99,duplicate=0;var mesh=new Mesh();var skins=enemy.Root.GetComponentsInChildren<SkinnedMeshRenderer>();int hits=b.HitSequence;bool cancelled=false;
                for(int f=0;f<hz*2;f++)
                {
                    Step(new PlayerInput{Tracking=true,LeftPunch=f==0,RangedAttack=ranged});
                    footSpeed=Mathf.Max(footSpeed,Vector3.Distance(l,enemy.FootPosition(true))/dt,Vector3.Distance(r,enemy.FootPosition(false))/dt);
                    bodySpeed=Mathf.Max(bodySpeed,Vector3.Distance(p,enemy.Root.position)/dt);
                    l=enemy.FootPosition(true);r=enemy.FootPosition(false);p=enemy.Root.position;
                    var sampled=enemy.Root.position;enemy.Update(b,world.Camera,0,time);
                    duplicate=Mathf.Max(duplicate,Vector3.Distance(sampled,enemy.Root.position),Vector3.Distance(l,enemy.FootPosition(true)),Vector3.Distance(r,enemy.FootPosition(false)));
                    if(f%Mathf.Max(1,hz/30)==0)foreach(var skin in skins){skin.BakeMesh(mesh,true);foreach(var v in mesh.vertices)ground=Mathf.Min(ground,skin.transform.TransformPoint(v).y);}
                    if(rt&&f%2==0)CharacterReview.Save(world.Camera,rt,$"{Folder}/{(ranged?"counter-frames":"counter-melee-frames")}/{f/2:D4}.jpg");
                    if(b.HitSequence>hits){cancelled=true;if(b.Enemy==EnemyPhase.Recover&&enemy.RushAge<0)throw new Exception("Ordinary recoil discarded in-flight feet");}
                }
                if(rt){world.Camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
                UnityEngine.Object.DestroyImmediate(mesh);
                if(!cancelled)throw new Exception("Counter never landed");
                if(ground<-.07f||duplicate>.001f)throw new Exception($"Counter floor/repeated sample failed: ground={ground} zero={duplicate}");
                if(footSpeed>9||bodySpeed>13)throw new Exception($"Counter snapped feet/body: hz={hz} cut={cut} feet={footSpeed} body={bodySpeed}");
                string row=$"ranged={ranged} hz={hz} cut={cut:F2} footSpeed={footSpeed:F4} bodySpeed={bodySpeed:F4} minY={ground:F6} zero={duplicate:F6} recoil-with-step=pass";
                report.AppendLine(row);Debug.Log("[RetreatCounter] "+row);
            }
            Directory.CreateDirectory(Folder);File.WriteAllText(Folder+(ranged?"/counter.txt":"/counter-melee.txt"),report.ToString());
        }
        static Transform Bone(Transform root,string name)=>root.GetComponentsInChildren<Transform>().First(t=>t.name==name);
        static void Run(string version,int hz,int attack,bool movie)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(1074);
            var world=new GameWorld();var b=new Battle();var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);
            var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
            for(int i=0;i<20000;i++)
            {if(b.Enemy==EnemyPhase.Windup&&b.EnemyAttackCount==attack-1&&b.WarningDuration-b.EnemyAge<.6f)break;b.Tick(1/60f,new PlayerInput{Tracking=true,Shield=true});}
            if(b.EnemyAttackCount!=attack-1)throw new Exception("Rush fixture");while(b.TryCue(out _)){}
            float time=0,dt=1f/hz;for(int i=0;i<30;i++){hero.Update(b,world.Camera,1/60f,time);enemy.Update(b,world.Camera,1/60f,time);world.Tick(b,1/60f,time);time+=1/60f;}
            string dir=Folder+"/"+version+"/"+attack+"-"+hz;Directory.CreateDirectory(dir);if(movie)Directory.CreateDirectory(dir+"/frames");
            var left=Bone(enemy.Root,"bip_foot_L");var right=Bone(enemy.Root,"bip_foot_R");var hands=new[]{Bone(enemy.Root,"bip_hand_L"),Bone(enemy.Root,"bip_hand_R")};
            var axis=-world.BattleAxis;var footPrevious=new[]{left.position,right.position};var handPrevious=hands.Select(t=>t.position).ToArray();
            float plantedSlip=0,maxHand=0,zero=0,ground=99;int blocks=b.Blocks,hits=b.HitsTaken;bool leadLeft=MonsterStepMotion.LeadLeft(attack);
            var states=new StringBuilder("frame,enemy,age,hero,hits,blocks,health,energy\n");
            var csv=new StringBuilder("frame,enemy,age,rootForward,leftForward,leftY,rightForward,rightY,plantSlip,handStep\n");
            var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;var mesh=new Mesh();
            var skins=enemy.Root.GetComponentsInChildren<SkinnedMeshRenderer>();
            try
            {
                for(int f=0;f<hz*4;f++)
                {
                    time+=dt;b.Tick(dt,new PlayerInput{Tracking=true,Shield=true});while(b.TryCue(out var cue))world.Cue(cue,b);
                    hero.Update(b,world.Camera,dt,time);enemy.Update(b,world.Camera,dt,time);world.Tick(b,dt,time);
                    var feet=new[]{left.position,right.position};float slip=0,handStep=0;
                    for(int side=0;side<2;side++)
                    {
                        bool lead=(side==0)==leadLeft;
                        bool planted=b.Enemy==EnemyPhase.Attack&&(lead?b.EnemyAge>.44f&&b.EnemyAge<.60f:b.EnemyAge>.27f&&b.EnemyAge<.50f);
                        if(planted)slip=Mathf.Max(slip,Vector3.ProjectOnPlane(feet[side]-footPrevious[side],Vector3.up).magnitude);
                        handStep=Mathf.Max(handStep,Vector3.Distance(hands[side].position,handPrevious[side]));
                        handPrevious[side]=hands[side].position;footPrevious[side]=feet[side];
                    }
                    maxHand=Mathf.Max(maxHand,handStep);plantedSlip=Mathf.Max(plantedSlip,slip);
                    foreach(var skin in skins){skin.BakeMesh(mesh,true);foreach(var v in mesh.vertices)ground=Mathf.Min(ground,skin.transform.TransformPoint(v).y);}
                    var root=enemy.Root.position;enemy.Update(b,world.Camera,0,time);zero=Mathf.Max(zero,Vector3.Distance(root,enemy.Root.position));
                    zero=Mathf.Max(zero,Vector3.Distance(feet[0],left.position),Vector3.Distance(feet[1],right.position));
                    states.AppendLine(FormattableString.Invariant($"{f},{b.Enemy},{b.EnemyAge:F5},{b.Action},{b.HitsTaken},{b.Blocks},{b.EnemyHealth:F3},{b.Energy:F3}"));
                    csv.AppendLine(FormattableString.Invariant($"{f},{b.Enemy},{b.EnemyAge:F5},{Vector3.Dot(root-world.EnemyHome,axis):F6},{Vector3.Dot(left.position-world.EnemyHome,axis):F6},{left.position.y:F6},{Vector3.Dot(right.position-world.EnemyHome,axis):F6},{right.position.y:F6},{slip:F6},{handStep:F6}"));
                    if(movie&&f%2==0)CharacterReview.Save(world.Camera,rt,$"{dir}/frames/{f/2:D4}.jpg");
                }
                string result=$"attack={attack} hz={hz} plantedFootStep={plantedSlip:F6} handStep={maxHand:F6} minimumY={ground:F6} zeroError={zero:F6} blocks={b.Blocks-blocks} hits={b.HitsTaken-hits} rushSteps={world.MonsterRushSteps}";
                File.WriteAllText(dir+"/metrics.txt",result);File.WriteAllText(dir+"/motion.csv",csv.ToString());File.WriteAllText(dir+"/states.csv",states.ToString());Debug.Log("[MonsterRetreatReview] "+result);
                if(b.Blocks-blocks!=1||b.HitsTaken!=hits||zero>.001f||ground<-.07f||hz==60&&maxHand>.50f)throw new Exception(result);
                if(version=="after"&&plantedSlip>.008f)throw new Exception("Grounded foot slides: "+result);
                if(version=="after"&&world.MonsterRushSteps!=3)throw new Exception("Missing or duplicated footfall: "+result);
            }
            finally{world.Camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(mesh);}
        }
    }
}
