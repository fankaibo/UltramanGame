using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor.SceneManagement;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class RangedFootworkReview
    {
        public static void Before()=>Run("before");
        public static void After(){Run("after");Run("guard-after",true);}
        static void Run(string version,bool guard=false)
        {
            string root=Path.GetFullPath(Application.dataPath+"/../../artifacts/ranged-footwork-20261011");
            foreach(int hz in new[]{15,30,60})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(112);
                var world=new GameWorld();var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                Transform Bone(string name){foreach(var t in hero.Root.GetComponentsInChildren<Transform>())if(t.name==name)return t;throw new Exception(name);}
                var lf=Bone("Foot_L");var rf=Bone("Foot_R");
                var b=new Battle();b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});b.GiveInstructionTime(30);while(b.TryCue(out _)){}
                string folder=$"{root}/{version}/{hz}";Directory.CreateDirectory(folder+"/frames");
                var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
                var trace=new StringBuilder("frame,action,age,health,energy,punches,launches,impacts\n");
                var feet=new StringBuilder("frame,action,age,leftX,leftY,leftZ,rightX,rightY,rightZ,leftQx,leftQy,leftQz,leftQw,rightQx,rightQy,rightQz,rightQw,leftHandX,leftHandY,leftHandZ,rightHandX,rightHandY,rightHandZ,ground\n");
                float dt=1f/hz,health=50,maxStep=0,minGround=100,repeat=0;Vector3 oldL=lf.position,oldR=rf.position;var mesh=new Mesh();
                try
                {
                    for(int f=0;f<hz*5;f++)
                    {
                        float t=f*dt;var input=new PlayerInput{Tracking=true};
                        if(f==Mathf.RoundToInt(.5f*hz)||f==2*hz||f==Mathf.RoundToInt(3.3f*hz))
                        {input.RangedAttack=true;input.LeftPunch=f==2*hz;input.RightPunch=!input.LeftPunch;input.AttackSpeed=f==2*hz?1:f<hz?.65f:1.7f;}
                        if(guard)foreach(float start in new[]{.5f,2,3.3f})if(t>=start+.24f&&t<start+.8f)input.GuardIntent=input.Shield=true;
                        b.Tick(dt,input);while(b.TryCue(out var cue))world.Cue(cue,b);hero.Update(b,world.Camera,dt,t);enemy.Update(b,world.Camera,dt,t);
                        if(b.EnemyHealth<health)world.Hit(false,b);health=b.EnemyHealth;world.Tick(b,dt,t);
                        var l=lf.position;var r=rf.position;var lq=lf.rotation;var rq=rf.rotation;var lh=hero.StrikeOrigin(HeroAction.LeftPunch);var rh=hero.HandPosition;
                        if(f>3)maxStep=Mathf.Max(maxStep,Vector3.Distance(l,oldL),Vector3.Distance(r,oldR));oldL=l;oldR=r;
                        float ground=100;
                        foreach(var skin in hero.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {skin.BakeMesh(mesh,true);foreach(var v in mesh.vertices)ground=Mathf.Min(ground,skin.transform.TransformPoint(v).y);}
                        minGround=Mathf.Min(minGround,ground);
                        hero.Update(b,world.Camera,0,t);repeat=Mathf.Max(repeat,Vector3.Distance(l,lf.position),Vector3.Distance(r,rf.position));
                        trace.AppendLine(FormattableString.Invariant($"{f},{b.Action},{b.ActionAge:F5},{health},{b.Energy},{b.Punches},{world.Projectile.Launches},{world.Projectile.Impacts}"));
                        feet.AppendLine(FormattableString.Invariant($"{f},{b.Action},{b.ActionAge:F5},{l.x:F6},{l.y:F6},{l.z:F6},{r.x:F6},{r.y:F6},{r.z:F6},{lq.x:F6},{lq.y:F6},{lq.z:F6},{lq.w:F6},{rq.x:F6},{rq.y:F6},{rq.z:F6},{rq.w:F6},{lh.x:F6},{lh.y:F6},{lh.z:F6},{rh.x:F6},{rh.y:F6},{rh.z:F6},{ground:F6}"));
                        if(hz==60&&f%2==0)RangedContactReview.Save(world.Camera,rt,$"{folder}/frames/{f/2:D4}.jpg");
                        if(hz==60&&f==46)CharacterReview.Save(world.Camera,rt,folder+"/release.png");
                    }
                    File.WriteAllText(folder+"/trace.csv",trace.ToString());File.WriteAllText(folder+"/feet.csv",feet.ToString());
                    string report=$"{hz}Hz hits={b.Punches} maxFootStep={maxStep:F6} ground={minGround:F6} repeatError={repeat:F6}";File.WriteAllText(folder+"/validation.txt",report);Debug.Log("[RangedFootwork] "+report);
                    if(b.Punches!=3||world.Projectile.Launches!=3||world.Projectile.Impacts!=3||repeat>.0001f||minGround<-.05f||hz==60&&maxStep>.2f)throw new Exception(report);
                }
                finally{world.Camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(mesh);}
            }
        }
    }
}
