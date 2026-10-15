using System;
using System.IO;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    // Identical accepted attacks with different camera wrist heights. This
    // measures presentation, not recognition accuracy or a second recognizer.
    public static class RangedHeightReview
    {
        public static void Before()=>Run(false);
        public static void After()=>Run(true);
        static Transform Bone(AnimatedActor actor,string name)
        {foreach(var bone in actor.Root.GetComponentsInChildren<Transform>())if(bone.name==name)return bone;throw new Exception(name);}
        static float BaselineStep(string file)
        {
            var rows=File.ReadAllLines(file);Vector3 previous=default;float max=0;
            for(int i=1;i<rows.Length;i++)
            {
                var cells=rows[i].Split(',');var hand=new Vector3(float.Parse(cells[3],CultureInfo.InvariantCulture),float.Parse(cells[4],CultureInfo.InvariantCulture),float.Parse(cells[5],CultureInfo.InvariantCulture));
                if(i>1)max=Mathf.Max(max,Vector3.Distance(previous,hand));previous=hand;
            }
            return max;
        }
        static PoseFrame Pose(int f,int hz,float t,bool left)
        {
            var p=new PosePoint[33];for(int n=0;n<33;n++)p[n]=new PosePoint(.5f,.5f);
            p[11]=new PosePoint(.65f,.30f);p[12]=new PosePoint(.35f,.30f);
            p[13]=new PosePoint(.70f,.50f);p[14]=new PosePoint(.30f,.50f);
            p[15]=new PosePoint(.67f,.60f);p[16]=new PosePoint(.33f,.60f);
            // Second cast begins beside the face. After acceptance, reverse
            // camera height: the accepted cast must not chase camera jitter.
            if(t>=1.6f&&t<2.25f)p[left?15:16].y=.15f;
            return new PoseFrame{schema=1,source="synthetic",streamId="cast-height",sequence=f+1,capturedMs=100000+(long)(f*1000.0/hz),tracked=true,points=p};
        }
        static void Run(bool check)
        {
            string root=Path.GetFullPath(Application.dataPath+"/../../artifacts/ranged-height-20261011");
            string folder=root+(check?"/after":"/before");Directory.CreateDirectory(folder);
            var report=new StringBuilder();var gameplay=new StringBuilder();var feet=new StringBuilder();
            foreach(int hz in new[]{15,30,60})foreach(bool left in new[]{false,true})foreach(float speed in new[]{.65f,1.7f})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(117);
                var w=new GameWorld();var h=new AnimatedActor("Tiga",w.HeroHome,w.EnemyHome);var e=new AnimatedActor("Golza",w.EnemyHome,w.HeroHome,true);w.BindActors(h,e);w.SetHeroProfile("Tiga");
                // Editor-only baseline switch; keep live ready/support motion
                // identical while reproducing the former horizontal cast.
                var rig=typeof(AnimatedActor).GetField("rigged",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(h);
                var cast=(RangedCastPose)typeof(RiggedActor).GetField("rangedCast",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(rig);
                var b=new Battle();b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});b.GiveInstructionTime(30);while(b.TryCue(out _)){}
                string key=$"{hz}-{(left?"left":"right")}-{speed:F2}";string path=folder+"/"+key;Directory.CreateDirectory(path);
                bool movie=hz==60&&!left; if(movie)Directory.CreateDirectory(path+"/frames");
                var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();w.Camera.targetTexture=rt;w.Camera.aspect=16f/9;
                var wrist=Bone(h,left?"HandBase_L":"HandBase_R");var upper=Bone(h,left?"armBase_L":"armBase_R");var lower=Bone(h,left?"ForearmBase_L":"ForearmBase_R");
                var lf=Bone(h,"Foot_L");var rf=Bone(h,"Foot_R");
                float dt=1f/hz,health=50,low=-99,high=-99,spanError=0,maxStep=0,repeat=0,minScreen=1,maxScreen=0;
                Vector3 old=wrist.position;bool first=true,second=false;
                var motion=new StringBuilder("frame,action,age,handX,handY,handZ,elevation,reach,projectileX,projectileY,projectileZ\n");
                try
                {
                    for(int f=-hz;f<hz*4;f++)
                    {
                        float t=f*dt;bool fire=f==Mathf.RoundToInt(.5f*hz)||f==Mathf.RoundToInt(2.2f*hz);
                        var input=new PlayerInput{Tracking=true,RangedAttack=fire,AttackSpeed=speed,LeftPunch=fire&&left,RightPunch=fire&&!left,Shield=t>=3.1f&&t<3.5f};
                        b.Tick(dt,input);while(b.TryCue(out var cue))w.Cue(cue,b);
                        var pose=Pose(f+hz,hz,t,left);h.ObserveReadyPose(pose,pose.capturedMs,input,b,dt,true);
                        if(!check)typeof(RangedCastPose).GetProperty("Elevation").SetValue(cast,0f);
                        h.Update(b,w.Camera,dt,t);e.Update(b,w.Camera,dt,t);if(b.EnemyHealth<health)w.Hit(false,b);health=b.EnemyHealth;w.Tick(b,dt,t);
                        if(f<0){old=wrist.position;continue;}
                        maxStep=Mathf.Max(maxStep,Vector3.Distance(old,wrist.position));old=wrist.position;
                        float span=Vector3.Distance(upper.position,lower.position)+Vector3.Distance(lower.position,wrist.position);
                        float reach=Vector3.Distance(upper.position,wrist.position)/span;
                        spanError=Mathf.Max(spanError,Mathf.Max(0,reach-1));
                        var point=w.Camera.WorldToViewportPoint(wrist.position);minScreen=Mathf.Min(minScreen,point.y);maxScreen=Mathf.Max(maxScreen,point.y);
                        float elevation=(wrist.position-upper.position).y/span;
                        if(b.IsRangedPunch&&b.ActionAge>=.12f&&b.ActionAge<.26f)
                        {
                            if(t<1.5f&&first){low=elevation;first=false;if(movie)CharacterReview.Save(w.Camera,rt,path+"/low.png");}
                            if(t>2&& !second){high=elevation;second=true;if(movie)CharacterReview.Save(w.Camera,rt,path+"/high.png");}
                        }
                        h.Update(b,w.Camera,0,t);repeat=Mathf.Max(repeat,Vector3.Distance(old,wrist.position));
                        var hand=wrist.position;var tip=w.Projectile.Tip;
                        gameplay.AppendLine(FormattableString.Invariant($"{key},{f},{b.Action},{b.ActionAge:F5},{b.Shot.Age:F5},{b.EnemyHealth},{b.Energy},{b.Punches},{b.Blocks},{w.Projectile.Launches},{w.Projectile.Impacts}"));
                        var a=lf.position;var z=rf.position;feet.AppendLine(FormattableString.Invariant($"{key},{f},{a.x:F5},{a.y:F5},{a.z:F5},{z.x:F5},{z.y:F5},{z.z:F5}"));
                        motion.AppendLine(FormattableString.Invariant($"{f},{b.Action},{b.ActionAge:F5},{hand.x:F5},{hand.y:F5},{hand.z:F5},{elevation:F5},{reach:F5},{tip.x:F5},{tip.y:F5},{tip.z:F5}"));
                        if(movie&&f%2==0)CharacterReview.Save(w.Camera,rt,$"{path}/frames/{f/2:D4}.jpg");
                    }
                    string line=$"{key} low={low:F4} high={high:F4} difference={high-low:F4} handStep={maxStep:F4} stretch={spanError:F5} repeat={repeat:F6} screenY={minScreen:F3}..{maxScreen:F3} hits={b.Punches}";
                    report.AppendLine(line);File.WriteAllText(path+"/motion.csv",motion.ToString());Debug.Log("[RangedHeightReview] "+line);
                    if(b.Punches!=2||w.Projectile.Launches!=2||w.Projectile.Impacts!=2||first||!second||spanError>.001f||repeat>.0001f)throw new Exception("Cast fixture failed: "+line);
                    // Compare the same accepted speed and frame interval. A fast
                    // cast legitimately travels farther per frame; do not use
                    // an arbitrary world-units/second cap as a discontinuity test.
                    if(check&&(high-low<.25f||high-low>.65f||maxStep>BaselineStep(root+"/before/"+key+"/motion.csv")+.0001f||minScreen<.12f||maxScreen>.90f))throw new Exception("Cast continuity/framing failed: "+line);
                }
                finally{w.Camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
            }
            File.WriteAllText(folder+"/gameplay.csv",gameplay.ToString());File.WriteAllText(folder+"/feet.csv",feet.ToString());File.WriteAllText(folder+"/validation.txt",report.ToString());
            if(check&&(File.ReadAllText(root+"/before/gameplay.csv")!=gameplay.ToString()||File.ReadAllText(root+"/before/feet.csv")!=feet.ToString()))throw new Exception("Cast presentation changed gameplay or feet");
            Debug.Log("[RangedHeightReview] PASS "+folder);
        }
    }
}
