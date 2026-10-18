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
    public static class RangedBodyReview
    {
        static string Root=>Path.GetFullPath(Application.dataPath+"/../../artifacts/a160-combat-follow");
        public static void Before()
        {
            using(var sha=SHA256.Create())
            {
                string hash=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Application.dataPath+"/Scripts/Runtime/RiggedActor.cs"))).Replace("-","").ToLowerInvariant();
                if(hash!="b12d405fdde3fb09c049aa29ba47a0d7c9b3338b52cb24b6f80e0d608385c523")throw new Exception("Before requires A-159 production source");
            }
            Run(false);
        }
        public static void After(){Run(true);RangedCastTransitions.Validate();}
        static Transform Bone(AnimatedActor a,string name)=>a.Root.GetComponentsInChildren<Transform>().Single(t=>t.name==name);
        static PoseFrame Pose(int f,int hz,float t,bool leaning)
        {
            var p=new PosePoint[33];for(int i=0;i<33;i++)p[i]=new PosePoint(.5f,.5f){z=-.1f};
            p[0]=new PosePoint(.5f,.1f);p[11]=new PosePoint(.65f,.35f);p[12]=new PosePoint(.35f,.35f);
            p[13]=new PosePoint(.68f,.39f);p[14]=new PosePoint(.32f,.39f);p[15]=new PosePoint(.58f,.20f){z=-.20f};p[16]=new PosePoint(.42f,.20f){z=-.20f};
            p[23]=new PosePoint(.6f,.74f);p[24]=new PosePoint(.4f,.74f);
            float lean=leaning?Mathf.Sin(t*1.5f):0;
            foreach(int i in new[]{0,11,12,13,14,15,16})p[i].x+=lean*.12f;
            p[11].y+=lean*.03f;p[12].y-=lean*.03f;
            return new PoseFrame{schema=1,source="synthetic",streamId="cast-body",sequence=f+1,capturedMs=100000+(long)(f*1000.0/hz),tracked=true,points=p};
        }
        static void Run(bool after)
        {
            string root=Root+(after?"/after":"/before");Directory.CreateDirectory(root);var report=new StringBuilder();
            foreach(int hz in new[]{15,30,60})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(160);
                var w=new GameWorld();var h=new AnimatedActor("Tiga",w.HeroHome,w.EnemyHome);var e=new AnimatedActor("Golza",w.EnemyHome,w.HeroHome,true);w.BindActors(h,e);w.SetHeroProfile("Tiga");
                var neutral=new AnimatedActor("Tiga",w.HeroHome,w.EnemyHome);foreach(var skin in neutral.Root.GetComponentsInChildren<Renderer>())skin.forceRenderingOff=true;
                var head=Bone(h,"head");var nhead=Bone(neutral,"head");var lateral=Vector3.Cross(Vector3.up,w.BattleAxis);
                var b=new Battle();b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});b.GiveInstructionTime(30);while(b.TryCue(out _)){}
                var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();w.Camera.targetTexture=rt;w.Camera.aspect=16f/9;
                string folder=root+"/"+hz;Directory.CreateDirectory(folder);if(hz==60)Directory.CreateDirectory(folder+"/frames");
                var rules=new StringBuilder();var trace=new StringBuilder("frame,age,action,headOffset,headStep,leftFootError,rightFootError\n");var mesh=new Mesh();
                float dt=1f/hz,health=50,feet=0,repeat=0,ground=100,headStep=0,castOffset=0;int casts=0;Vector3 lastHead=head.position;
                try
                {
                    for(int f=0;f<hz*9;f++)
                    {
                        float t=f*dt;var input=new PlayerInput{Tracking=true};
                        for(int n=0;n<4;n++)if(f==(1+n*2)*hz){input.RangedAttack=true;input.LeftPunch=n%2==0;input.RightPunch=n%2==1;input.AttackSpeed=n%2==0?.8f:1.4f;}
                        b.Tick(w.BattleDelta(dt,b),input);while(b.TryCue(out var cue))w.Cue(cue,b);
                        var pose=Pose(f,hz,t,true);var flat=Pose(f,hz,t,false);
                        h.ObserveReadyPose(pose,pose.capturedMs,input,b,dt,true);neutral.ObserveReadyPose(flat,flat.capturedMs,input,b,dt,true);
                        h.Update(b,w.Camera,dt,t);neutral.Update(b,w.Camera,dt,t);e.Update(b,w.Camera,dt,t);
                        if(b.EnemyHealth<health)w.Hit(false,b);health=b.EnemyHealth;w.Tick(b,dt,t);
                        float step=f>0?Vector3.Distance(lastHead,head.position):0;lastHead=head.position;headStep=Mathf.Max(headStep,step);
                        float offset=Vector3.Dot(head.position-nhead.position,lateral);
                        float lf=Vector3.Distance(h.FootPosition(true),neutral.FootPosition(true)),rf=Vector3.Distance(h.FootPosition(false),neutral.FootPosition(false));feet=Mathf.Max(feet,lf,rf);
                        if(b.IsRangedPunch&&b.ActionAge>=.18f&&b.ActionAge<=.30f){castOffset+=Mathf.Abs(offset);casts++;}
                        var held=head.position;h.Update(b,w.Camera,0,t);repeat=Mathf.Max(repeat,Vector3.Distance(held,head.position));
                        if(f%Mathf.Max(1,hz/15)==0)foreach(var skin in h.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {skin.BakeMesh(mesh,true);foreach(var v in mesh.vertices)ground=Mathf.Min(ground,skin.transform.TransformPoint(v).y);}
                        rules.AppendLine(FormattableString.Invariant($"{f},{b.Action},{b.ActionAge:F5},{b.Punches},{b.Energy},{b.EnemyHealth},{w.Projectile.Launches},{w.Projectile.Impacts}"));
                        trace.AppendLine(FormattableString.Invariant($"{f},{b.ActionAge:F5},{b.Action},{offset:F6},{step:F6},{lf:F6},{rf:F6}"));
                        if(hz==60&&f%2==0)CharacterReview.Save(w.Camera,rt,$"{folder}/frames/{f/2:D4}.jpg",()=>
                        {if(neutral.Root.GetComponentsInChildren<MeshRenderer>().Any(r=>r.gameObject.name=="Skin review snapshot"))throw new Exception("Hidden reference rig leaked into render");});
                    }
                    float mean=castOffset/Mathf.Max(1,casts);string line=$"{hz}Hz hits={b.Punches} feet={feet:F6} ground={ground:F6} repeat={repeat:F6} maxHeadStep={headStep:F6} meanCastLean={mean:F6}";
                    File.WriteAllText(folder+"/rules.csv",rules.ToString());File.WriteAllText(folder+"/trace.csv",trace.ToString());report.AppendLine(line);Debug.Log("[RangedBodyReview] "+line);
                    if(b.Punches!=4||w.Projectile.Impacts!=4||repeat>.0001f||feet>.035f||ground<-.055f)throw new Exception(line);
                    if(after)
                    {
                        if(File.ReadAllText(Root+"/before/"+hz+"/rules.csv")!=rules.ToString())throw new Exception("Body follow changed gameplay");
                        if(mean<.06f)throw new Exception("Body follow disappears during casts: "+line);
                    }
                }
                finally{w.Camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(mesh);}
            }
            File.WriteAllText(root+"/validation.txt",report.ToString());
        }
    }
}
