using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class HighCastReview
    {
        public static void Before()
        {
            // A baseline must use the old runtime with this review fixture.
            // Refuse to overwrite it with the candidate under the same label.
            using(var sha=SHA256.Create())
            {
                string hash=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Application.dataPath+"/Scripts/Runtime/RiggedActor.cs"))).Replace("-","").ToLowerInvariant();
                if(hash!="7debc4d0577c080f0ebbc4e6edcbcc23beb66cd46dcb748de95bb3690fdeedd1")
                    throw new Exception("Before requires RiggedActor.cs from baseline 0f3af64, not the candidate");
            }
            Run("before");
        }
        public static void After()=>Run("after");
        static Transform Bone(AnimatedActor a,string name)
        {foreach(var t in a.Root.GetComponentsInChildren<Transform>())if(t.name==name)return t;throw new Exception(name);}
        static PoseFrame Pose(int frame,int hz,float t)
        {
            var p=new PosePoint[33];for(int n=0;n<33;n++)p[n]=new PosePoint(.5f,.5f){z=-.1f};
            p[11]=new PosePoint(.65f,.35f){z=-.1f};p[12]=new PosePoint(.35f,.35f){z=-.1f};
            p[13]=new PosePoint(.68f,.39f){z=-.12f};p[14]=new PosePoint(.32f,.39f){z=-.12f};
            p[15]=new PosePoint(.58f,.20f){z=-.2f};p[16]=new PosePoint(.42f,.20f){z=-.2f};
            if(t>=1&&t<7)
            {
                int cycle=(int)((t-1)/1.5f);float phase=(t-1)%1.5f;
                bool left=cycle%2==0;float duration=left?.18f:.55f;
                float e=Mathf.Clamp01((phase-.15f)/duration);
                if(phase>duration+.4f)e=Mathf.Clamp01(1-(phase-duration-.4f)/.25f);
                int hand=left?15:16;p[hand].x+=(left?1:-1)*.08f*e;p[hand].z-=.28f*e;
            }
            return new PoseFrame{schema=1,source="synthetic",streamId="high-cast",sequence=frame+1,capturedMs=100000+(long)(frame*1000.0/hz),tracked=true,points=p};
        }
        static void Run(string version)
        {
            string root=Path.GetFullPath(Application.dataPath+"/../../artifacts/high-cast-20261012/"+version);Directory.CreateDirectory(root);
            var report=new StringBuilder();
            foreach(int hz in new[]{15,30,60})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(128);
                var w=new GameWorld();var h=new AnimatedActor("Tiga",w.HeroHome,w.EnemyHome);var e=new AnimatedActor("Golza",w.EnemyHome,w.HeroHome,true);w.BindActors(h,e);w.SetHeroProfile("Tiga");
                var b=new Battle();b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});b.GiveInstructionTime(30);while(b.TryCue(out _)){}
                var r=new GestureRecognizer{Difficulty=1};float dt=1f/hz,health=50;int oldLaunch=0;
                var lu=Bone(h,"armBase_L");var ll=Bone(h,"ForearmBase_L");var lh=Bone(h,"HandBase_L");var ru=Bone(h,"armBase_R");var rl=Bone(h,"ForearmBase_R");var rh=Bone(h,"HandBase_R");
                var lf=Bone(h,"Foot_L");var rf=Bone(h,"Foot_R");
                string folder=root+"/"+hz;Directory.CreateDirectory(folder);if(hz==60)Directory.CreateDirectory(folder+"/frames");
                var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();w.Camera.targetTexture=rt;w.Camera.aspect=16f/9;
                var trace=new StringBuilder("frame,action,age,shield,launches,hits,energy,leftElbow,rightElbow,leftX,leftY,leftZ,rightX,rightY,rightZ\n");
                var rules=new StringBuilder();var feet=new StringBuilder();float minSupport=180,maxSupport=0,maxStep=0,repeat=0;
                Vector3 oldLeft=lh.position,oldRight=rh.position;
                try
                {
                    for(int f=0;f<hz*8;f++)
                    {
                        float t=f*dt;var pose=Pose(f,hz,t);var input=r.Update(pose,pose.capturedMs,false,false);
                        b.Tick(w.BattleDelta(dt,b),input);while(b.TryCue(out var cue))w.Cue(cue,b);
                        h.ObserveReadyPose(pose,pose.capturedMs,input,b,dt,true);
                        h.Update(b,w.Camera,dt,t);e.Update(b,w.Camera,dt,t);if(b.EnemyHealth<health)w.Hit(false,b);health=b.EnemyHealth;w.Tick(b,dt,t);
                        float la=Vector3.Angle(lu.position-ll.position,lh.position-ll.position),ra=Vector3.Angle(ru.position-rl.position,rh.position-rl.position);
                        if(f>0)maxStep=Mathf.Max(maxStep,Vector3.Distance(oldLeft,lh.position),Vector3.Distance(oldRight,rh.position));
                        oldLeft=lh.position;oldRight=rh.position;
                        h.Update(b,w.Camera,0,t);repeat=Mathf.Max(repeat,Vector3.Distance(oldLeft,lh.position),Vector3.Distance(oldRight,rh.position));
                        trace.AppendLine(FormattableString.Invariant($"{f},{b.Action},{b.ActionAge:F5},{b.Shield},{w.Projectile.Launches},{b.Punches},{b.Energy},{la:F3},{ra:F3},{lh.position.x:F5},{lh.position.y:F5},{lh.position.z:F5},{rh.position.x:F5},{rh.position.y:F5},{rh.position.z:F5}"));
                        rules.AppendLine(FormattableString.Invariant($"{f},{b.Action},{b.ActionAge:F5},{b.Shield},{b.Punches},{b.Energy},{b.EnemyHealth},{w.Projectile.Launches},{w.Projectile.Impacts}"));
                        feet.AppendLine(FormattableString.Invariant($"{f},{lf.position.x:F5},{lf.position.y:F5},{lf.position.z:F5},{rf.position.x:F5},{rf.position.y:F5},{rf.position.z:F5}"));
                        if(w.Projectile.Launches>oldLaunch)
                        {float support=b.Action==HeroAction.LeftPunch?ra:la;minSupport=Mathf.Min(minSupport,support);maxSupport=Mathf.Max(maxSupport,support);
                            string row=$"{hz} Hz launch={w.Projectile.Launches} action={b.Action} age={b.ActionAge:F3} elbows={la:F2}/{ra:F2}";report.AppendLine(row);Debug.Log("[HighCast] "+row);CharacterReview.Save(w.Camera,rt,$"{folder}/launch-{w.Projectile.Launches}.png");oldLaunch=w.Projectile.Launches;}
                        if(hz==60&&f%2==0)CharacterReview.Save(w.Camera,rt,$"{folder}/frames/{f/2:D4}.jpg");
                    }
                    File.WriteAllText(folder+"/trace.csv",trace.ToString());File.WriteAllText(folder+"/rules.csv",rules.ToString());File.WriteAllText(folder+"/feet.csv",feet.ToString());
                    if(b.Punches!=4||w.Projectile.Impacts!=4||w.Projectile.Launches!=4)throw new Exception("Expected four alternating casts");
                    if(repeat>.0001f)throw new Exception("Zero-time redraw changed pose");
                    if(version=="after")
                    {
                        string baseline=Path.GetFullPath(root+"/../before/"+hz);
                        if(File.ReadAllText(baseline+"/rules.csv")!=rules.ToString()||File.ReadAllText(baseline+"/feet.csv")!=feet.ToString())throw new Exception("Support pose changed gameplay or feet");
                        if(minSupport<60||maxSupport>100)throw new Exception("Support fist is folded or reaching like a second attack");
                        string[] old=File.ReadAllLines(baseline+"/trace.csv"),current=trace.ToString().TrimEnd().Split('\n');
                        if(old.Length!=current.Length)throw new Exception("Frame count changed");
                        for(int i=1;i<old.Length;i++)
                        {
                            var a=old[i].TrimEnd().Split(',');var c=current[i].TrimEnd().Split(',');
                            int start=c[1]=="LeftPunch"?9:c[1]=="RightPunch"?12:-1;
                            if(start>=0)for(int n=start;n<start+3;n++)if(a[n]!=c[n])throw new Exception("Emitting hand changed at "+i);
                        }
                    }
                    report.AppendLine($"{hz} Hz four hits, zero-time={repeat:F6}, maxHandStep={maxStep:F6}, support={minSupport:F2}..{maxSupport:F2}");
                }
                finally{w.Camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
            }
            File.WriteAllText(root+"/validation.txt",report.ToString());
        }
    }
}
