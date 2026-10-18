using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class VolleyContinuityReview
    {
        static string Folder {get{var a=Environment.GetCommandLineArgs();int n=Array.IndexOf(a,"--volley-output");if(n<0||n+1>=a.Length)throw new ArgumentException("Explicit --volley-output required");return Path.GetFullPath(a[n+1]);}}
        static string Hash(string path){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
        public static void Before()
        {
            if(Hash(Application.dataPath+"/Scripts/Runtime/RiggedActor.cs")!="f5a93f79f33b017bdbc8f6d2022af6a24eb6fe1908dfbca41d0459230d85c946"||
                Hash(Application.dataPath+"/Scripts/Core/HeroPunchLink.cs")!="da00168254ca00cb84c65f1ef4ca853c2041015b0e050978a6bd9791e04b4938")throw new Exception("Before requires A183 source");
            Run("before");
        }
        public static void After()=>Run("after");
        static Transform Bone(AnimatedActor a,string name)=>a.Root.GetComponentsInChildren<Transform>().Single(t=>t.name==name);
        static PoseFrame Pose(int frame,int hz)
        {
            var p=new PosePoint[33];for(int i=0;i<33;i++)p[i]=new PosePoint(.5f,.5f){z=-.1f};
            p[0]=new PosePoint(.5f,.1f);p[11]=new PosePoint(.65f,.35f);p[12]=new PosePoint(.35f,.35f);
            p[13]=new PosePoint(.68f,.39f);p[14]=new PosePoint(.32f,.39f);p[15]=new PosePoint(.58f,.20f){z=-.2f};p[16]=new PosePoint(.42f,.20f){z=-.2f};p[23]=new PosePoint(.60f,.74f);p[24]=new PosePoint(.40f,.74f);
            return new PoseFrame{schema=1,source="synthetic",streamId="volley",sequence=frame+1,capturedMs=100000+(long)(frame*1000.0/hz),tracked=true,points=p};
        }
        static void Run(string version)
        {
            string folder=Folder+"/"+version;Directory.CreateDirectory(folder+"/frames");var report=new StringBuilder();
            var sources=new StringBuilder();foreach(string name in new[]{"Runtime/RiggedActor.cs","Core/HeroPunchLink.cs"})sources.AppendLine(name+" "+Hash(Application.dataPath+"/Scripts/"+name));File.WriteAllText(folder+"/sources.txt",sources.ToString());
            foreach(int hz in new[]{15,30,60})foreach(float speed in new[]{.75f,1.7f})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(184);
                var w=new GameWorld();var h=new AnimatedActor("Tiga",w.HeroHome,w.EnemyHome);var e=new AnimatedActor("Golza",w.EnemyHome,w.HeroHome,true);w.BindActors(h,e);w.SetHeroProfile("Tiga");
                var b=new Battle(200);b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});b.GiveInstructionTime(30);while(b.TryCue(out _)){}
                var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();w.Camera.targetTexture=rt;w.Camera.aspect=16f/9;
                string key=$"{hz}-{speed:F2}";var rules=new StringBuilder("frame,action,age,buffered,sequence,health,energy,punches,launches,impacts\n");
                var trace=new StringBuilder("frame,action,age,sequence,link,leftX,leftY,leftZ,rightX,rightY,rightZ,leftStep,rightStep,headStep,feetStep\n");
                float dt=1f/hz,health=200,maxStep=0,handoffStep=0,repeat=0,ground=100,spanError=0;int queued=0,oldSequence=0;
                var lh=Bone(h,"HandBase_L");var rh=Bone(h,"HandBase_R");var head=Bone(h,"head");var lu=Bone(h,"armBase_L");var ll=Bone(h,"ForearmBase_L");var ru=Bone(h,"armBase_R");var rl=Bone(h,"ForearmBase_R");
                Vector3 oldL=lh.position,oldR=rh.position,oldHead=head.position,oldLF=h.FootPosition(true),oldRF=h.FootPosition(false);var mesh=new Mesh();
                try
                {
                    for(int f=-hz;f<hz*6;f++)
                    {
                        float t=f*dt;var input=new PlayerInput{Tracking=true};
                        if(f==Mathf.RoundToInt(.3f*hz)){input.RangedAttack=input.LeftPunch=true;input.AttackSpeed=speed;}
                        if(b.IsRangedPunch&&b.ActionAge>=.25f&&b.AttackSequence<6&&queued!=b.AttackSequence)
                        {queued=b.AttackSequence;input.RangedAttack=true;input.LeftPunch=b.Action==HeroAction.RightPunch;input.RightPunch=!input.LeftPunch;input.AttackSpeed=speed;}
                        if(t>=4.6f&&t<5.15f)input.Shield=input.GuardIntent=true;
                        b.Tick(w.BattleDelta(dt,b),input);while(b.TryCue(out var cue))w.Cue(cue,b);
                        var pose=Pose(f+hz,hz);h.ObserveReadyPose(pose,pose.capturedMs,input,b,dt,true);h.Update(b,w.Camera,dt,t);e.Update(b,w.Camera,dt,t);
                        if(b.EnemyHealth<health)w.Hit(false,b);health=b.EnemyHealth;w.Tick(b,dt,t);
                        var l=lh.position;var r=rh.position;var lf=h.FootPosition(true);var rf=h.FootPosition(false);
                        float lstep=Vector3.Distance(l,oldL),rstep=Vector3.Distance(r,oldR),headStep=Vector3.Distance(head.position,oldHead),footStep=Mathf.Max(Vector3.Distance(lf,oldLF),Vector3.Distance(rf,oldRF));
                        oldL=l;oldR=r;oldHead=head.position;oldLF=lf;oldRF=rf;
                        if(f<0)continue;
                        maxStep=Mathf.Max(maxStep,lstep,rstep);if(b.AttackSequence>oldSequence&&oldSequence>0)handoffStep=Mathf.Max(handoffStep,lstep,rstep);oldSequence=b.AttackSequence;
                        spanError=Mathf.Max(spanError,Vector3.Distance(lu.position,lh.position)/(Vector3.Distance(lu.position,ll.position)+Vector3.Distance(ll.position,lh.position))-1,Vector3.Distance(ru.position,rh.position)/(Vector3.Distance(ru.position,rl.position)+Vector3.Distance(rl.position,rh.position))-1);
                        h.Update(b,w.Camera,0,t);repeat=Mathf.Max(repeat,Vector3.Distance(l,lh.position),Vector3.Distance(r,rh.position));
                        if(f%Mathf.Max(1,hz/15)==0)foreach(var skin in h.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {skin.BakeMesh(mesh,true);foreach(var v in mesh.vertices)ground=Mathf.Min(ground,skin.transform.TransformPoint(v).y);}
                        rules.AppendLine(FormattableString.Invariant($"{f},{b.Action},{b.ActionAge:F5},{b.BufferedPunch},{b.AttackSequence},{b.EnemyHealth},{b.Energy},{b.Punches},{w.Projectile.Launches},{w.Projectile.Impacts}"));
                        trace.AppendLine(FormattableString.Invariant($"{f},{b.Action},{b.ActionAge:F5},{b.AttackSequence},{h.LinkedPunchWeight:F5},{l.x:F6},{l.y:F6},{l.z:F6},{r.x:F6},{r.y:F6},{r.z:F6},{lstep:F6},{rstep:F6},{headStep:F6},{footStep:F6}"));
                        if(hz==60&&f%2==0)CharacterReview.Save(w.Camera,rt,$"{folder}/frames/{(speed>1?180:0)+f/2:D4}.jpg");
                    }
                    File.WriteAllText(folder+"/"+key+"-rules.csv",rules.ToString());File.WriteAllText(folder+"/"+key+"-trace.csv",trace.ToString());
                    string line=$"{key} shots={w.Projectile.Launches} hits={b.Punches} maxHandStep={maxStep:F5} handoffStep={handoffStep:F5} ground={ground:F5} stretch={spanError:F6} repeat={repeat:F6}";report.AppendLine(line);Debug.Log("[VolleyContinuityReview] "+line);
                    if(b.Punches!=6||w.Projectile.Launches!=6||w.Projectile.Impacts!=6||repeat>.0001f||spanError>.001f||ground<-.055f)throw new Exception(line);
                    if(version=="after"&&File.ReadAllText(Folder+"/before/"+key+"-rules.csv")!=rules.ToString())throw new Exception("Volley gameplay changed");
                }
                finally{w.Camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(mesh);}
            }
            File.WriteAllText(folder+"/validation.txt",report.ToString());
        }
    }
}
