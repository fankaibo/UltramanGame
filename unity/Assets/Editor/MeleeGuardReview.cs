using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class MeleeGuardReview
    {
        public static void Before()=>Run("before");
        public static void After()=>Run("after");
        public static void Release(){After();HeroGuardHandoffReview.Validate();RosterPunchReview.CheckGuardTransitions();SurfaceImpactReview.CheckPunchRecovery();}
        static void Require(bool value,string message){if(!value)throw new Exception(message);}
        static Battle Start()
        {
            var b=new Battle(200);b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
            while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});
            b.GiveInstructionTime(60);while(b.TryCue(out _)){}return b;
        }
        static void Run(string version)
        {
            string folder=Path.GetFullPath("../artifacts/melee-guard-20261009/"+version);Directory.CreateDirectory(folder);
            var report=new StringBuilder("hero,hz,speed,left,guardAt,intent,firstStep,maxStep,firstPalm,maxPalm,hits,settleGap,firstRoot,maxRoot,maxFoot\n");
            var trace=new StringBuilder("case,frame,action,age,shield,hits,health,energy\n");int cases=0;
            foreach(string name in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})foreach(int hz in new[]{15,30,60})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(100959);
                var world=new GameWorld();var hero=new AnimatedActor(name,world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);
                world.BindActors(hero,enemy);world.SetHeroProfile(name);
                var all=hero.Root.GetComponentsInChildren<Transform>();Transform left=null,right=null;
                foreach(var bone in all){if(bone.name=="HandBase_L"||bone.name=="bip_hand_L")left=bone;if(bone.name=="HandBase_R"||bone.name=="bip_hand_R")right=bone;}
                Require(left&&right,"Missing hands "+name);
                var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
                try
                {
                    foreach(float speed in new[]{.65f,1.7f})foreach(bool activeLeft in new[]{true,false})foreach(float at in new[]{.04f,.15f,.27f})foreach(bool intent in new[]{false,true})
                    {
                        var b=Start();float dt=1f/hz,time=0;int frame=0,guardFrame=-1;bool launched=false;
                        float first=0,max=0,firstPalm=0,maxPalm=0;Vector3 oldL=Vector3.zero,oldR=Vector3.zero;Quaternion oldLR=Quaternion.identity,oldRR=Quaternion.identity;
                        float firstRoot=0,maxRoot=0,maxFoot=0;Vector3 oldRoot=hero.Root.position,oldLF=hero.FootPosition(true),oldRF=hero.FootPosition(false);
                        bool movie=hz==60&&speed==.65f&&activeLeft&&at==.15f&&!intent&&(name=="Tiga"||name=="Mebius");
                        string frames=folder+"/"+name+"/frames";if(movie)Directory.CreateDirectory(frames);
                        for(;frame<hz*2;frame++)
                        {
                            var input=new PlayerInput{Tracking=true,AttackSpeed=speed};
                            if(time>=.30f&&!launched){launched=true;input.LeftPunch=activeLeft;input.RightPunch=!activeLeft;}
                            if(launched&&guardFrame<0&&b.IsPunch&&b.ActionAge>=at)guardFrame=frame;
                            if(guardFrame>=0){input.GuardIntent=true;input.Shield=!intent||frame-guardFrame>=2;}
                            b.Tick(dt,input);while(b.TryCue(out _)){}
                            hero.Update(b,world.Camera,dt,time);enemy.Update(b,world.Camera,dt,time);world.Tick(b,dt,time);
                            Vector3 l=left.position,r=right.position;
                            float step=Mathf.Max(Vector3.Distance(l,oldL),Vector3.Distance(r,oldR));
                            float palm=Mathf.Max(Quaternion.Angle(left.rotation,oldLR),Quaternion.Angle(right.rotation,oldRR));
                            float rootStep=Vector3.Distance(hero.Root.position,oldRoot),footStep=Mathf.Max(Vector3.Distance(hero.FootPosition(true),oldLF),Vector3.Distance(hero.FootPosition(false),oldRF));
                            if(guardFrame>=0&&time-guardFrame*dt<.35f)
                            {max=Mathf.Max(max,step);maxPalm=Mathf.Max(maxPalm,palm);maxRoot=Mathf.Max(maxRoot,rootStep);maxFoot=Mathf.Max(maxFoot,footStep);if(frame==guardFrame){first=step;firstPalm=palm;firstRoot=rootStep;}}
                            if(guardFrame>=0&&input.Shield)Require(b.Shield,"Presentation delayed shield");
                            oldL=l;oldR=r;oldLR=left.rotation;oldRR=right.rotation;
                            oldRoot=hero.Root.position;oldLF=hero.FootPosition(true);oldRF=hero.FootPosition(false);
                            trace.AppendLine(FormattableString.Invariant($"{cases},{frame},{b.Action},{b.ActionAge:F5},{b.Shield},{b.Punches},{b.EnemyHealth},{b.Energy}"));
                            if(movie&&frame%2==0)CharacterReview.Save(world.Camera,rt,frames+"/"+(frame/2).ToString("D4")+".png");
                            time+=dt;
                        }
                        Require(guardFrame>=0&&b.Shield,"Guard scenario did not run");
                        var frozenL=left.position;var frozenR=right.position;
                        hero.Update(b,world.Camera,0,time-dt);enemy.Update(b,world.Camera,0,time-dt);
                        float repeat=Mathf.Max(Vector3.Distance(frozenL,left.position),Vector3.Distance(frozenR,right.position));
                        Require(repeat<.0001f,"Repeated sample drift "+name);
                        if(version=="after")
                        {
                            float stepScale=60f/hz;
                            Require(first<.15f*stepScale&&max<.35f*stepScale&&maxRoot<.23f*stepScale&&maxFoot<.22f*stepScale,"Guard handoff discontinuity "+name+"-"+hz+"-"+activeLeft+"-"+at+" first="+first+" max="+max);
                            Require(maxPalm<13*stepScale,"Wrist rotation discontinuity "+name);
                        }
                        report.AppendLine(FormattableString.Invariant($"{name},{hz},{speed:F2},{activeLeft},{at:F2},{intent},{first:F5},{max:F5},{firstPalm:F3},{maxPalm:F3},{b.Punches},{repeat:F6},{firstRoot:F5},{maxRoot:F5},{maxFoot:F5}"));cases++;
                    }
                }
                finally{world.Camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
            }
            File.WriteAllText(folder+"/cases.csv",report.ToString());File.WriteAllText(folder+"/trace.csv",trace.ToString());
            var sources=new StringBuilder();foreach(string p in Directory.GetFiles(Application.dataPath+"/Scripts","*.cs",SearchOption.AllDirectories))
                using(var sha=System.Security.Cryptography.SHA256.Create())sources.AppendLine(p.Substring(Application.dataPath.Length+1)+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant());
            File.WriteAllText(folder+"/sources.txt",sources.ToString());Debug.Log("[MeleeGuardReview] "+version+" cases="+cases);
        }
    }
}
