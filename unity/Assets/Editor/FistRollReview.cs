using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;
namespace UltramanGame.Editor
{
    public static class FistRollReview
    {
        static string Root=>Path.GetFullPath(Application.dataPath+"/../../artifacts/fist-roll-20261012");
        static Transform Bone(Transform root,string name){foreach(var t in root.GetComponentsInChildren<Transform>())if(t.name==name)return t;throw new Exception(name);}
        public static void Before()=>Run("before");
        public static void After()=>Run("after");
        static void Run(string version)
        {
            string dir=Root+"/"+version;Directory.CreateDirectory(dir+"/frames");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(135);
            var w=new GameWorld();var h=new AnimatedActor("Tiga",w.HeroHome,w.EnemyHome);var e=new AnimatedActor("Golza",w.EnemyHome,w.HeroHome,true);w.BindActors(h,e);w.SetHeroProfile("Tiga");
            var b=new Battle();b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});b.GiveInstructionTime(30);while(b.TryCue(out _)){}
            var report=new StringBuilder();foreach(var t in h.Root.GetComponentsInChildren<Transform>())if(t.name.Contains("Thumb")||t.name.Contains("Finger")||t.name.Contains("Hand")||t.name.Contains("finger"))report.AppendLine($"{t.name} parent={t.parent?.name} pos={t.position:F4}");
            File.WriteAllText(dir+"/bones.txt",report.ToString());report.Clear();
            var wrists=new[]{Bone(h.Root,"HandBase_L"),Bone(h.Root,"HandBase_R")};var elbows=new[]{Bone(h.Root,"ForearmBase_L"),Bone(h.Root,"ForearmBase_R")};var thumbs=new[]{Bone(h.Root,"Thumb1_L"),Bone(h.Root,"Thumb1_R")};
            var trace=new StringBuilder("frame,action,actionAge,shield,punches,health,energy,lx,ly,lz,rx,ry,rz\n");
            var previous=new Quaternion[2];float maxAngle=0,repeatAngle=0;
            var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();w.Camera.targetTexture=rt;w.Camera.aspect=16f/9;
            try
            {
                for(int f=0;f<360;f++)
                {
                    var input=new PlayerInput{Tracking=true,LeftPunch=f==60,RightPunch=f==180,RangedAttack=true,Shield=f>=270&&f<300};b.Tick(1f/60,input);while(b.TryCue(out var cue))w.Cue(cue,b);
                    h.Update(b,w.Camera,1f/60,f/60f);e.Update(b,w.Camera,1f/60,f/60f);w.Tick(b,1f/60,f/60f);
                    for(int i=0;i<2;i++){if(f>0)maxAngle=Mathf.Max(maxAngle,Quaternion.Angle(previous[i],wrists[i].rotation));previous[i]=wrists[i].rotation;}
                    if(f==40||f==100||f==335)
                    {
                        var a=wrists[0].rotation;var c=wrists[1].rotation;h.Update(b,w.Camera,0,f/60f);
                        repeatAngle=Mathf.Max(repeatAngle,Quaternion.Angle(a,wrists[0].rotation),Quaternion.Angle(c,wrists[1].rotation));
                    }
                    var l=wrists[0].position;var r=wrists[1].position;trace.AppendLine(FormattableString.Invariant($"{f},{b.Action},{b.ActionAge:F5},{b.Shield},{b.Punches},{b.EnemyHealth},{b.Energy},{l.x:F5},{l.y:F5},{l.z:F5},{r.x:F5},{r.y:F5},{r.z:F5}"));
                    if(f%2==0)CharacterReview.Save(w.Camera,rt,$"{dir}/frames/{f/2:D4}.jpg");
                    if(f==40||f==70||f==100||f==190||f==285||f==335)
                    {
                        for(int i=0;i<2;i++)
                        {var axis=(wrists[i].position-elbows[i].position).normalized;var thumb=Vector3.ProjectOnPlane(thumbs[i].position-wrists[i].position,axis).normalized;if(version=="after"&&(f==40||f==100||f==335)&&Vector3.Dot(thumb,Vector3.up)<.85f)throw new Exception("Ready fist still supinated");report.AppendLine($"frame={f} side={i} thumbUp={Vector3.Dot(thumb,Vector3.up):F4} thumbForward={Vector3.Dot(thumb,w.BattleAxis):F4} axis={axis:F4}");}
                        CharacterReview.Save(w.Camera,rt,$"{dir}/stage-{f}.png");
                        var position=w.Camera.transform.position;var rotation=w.Camera.transform.rotation;float fov=w.Camera.fieldOfView;
                        var mid=(wrists[0].position+wrists[1].position)*.5f;w.Camera.transform.position=mid-w.BattleAxis*1.4f+Vector3.up*.55f+Vector3.Cross(Vector3.up,w.BattleAxis)*1.6f;w.Camera.transform.LookAt(mid);w.Camera.fieldOfView=45;
                        CharacterReview.Save(w.Camera,rt,$"{dir}/hands-{f}.png");w.Camera.transform.SetPositionAndRotation(position,rotation);w.Camera.fieldOfView=fov;
                    }
                }
                File.WriteAllText(dir+"/trace.csv",trace.ToString());File.WriteAllText(dir+"/measurements.txt",report.ToString());
                if(version=="after")
                {
                    var old=File.ReadAllLines(Root+"/before/trace.csv");var current=trace.ToString().TrimEnd().Split('\n');float delta=0,transitionDelta=0,guardStep=0;
                    if(old.Length!=current.Length)throw new Exception("Frame count changed");
                    float Number(string v)=>float.Parse(v,System.Globalization.CultureInfo.InvariantCulture);
                    for(int row=1;row<old.Length;row++)
                    {
                        var a=old[row].TrimEnd().Split(',');var c=current[row].TrimEnd().Split(',');int frame=int.Parse(c[0]);
                        for(int j=0;j<7;j++)if(a[j]!=c[j])throw new Exception("Battle state changed");
                        // Changing the starting forearm roll changes the small
                        // curved path during quaternion guard handoffs. Require
                        // unchanged endpoints everywhere else, and a bounded,
                        // continuous entry/exit rather than identical old rolls.
                        bool handoff=frame>=270&&frame<280||frame>=300&&frame<310;
                        for(int j=7;j<13;j++){float d=Mathf.Abs(Number(a[j])-Number(c[j]));if(handoff)transitionDelta=Mathf.Max(transitionDelta,d);else delta=Mathf.Max(delta,d);}
                        if(frame>=265&&frame<=315&&row>1)
                        {var prev=current[row-1].TrimEnd().Split(',');for(int j=7;j<13;j+=3)guardStep=Mathf.Max(guardStep,Vector3.Distance(new Vector3(Number(prev[j]),Number(prev[j+1]),Number(prev[j+2])),new Vector3(Number(c[j]),Number(c[j+1]),Number(c[j+2]))));}
                    }
                    if(delta>.0001f||transitionDelta>.05f||guardStep>.10f||repeatAngle>.1f||maxAngle>30)throw new Exception($"Pose continuity failed position={delta} handoffDelta={transitionDelta} guardStep={guardStep} repeat={repeatAngle} step={maxAngle}");
                    report.AppendLine($"PASS rules=360 wristDeltaOutsideHandoffs={delta:F6} handoffDelta={transitionDelta:F6} guardHandStep={guardStep:F6} repeatDegrees={repeatAngle:F4} maxWristStepDegrees={maxAngle:F3}");
                }
                File.WriteAllText(dir+"/trace.csv",trace.ToString());File.WriteAllText(dir+"/measurements.txt",report.ToString());Debug.Log("[FistRollReview] "+version+" complete\n"+report);
            }
            finally{w.Camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
        }
    }
}
