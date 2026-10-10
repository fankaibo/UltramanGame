using System;
using System.IO;
using System.Text;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class KnockdownKneeReview
    {
        public static void Before()=>Run("before","Tiga",60,true);
        public static void After()
        {foreach(string id in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})foreach(int hz in new[]{15,30,60})Run("after",id,hz,id=="Tiga"&&hz==60);}
        static void Run(string version,string id,int hz,bool movie)
        {
            // Keep later regressions separate from the original before/after evidence.
            string reviewRoot=Environment.GetEnvironmentVariable("ULTRAMAN_KNEE_REVIEW_ROOT")??"knee-rise-20261008";
            string folder=Path.GetFullPath($"../artifacts/{reviewRoot}/{version}/{id}-{hz}");Directory.CreateDirectory(folder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(1008);
            var world=new GameWorld();var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);world.SetHeroProfile(id);
            if(!hero.IsRigged||!enemy.IsRigged)throw new Exception("Actual rig missing");
            var b=new Battle();b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});while(b.TryCue(out _)){}
            float time=0,dt=1f/hz;int landings=0;
            void Step(bool shield)
            {time+=dt;b.Tick(dt,new PlayerInput{Tracking=true,Shield=shield});while(b.TryCue(out var cue)){if(cue==GameCue.HeroLanded)landings++;world.Cue(cue,b);}hero.Update(b,world.Camera,dt,time);enemy.Update(b,world.Camera,dt,time);world.Tick(b,dt,time);}
            for(int n=0;n<45;n++)Step(false);
            int wait=0;while(!(b.Enemy==EnemyPhase.Windup&&b.EnemyAttackCount==1&&b.WarningDuration-b.EnemyAge<1.5f)&&wait++<hz*70)Step(true);
            if(wait>=hz*70)throw new Exception("Rock windup not reached");
            var rt=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
            var joints=hero.Root.GetComponentsInChildren<Transform>();Transform Bone(params string[] names)=>joints.First(x=>names.Contains(x.name));
            var hips=new[]{Bone("ThighBase_L","bip_hip_L"),Bone("ThighBase_R","bip_hip_R")};var knees=new[]{Bone("Shin_L","bip_knee_L"),Bone("Shin_R","bip_knee_R")};var feet=new[]{Bone("Foot_L","bip_foot_L"),Bone("Foot_R","bip_foot_R")};
            var pelvis=Bone("hip","bip_pelvis");var forward=world.BattleAxis;float restLeft=feet[0].position.y,restRight=feet[1].position.y;
            float minForward=1,minKnee=180,ground=100,unsupported=0,maxStep=0;Vector3 oldKnee=knees[0].position;int violations=0,samples=0;bool saved=false;var mesh=new Mesh();
            var csv=new StringBuilder("frame,action,age,hits,landings,hipY,leftKneeAngle,rightKneeAngle,leftBendForward,rightBendForward,leftY,rightY\n");
            if(movie)Directory.CreateDirectory(folder+"/frames");
            try
            {
                for(int f=0;f<hz*5;f++)
                {
                    Step(false);float[] angle=new float[2],direction=new float[2];
                    for(int i=0;i<2;i++)
                    {
                        angle[i]=Vector3.Angle(hips[i].position-knees[i].position,feet[i].position-knees[i].position);
                        Vector3 axis=(feet[i].position-hips[i].position).normalized;
                        Vector3 bend=Vector3.ProjectOnPlane(knees[i].position-hips[i].position,axis).normalized;
                        direction[i]=Vector3.Dot(bend,Vector3.ProjectOnPlane(forward,axis).normalized);
                        if(b.Action==HeroAction.Hurt&&b.ActionAge>=1.12f&&b.ActionAge<=1.6f&&angle[i]<170)
                        {samples++;minForward=Mathf.Min(minForward,direction[i]);minKnee=Mathf.Min(minKnee,angle[i]);if(direction[i]<.5f)violations++;}
                    }
                    if(b.Action==HeroAction.Hurt)
                    {
                        maxStep=Mathf.Max(maxStep,Vector3.Distance(oldKnee,knees[0].position));
                        if(b.ActionAge>=.32f&&b.ActionAge<=1.6f)unsupported=Mathf.Max(unsupported,Mathf.Min(feet[0].position.y-restLeft,feet[1].position.y-restRight));
                        foreach(var skin in hero.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {skin.BakeMesh(mesh,true);foreach(var v in mesh.vertices)ground=Mathf.Min(ground,skin.transform.TransformPoint(v).y);}
                        if(!saved&&b.ActionAge>=1.18f){CharacterReview.Save(world.Camera,rt,folder+"/rise.png");saved=true;}
                    }
                    oldKnee=knees[0].position;
                    csv.AppendLine(FormattableString.Invariant($"{f},{b.Action},{b.ActionAge:F5},{b.HitsTaken},{landings},{pelvis.position.y:F5},{angle[0]:F3},{angle[1]:F3},{direction[0]:F5},{direction[1]:F5},{feet[0].position.y:F5},{feet[1].position.y:F5}"));
                    if(movie&&f%2==0)CharacterReview.Save(world.Camera,rt,folder+"/frames/"+(f/2).ToString("D4")+".png");
                }
                File.WriteAllText(folder+"/trace.csv",csv.ToString());
                string line=$"{id}-{hz} hits={b.HitsTaken} landings={landings} recovered={b.Action==HeroAction.None} riseSamples={samples} reversedKnees={violations} minForward={minForward:F4} minKnee={minKnee:F2} ground={ground:F4} unsupported={unsupported:F4} kneeStep={maxStep:F4}";
                File.WriteAllText(folder+"/validation.txt",line);Debug.Log("[KnockdownKnee] "+line);
                if(b.HitsTaken!=1||landings!=1||!saved||b.Action!=HeroAction.None||samples<6)throw new Exception("Incomplete rock recovery: "+line);
                if(version=="after"&&(violations>0||ground<-.05f||unsupported>.05f))throw new Exception("Invalid support pose: "+line);
            }
            finally{world.Camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(mesh);}
            using(var sha=System.Security.Cryptography.SHA256.Create())File.WriteAllText(folder+"/rig-sha256.txt",BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Application.dataPath+"/Scripts/Runtime/RiggedActor.cs"))).Replace("-","").ToLowerInvariant());
        }
    }
}
