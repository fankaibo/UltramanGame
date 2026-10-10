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
    public static class LiveBodyReview
    {
        public static void Before()=>Run(false);
        public static void After()=>Run(true);
        static Transform Bone(Transform root,string a,string b)=>root.GetComponentsInChildren<Transform>().Single(t=>t.name==a||t.name==b);
        public static PoseFrame Pose(int f,float time)
        {
            var p=new PosePoint[33];for(int i=0;i<p.Length;i++)p[i]=new PosePoint(.5f,.5f);
            p[0]=new PosePoint(.5f,.12f);p[11]=new PosePoint(.65f,.30f);p[12]=new PosePoint(.35f,.30f);
            p[13]=new PosePoint(.68f,.5f);p[14]=new PosePoint(.32f,.5f);
            p[15]=new PosePoint(.67f,.75f);p[16]=new PosePoint(.33f,.75f);
            p[23]=new PosePoint(.60f,.69f);p[24]=new PosePoint(.40f,.69f);
            float lean=time<.5f?0:time<1.1f?Mathf.SmoothStep(0,1,(time-.5f)/.6f):time<1.7f?1:
                time<2.5f?Mathf.Lerp(1,-1,Mathf.SmoothStep(0,1,(time-1.7f)/.8f)):time<3.1f?-1:
                time<3.6f?Mathf.Lerp(-1,0,Mathf.SmoothStep(0,1,(time-3.1f)/.5f)):0;
            if(time>=4.3f&&time<5.4f)lean=.8f;
            foreach(int i in new[]{0,11,12,13,14,15,16})p[i].x+=lean*.13f;
            p[11].y+=lean*.035f;p[12].y-=lean*.035f;
            if(time>=5.8f){p[23].visibility=p[24].visibility=.1f;}
            return new PoseFrame{schema=1,source="synthetic",streamId="live-body-review",sequence=f+1,capturedMs=200000+(long)(time*1000),tracked=true,points=p};
        }
        static void Run(bool after)
        {
            string folder=Path.GetFullPath("../artifacts/live-body-20261010/"+(after?"after":"before"));Directory.CreateDirectory(folder);
            var metrics=new StringBuilder();var gameplay=new StringBuilder();var traces=new StringBuilder("hero,hz,frame,headX,hipX,leftY,rightY\n");
            foreach(string id in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})foreach(int hz in new[]{15,30,60})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(101080);
                var world=new GameWorld();var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);world.SetHeroProfile(id);
                var head=Bone(hero.Root,"head","bip_head");var hip=Bone(hero.Root,"hip","bip_pelvis");
                var b=new Battle();b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});b.GiveInstructionTime(60);while(b.TryCue(out _)){}
                var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
                var mesh=new Mesh();float dt=1f/hz,minGround=100,repeat=0,maxStep=0,leanLeft=0,leanRight=0,footDrift=0;Vector3 old=Vector3.zero,neutral=Vector3.zero,lf=Vector3.zero,rf=Vector3.zero;
                bool film=id=="Tiga"&&hz==60;if(film)Directory.CreateDirectory(folder+"/frames");
                try
                {
                    for(int f=0;f<hz*7;f++)
                    {
                        float t=f*dt;var pose=Pose(f,t);var input=new PlayerInput{Tracking=!(t>=5.4f&&t<5.85f),LeftPunch=f==Mathf.RoundToInt(3.75f*hz),Shield=t>=4.8f&&t<5.3f};
                        b.Tick(dt,input);while(b.TryCue(out var cue))world.Cue(cue,b);
                        hero.ObserveReadyPose(pose,pose.capturedMs,input,b,dt,true);hero.Update(b,world.Camera,dt,t);enemy.Update(b,world.Camera,dt,t);world.Tick(b,dt,t);
                        var local=Quaternion.Inverse(Quaternion.LookRotation(world.BattleAxis))*(head.position-world.HeroHome);
                        if(f>0)maxStep=Mathf.Max(maxStep,Vector3.Distance(local,old));old=local;
                        if(f==Mathf.RoundToInt(.4f*hz)){neutral=local;lf=hero.FootPosition(true);rf=hero.FootPosition(false);}
                        if(f==Mathf.RoundToInt(1.55f*hz))leanLeft=local.x-neutral.x;
                        if(f==Mathf.RoundToInt(3f*hz))leanRight=local.x-neutral.x;
                        if(t>.5f&&t<3.5f)footDrift=Mathf.Max(footDrift,Vector3.Distance(lf,hero.FootPosition(true)),Vector3.Distance(rf,hero.FootPosition(false)));
                        if(f%5==0)foreach(var skin in hero.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {skin.BakeMesh(mesh,true);foreach(var v in mesh.vertices)minGround=Mathf.Min(minGround,skin.transform.TransformPoint(v).y);}
                        var held=head.position;hero.ObserveReadyPose(pose,pose.capturedMs,input,b,0,true);hero.Update(b,world.Camera,0,t);repeat=Mathf.Max(repeat,Vector3.Distance(held,head.position));
                        gameplay.AppendLine($"{id},{hz},{f},{b.Phase},{b.Action},{b.Shield},{b.Punches},{b.EnemyHealth},{b.Energy}");
                        traces.AppendLine(FormattableString.Invariant($"{id},{hz},{f},{local.x:F6},{hero.Root.InverseTransformPoint(hip.position).x:F6},{hero.FootPosition(true).y:F6},{hero.FootPosition(false).y:F6}"));
                        if(film&&f%2==0)CharacterReview.Save(world.Camera,rt,$"{folder}/frames/{f/2:D4}.png");
                        if(hz==60&&(f==93||f==180||f==300||f==390))CharacterReview.Save(world.Camera,rt,$"{folder}/{id}-{f}.png");
                    }
                    string line=FormattableString.Invariant($"{id}/{hz} lean={leanLeft:F4}/{leanRight:F4} footDrift={footDrift:F4} ground={minGround:F4} maxHeadStep={maxStep:F4} repeat={repeat:F6}");metrics.AppendLine(line);
                    if(after&&(leanLeft>-.10f||leanRight<.10f||footDrift>.07f||minGround<-.055f||repeat>.0001f))throw new Exception(line);
                    if(b.Punches!=1||b.EnemyHealth!=49||b.Energy!=1)throw new Exception("Body review changed combat");
                }
                finally{world.Camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(mesh);}
            }
            File.WriteAllText(folder+"/metrics.txt",metrics.ToString());File.WriteAllText(folder+"/gameplay.csv",gameplay.ToString());File.WriteAllText(folder+"/body.csv",traces.ToString());
            if(after&&File.ReadAllText(folder+"/../before/gameplay.csv")!=gameplay.ToString())throw new Exception("Gameplay changed");
            if(after)
            {CheckInterruptions(folder);ReadyArmsReview.Regression();}
            Debug.Log("[LiveBodyReview] PASS\n"+metrics);
        }
        static Battle StartBattle()
        {var b=new Battle();b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});b.GiveInstructionTime(60);return b;}
        static void CheckInterruptions(string folder)
        {
            var report=new StringBuilder();
            foreach(string id in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})foreach(string mode in new[]{"pause","keyboard","new-round"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var camera=new GameObject("ReviewCamera").AddComponent<Camera>();var a=new AnimatedActor(id,Vector3.zero,Vector3.forward*7);var neutral=new AnimatedActor(id,Vector3.zero,Vector3.forward*7);
                var b=StartBattle();float dt=1f/60;var input=new PlayerInput{Tracking=true};
                for(int f=0;f<180;f++)
                {
                    if(f==60){if(mode=="pause")b.Pause();if(mode=="new-round")b=StartBattle();}
                    input.Tracking=mode!="pause"||f<60;b.Tick(dt,input);
                    var pose=Pose(f,1.3f);pose.capturedMs+=f*17;
                    a.ObserveReadyPose(pose,pose.capturedMs,input,b,dt,f<60);neutral.ObserveReadyPose(pose,pose.capturedMs,input,b,dt,false);
                    a.Update(b,camera,dt,f*dt);neutral.Update(b,camera,dt,f*dt);
                }
                float head=Vector3.Distance(Bone(a.Root,"head","bip_head").position,Bone(neutral.Root,"head","bip_head").position);
                float feet=Mathf.Max(Vector3.Distance(a.FootPosition(true),neutral.FootPosition(true)),Vector3.Distance(a.FootPosition(false),neutral.FootPosition(false)));
                if(head>.003f||feet>.003f)throw new Exception($"Body layer leaked after {id}/{mode}: {head}/{feet}");
                report.AppendLine($"{id}/{mode} head={head:F6} feet={feet:F6}");
            }
            File.WriteAllText(folder+"/interruptions.txt",report.ToString());
        }
    }
}
