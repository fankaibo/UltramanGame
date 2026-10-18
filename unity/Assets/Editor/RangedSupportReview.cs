using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class RangedSupportReview
    {
        public static void Before()=>Run("before");
        public static void After()=>Run("after");
        static PoseFrame Frame(int frame,int hz,float time,bool left)
        {
            var p=new PosePoint[33];for(int i=0;i<33;i++)p[i]=new PosePoint(.5f,.5f);
            p[11]=new PosePoint(.65f,.3f);p[12]=new PosePoint(.35f,.3f);
            p[13]=new PosePoint(.7f,.5f);p[14]=new PosePoint(.3f,.5f);
            p[15]=new PosePoint(.67f,.7f);p[16]=new PosePoint(.33f,.7f);
            // The resting hand follows an open, readable upper/chest gesture.
            // Attack input below is a fixture, not a second pose recognizer.
            int support=left?16:15;float wave=.5f+.5f*Mathf.Sin(time*3);
            p[support].y=.20f+wave*.20f;p[support].x+=(left?-.10f:.10f)*wave;
            return new PoseFrame{schema=1,streamId="support-review",sequence=frame+1,capturedMs=100000+Mathf.RoundToInt(frame*1000f/hz),tracked=true,points=p};
        }
        static void Run(string version)
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--ranged-support-output");
            string root=at>=0?args[at+1]:Path.GetFullPath(Application.dataPath+"/../../artifacts/ranged-support-20261011");
            foreach(int hz in new[]{15,30,60})
            {
                string folder=$"{root}/{version}/{hz}";Directory.CreateDirectory(folder+"/frames");
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(111);
                var world=new GameWorld();var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                var b=new Battle();b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});b.GiveInstructionTime(30);while(b.TryCue(out _)){}
                var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
                var trace=new StringBuilder("frame,action,age,health,energy,punches,launches,impacts\n");
                var body=new StringBuilder("frame,active,leftX,leftY,leftZ,rightX,rightY,rightZ\n");
                float dt=1f/hz,health=50,maxStep=0,repeat=0;Vector3 oldL=hero.StrikeOrigin(HeroAction.LeftPunch),oldR=hero.HandPosition;bool left=false;
                try
                {
                    for(int f=0;f<hz*5;f++)
                    {
                        float t=f*dt;var input=new PlayerInput{Tracking=true};
                        if(f==Mathf.RoundToInt(.5f*hz)||f==2*hz||f==Mathf.RoundToInt(3.3f*hz))
                        {left=f==2*hz;input.RangedAttack=true;input.LeftPunch=left;input.RightPunch=!left;input.AttackSpeed=f==2*hz?1:f<hz?.65f:1.7f;}
                        b.Tick(dt,input);while(b.TryCue(out var cue))world.Cue(cue,b);
                        var frame=Frame(f,hz,t,left);hero.ObserveReadyPose(frame,frame.capturedMs,input,b,dt,true);
                        hero.Update(b,world.Camera,dt,t);enemy.Update(b,world.Camera,dt,t);
                        if(b.EnemyHealth<health)world.Hit(false,b);health=b.EnemyHealth;world.Tick(b,dt,t);
                        var l=hero.StrikeOrigin(HeroAction.LeftPunch);var r=hero.HandPosition;
                        if(f>3)maxStep=Mathf.Max(maxStep,Vector3.Distance(l,oldL),Vector3.Distance(r,oldR));oldL=l;oldR=r;
                        hero.Update(b,world.Camera,0,t);repeat=Mathf.Max(repeat,Vector3.Distance(l,hero.StrikeOrigin(HeroAction.LeftPunch)),Vector3.Distance(r,hero.HandPosition));
                        trace.AppendLine(FormattableString.Invariant($"{f},{b.Action},{b.ActionAge:F5},{health},{b.Energy},{b.Punches},{world.Projectile.Launches},{world.Projectile.Impacts}"));
                        body.AppendLine(FormattableString.Invariant($"{f},{(b.IsRangedPunch?(left?"L":"R"):"none")},{l.x:F6},{l.y:F6},{l.z:F6},{r.x:F6},{r.y:F6},{r.z:F6}"));
                        if(hz==60&&f%2==0)RangedContactReview.Save(world.Camera,rt,$"{folder}/frames/{f/2:D4}.jpg");
                        if(hz==60&&f==Mathf.RoundToInt(.75f*hz))CharacterReview.Save(world.Camera,rt,folder+"/support.png");
                    }
                    if(b.Punches!=3||world.Projectile.Launches!=3||world.Projectile.Impacts!=3||repeat>.0001f||hz==60&&maxStep>.65f)throw new Exception($"Support lifecycle failed: {hz} step={maxStep} repeat={repeat} hits={b.Punches}");
                    File.WriteAllText(folder+"/trace.csv",trace.ToString());File.WriteAllText(folder+"/body.csv",body.ToString());
                    File.WriteAllText(folder+"/validation.txt",$"{hz} Hz punches={b.Punches} launches=3 impacts=3 maxHandStep={maxStep:F6} repeatError={repeat:F6} passed");
                }
                finally{world.Camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
            }
        }
    }
}
