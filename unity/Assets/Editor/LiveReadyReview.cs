using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class LiveReadyReview
    {
        public static void Before()=>Run(false);
        public static void After()=>Run(true);
        static Transform Bone(Transform root,string a,string b)=>root.GetComponentsInChildren<Transform>().Single(t=>t.name==a||t.name==b);
        static PoseFrame Pose(int f,float time)
        {
            var p=new PosePoint[33];for(int i=0;i<33;i++)p[i]=new PosePoint(.5f,.5f);
            p[11]=new PosePoint(.65f,.30f);p[12]=new PosePoint(.35f,.30f);
            p[15]=new PosePoint(.67f,.62f);p[16]=new PosePoint(.33f,.62f);
            if(time>=1&&time<2)p[15].y=Mathf.Lerp(.62f,.12f,Mathf.SmoothStep(0,1,(time-1)/.3f));
            if(time>=2.5f&&time<3.5f)p[16].y=Mathf.Lerp(.62f,.12f,Mathf.SmoothStep(0,1,(time-2.5f)/.3f));
            if(time>=4&&time<4.8f)p[15].y=p[16].y=.2f;
            return new PoseFrame{schema=1,source="synthetic",streamId="live-ready-review",sequence=f+1,capturedMs=100000+(long)(time*1000),tracked=true,points=p};
        }
        static void Run(bool enabled)
        {
            string folder=Path.GetFullPath("../artifacts/live-ready-20261010/"+(enabled?"after":"before"));Directory.CreateDirectory(folder);
            var report=new StringBuilder();var trace=new StringBuilder();var bones=new StringBuilder();
            foreach(string id in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})foreach(int hz in new[]{15,30,60})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(101077);
                var world=new GameWorld();var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);
                var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);world.SetHeroProfile(id);
                var upper=new[]{Bone(hero.Root,"armBase_L","bip_upperArm_L"),Bone(hero.Root,"armBase_R","bip_upperArm_R")};
                var lower=new[]{Bone(hero.Root,"ForearmBase_L","bip_lowerArm_L"),Bone(hero.Root,"ForearmBase_R","bip_lowerArm_R")};
                var wrist=new[]{Bone(hero.Root,"HandBase_L","bip_hand_L"),Bone(hero.Root,"HandBase_R","bip_hand_R")};
                var b=new Battle();b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});b.GiveInstructionTime(60);while(b.TryCue(out _)){}
                var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
                bool wasReady=false;float maxActionStep=0;float lastExclusive=-1;float dt=1f/hz,minReach=1,maxReach=0,maxStep=0,repeat=0;var old=new Vector3[2];var neutral=new Vector3[2];float leftLift=0,rightCrossTalk=0,rightLift=0,leftCrossTalk=0;
                bool film=id=="Tiga"&&hz==60;if(film)Directory.CreateDirectory(folder+"/frames");
                try
                {
                    for(int f=0;f<hz*9;f++)
                    {
                        float time=f*dt;var pose=Pose(f,time);
                        var input=new PlayerInput{Tracking=!(time>=6&&time<6.4f),LeftPunch=f==Mathf.RoundToInt(1.85f*hz),RightPunch=f==Mathf.RoundToInt(3.35f*hz),Shield=time>=4.25f&&time<4.8f,GuardIntent=time>=4.1f&&time<4.8f};
                        b.Tick(dt,input);while(b.TryCue(out var cue))world.Cue(cue,b);
                        hero.ObserveReadyPose(pose,pose.capturedMs,input,b,dt,enabled);
                        hero.Update(b,world.Camera,dt,time);enemy.Update(b,world.Camera,dt,time);world.Tick(b,dt,time);
                        trace.AppendLine($"{id},{hz},{f},{b.Phase},{b.Action},{b.Shield},{b.Punches},{b.EnemyHealth},{b.Energy}");
                        if(!LiveReadyPose.Ready(b))lastExclusive=time;
                        for(int i=0;i<2;i++)
                        {
                            var local=hero.Root.InverseTransformPoint(wrist[i].position);
                            if(f>0)
                            {
                                float step=Vector3.Distance(local,old[i]);maxActionStep=Mathf.Max(maxActionStep,step);
                                if(LiveReadyPose.Ready(b)&&wasReady&&time-lastExclusive>.3f)maxStep=Mathf.Max(maxStep,step);
                            }
                            old[i]=local;
                            if(f==hz-1)neutral[i]=local;
                            if(f==Mathf.RoundToInt(1.8f*hz)) {if(i==0)leftLift=local.y-neutral[i].y;else rightCrossTalk=Mathf.Abs(local.y-neutral[i].y);}
                            if(f==Mathf.RoundToInt(3.1f*hz)) {if(i==1)rightLift=local.y-neutral[i].y;else leftCrossTalk=Mathf.Abs(local.y-neutral[i].y);}
                            if(LiveReadyPose.Ready(b)&&time-lastExclusive>.3f&&time>.3f||b.Phase==GamePhase.Paused&&time>6.7f)
                            {float span=Vector3.Distance(upper[i].position,lower[i].position)+Vector3.Distance(lower[i].position,wrist[i].position);float reach=Vector3.Distance(upper[i].position,wrist[i].position)/span;minReach=Mathf.Min(minReach,reach);maxReach=Mathf.Max(maxReach,reach);}
                            bones.AppendLine(FormattableString.Invariant($"{id},{hz},{f},{i},{local.x:F5},{local.y:F5},{local.z:F5}"));
                        }
                        wasReady=LiveReadyPose.Ready(b);
                        var held=wrist.Select(t=>t.position).ToArray();hero.ObserveReadyPose(pose,pose.capturedMs,input,b,0,enabled);hero.Update(b,world.Camera,0,time);
                        for(int i=0;i<2;i++)repeat=Mathf.Max(repeat,Vector3.Distance(held[i],wrist[i].position));
                        if(film&&f%2==0)CharacterReview.Save(world.Camera,rt,$"{folder}/frames/{f/2:D4}.png");
                        if(hz==60&&(f==108||f==198||f==510))CharacterReview.Save(world.Camera,rt,$"{folder}/{id}-{f}.png");
                    }
                    if(enabled&&(leftLift<.3f||rightLift<.3f||leftCrossTalk>.035f||rightCrossTalk>.035f||maxStep>dt*6f||maxActionStep>dt*36||minReach<.795f||maxReach>.925f||repeat>.0001f))throw new Exception($"Live hands failed {id}/{hz}: lift={leftLift}/{rightLift} cross={leftCrossTalk}/{rightCrossTalk} step={maxStep} reach={minReach}..{maxReach} repeat={repeat}");
                    if(b.Phase!=GamePhase.Battle||b.Punches!=2||b.EnemyHealth!=48||b.Energy!=2)throw new Exception("Unexpected battle result");
                    report.AppendLine(FormattableString.Invariant($"{id}/{hz} lift={leftLift:F4}/{rightLift:F4} crossTalk={leftCrossTalk:F4}/{rightCrossTalk:F4} localStep={maxStep:F4} actionStep={maxActionStep:F4} reach={minReach:F4}..{maxReach:F4} repeat={repeat:F6} hits=2 health=48"));
                }
                finally{world.Camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
            }
            File.WriteAllText(folder+"/metrics.txt",report.ToString());File.WriteAllText(folder+"/trace.csv",trace.ToString());File.WriteAllText(folder+"/hands.csv",bones.ToString());
            if(enabled&&File.ReadAllText(Path.Combine(folder,"../before/trace.csv"))!=trace.ToString())throw new Exception("Gameplay differs");
            Debug.Log("[LiveReadyReview] PASS\n"+report);
        }
    }
}
