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
    public static class GuardBodyReview
    {
        static string Root
        {
            get {var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"--guard-body-output");
                if(i<0||i+1>=a.Length)throw new ArgumentException("Explicit --guard-body-output required");return Path.GetFullPath(a[i+1]);}
        }
        static string Hash(string path)
        {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
        public static void Before()
        {
            if(Hash(Application.dataPath+"/Scripts/Runtime/RiggedActor.cs")!="c2586fd062e4cfeaac9bfe8f4b2b2cf4aa3adee78e3b8562abe1d128845dc9a0"||
                Hash(Application.dataPath+"/Scripts/Core/LiveReadyPose.cs")!="9c8406d8de60f28f669f10cc73537e451568147ef33d721cad1b88077d840119")
                throw new Exception("Before requires A-181 runtime");
            Run(false);
        }
        public static void After()=>Run(true);
        static Transform Bone(AnimatedActor a,string name)=>a.Root.GetComponentsInChildren<Transform>().Single(t=>t.name==name);
        static PoseFrame Pose(int f,int hz,float t,bool leaning)
        {
            var p=new PosePoint[33];for(int i=0;i<p.Length;i++)p[i]=new PosePoint(.5f,.5f){z=-.1f};
            p[0]=new PosePoint(.5f,.1f);p[11]=new PosePoint(.65f,.35f);p[12]=new PosePoint(.35f,.35f);
            p[13]=new PosePoint(.68f,.46f);p[14]=new PosePoint(.32f,.46f);p[15]=new PosePoint(.53f,.20f){z=-.20f};p[16]=new PosePoint(.47f,.20f){z=-.20f};
            p[23]=new PosePoint(.6f,.74f);p[24]=new PosePoint(.4f,.74f);
            float lean=leaning?Mathf.Cos(t*1.55f):0;
            foreach(int i in new[]{0,11,12,13,14,15,16})p[i].x+=lean*.12f;
            p[11].y+=lean*.03f;p[12].y-=lean*.03f;
            return new PoseFrame{schema=1,source="synthetic",streamId="guard-body",sequence=f+1,capturedMs=100000+(long)(f*1000.0/hz),tracked=true,points=p};
        }
        static void Run(bool after)
        {
            string root=Root+(after?"/after":"/before");Directory.CreateDirectory(root);var report=new StringBuilder();
            File.WriteAllText(root+"/sources.txt",string.Join("\n",new[]{"Scripts/Runtime/RiggedActor.cs","Scripts/Core/LiveReadyPose.cs"}.Select(p=>p+" "+Hash(Application.dataPath+"/"+p))));
            foreach(int hz in new[]{15,30,60})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(182);
                var w=new GameWorld();var h=new AnimatedActor("Tiga",w.HeroHome,w.EnemyHome);var e=new AnimatedActor("Golza",w.EnemyHome,w.HeroHome,true);w.BindActors(h,e);w.SetHeroProfile("Tiga");
                var flat=new AnimatedActor("Tiga",w.HeroHome,w.EnemyHome);foreach(var r in flat.Root.GetComponentsInChildren<Renderer>())r.forceRenderingOff=true;
                var head=Bone(h,"head");var flatHead=Bone(flat,"head");var axis=Vector3.Cross(Vector3.up,w.BattleAxis);
                var b=new Battle(200);b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});
                while(b.Enemy!=EnemyPhase.Windup)b.Tick(.02f,new PlayerInput{Tracking=true});while(b.TryCue(out _)){}
                var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();w.Camera.targetTexture=rt;w.Camera.aspect=16f/9;
                string folder=root+"/"+hz;Directory.CreateDirectory(folder);if(hz==60)Directory.CreateDirectory(folder+"/frames");
                var rules=new StringBuilder("frame,age,action,enemy,enemyAge,shield,blocks,hitsTaken,punches,energy,health\n");
                var trace=new StringBuilder("frame,shield,headOffset,headStep,footError,ground\n");var mesh=new Mesh();
                float dt=1f/hz,health=200,feet=0,repeat=0,ground=100,headStep=0,sum=0;int samples=0;Vector3 lastHead=head.position;
                try
                {
                    for(int f=0;f<hz*9;f++)
                    {
                        float t=f*dt;bool guard=t>=1&&t<6.8f||t>=7.45f;
                        var input=new PlayerInput{Tracking=true,Shield=guard,GuardIntent=guard};
                        if(f==Mathf.RoundToInt(6.9f*hz)){input.RangedAttack=true;input.RightPunch=true;input.AttackSpeed=1.3f;}
                        b.Tick(w.BattleDelta(dt,b),input);while(b.TryCue(out var cue))w.Cue(cue,b);
                        var p=Pose(f,hz,t,true);var q=Pose(f,hz,t,false);
                        h.ObserveReadyPose(p,p.capturedMs,input,b,dt,true);flat.ObserveReadyPose(q,q.capturedMs,input,b,dt,true);
                        h.Update(b,w.Camera,dt,t);flat.Update(b,w.Camera,dt,t);e.Update(b,w.Camera,dt,t);
                        if(b.EnemyHealth<health)w.Hit(false,b);health=b.EnemyHealth;w.Tick(b,dt,t);
                        float step=f>0?Vector3.Distance(lastHead,head.position):0;lastHead=head.position;headStep=Mathf.Max(headStep,step);
                        float offset=Vector3.Dot(head.position-flatHead.position,axis);
                        float foot=Mathf.Max(Vector3.Distance(h.FootPosition(true),flat.FootPosition(true)),Vector3.Distance(h.FootPosition(false),flat.FootPosition(false)));feet=Mathf.Max(feet,foot);
                        if(b.Shield&&t>1.5f&&t<4.5f){sum+=Mathf.Abs(offset);samples++;}
                        var held=head.position;h.Update(b,w.Camera,0,t);repeat=Mathf.Max(repeat,Vector3.Distance(held,head.position));
                        if(f%Mathf.Max(1,hz/15)==0)foreach(var skin in h.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {skin.BakeMesh(mesh,true);foreach(var v in mesh.vertices)ground=Mathf.Min(ground,skin.transform.TransformPoint(v).y);}
                        rules.AppendLine(FormattableString.Invariant($"{f},{b.ActionAge:F5},{b.Action},{b.Enemy},{b.EnemyAge:F5},{b.Shield},{b.Blocks},{b.HitsTaken},{b.Punches},{b.Energy},{b.EnemyHealth}"));
                        trace.AppendLine(FormattableString.Invariant($"{f},{b.Shield},{offset:F6},{step:F6},{foot:F6},{ground:F6}"));
                        if(hz==60&&f%2==0)CharacterReview.Save(w.Camera,rt,$"{folder}/frames/{f/2:D4}.jpg");
                    }
                    float mean=sum/Mathf.Max(1,samples);string line=$"{hz}Hz blocks={b.Blocks} hits={b.HitsTaken} punches={b.Punches} feet={feet:F6} ground={ground:F6} repeat={repeat:F6} headStep={headStep:F6} meanGuardLean={mean:F6}";
                    File.WriteAllText(folder+"/rules.csv",rules.ToString());File.WriteAllText(folder+"/trace.csv",trace.ToString());report.AppendLine(line);Debug.Log("[GuardBodyReview] "+line);
                    if(b.Blocks!=1||b.HitsTaken!=0||b.Punches!=1||repeat>.0001f||feet>.035f||ground<-.055f)throw new Exception(line);
                    if(after)
                    {
                        if(File.ReadAllText(Root+"/before/"+hz+"/rules.csv")!=rules.ToString())throw new Exception("Guard body changed gameplay");
                        if(mean<.06f)throw new Exception("Guard body still frozen: "+line);
                    }
                }
                finally{w.Camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(mesh);}
            }
            File.WriteAllText(root+"/validation.txt",report.ToString());
        }
    }
}
