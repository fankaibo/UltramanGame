using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class ReadyArmsReview
    {
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/ready-arms-20261009"));
        public static void Before()=>Run("before");
        public static void After()=>Run("after");
        public static void Regression(){RosterPunchReview.CheckGuardTransitions();SurfaceImpactReview.CheckPunchRecovery();}
        static Transform Bone(Transform root,string name)
        {foreach(var bone in root.GetComponentsInChildren<Transform>())if(bone.name==name)return bone;throw new Exception(name);}
        static void Run(string version)
        {
            string folder=Folder+"/"+version;Directory.CreateDirectory(folder+"/frames");
            File.Delete(folder+"/validation.txt");var metrics=new StringBuilder();var trace=new StringBuilder("hz,frame,action,shield,punches,health,energy\n");
            foreach(int hz in new[]{15,30,60})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(100965);
                var world=new GameWorld();var b=new Battle();var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);
                var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});
                b.GiveInstructionTime(60);while(b.TryCue(out _)){}
                var u=new[]{Bone(hero.Root,"armBase_L"),Bone(hero.Root,"armBase_R")};
                var w=new[]{Bone(hero.Root,"HandBase_L"),Bone(hero.Root,"HandBase_R")};
                // The wrist has a twist parent. Measure the elbow joint that
                // actually drives IK, not the intervening wrist roll helper.
                var l=new[]{Bone(hero.Root,"ForearmBase_L"),Bone(hero.Root,"ForearmBase_R")};
                var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
                float dt=1f/hz;var old=new Vector3[2];float maxStep=0;
                try
                {
                    for(int f=0;f<hz*6;f++)
                    {
                        var input=new PlayerInput{Tracking=true,LeftPunch=f==Mathf.RoundToInt(hz*1.2f),RightPunch=f==Mathf.RoundToInt(hz*3.9f),Shield=f>=Mathf.RoundToInt(hz*2.4f)&&f<Mathf.RoundToInt(hz*3.1f)};
                        b.Tick(dt,input);while(b.TryCue(out var cue))world.Cue(cue,b);
                        hero.Update(b,world.Camera,dt,f*dt);enemy.Update(b,world.Camera,dt,f*dt);world.Tick(b,dt,f*dt);
                        for(int i=0;i<2;i++)
                        {
                            var local=hero.Root.InverseTransformPoint(w[i].position);
                            if(f>0)maxStep=Mathf.Max(maxStep,Vector3.Distance(local,old[i]));old[i]=local;
                            if(f==Mathf.RoundToInt(hz*.8f)||f==Mathf.RoundToInt(hz*5.5f))
                            {
                                float span=Vector3.Distance(u[i].position,l[i].position)+Vector3.Distance(l[i].position,w[i].position);
                                float reach=Vector3.Distance(u[i].position,w[i].position)/span;
                                float angle=Vector3.Angle(u[i].position-l[i].position,w[i].position-l[i].position);
                                metrics.AppendLine(FormattableString.Invariant($"hz={hz} frame={f} hand={i} shoulderReach={reach:F4} elbowAngle={angle:F2}"));
                                if(version=="after"&&(reach<.50f||reach>.92f||angle<65||angle>150))throw new Exception("Ready arm cramped or locked: "+metrics);
                            }
                        }
                        trace.AppendLine($"{hz},{f},{b.Action},{b.Shield},{b.Punches},{b.EnemyHealth},{b.Energy}");
                        if(hz==60&&f%2==0)CharacterReview.Save(world.Camera,rt,$"{folder}/frames/{f/2:D4}.png");
                        if(hz==60&&(f==48||f==165||f==330))CharacterReview.Save(world.Camera,rt,$"{folder}/pose-{f}.png");
                    }
                    if(b.Punches!=2||b.EnemyHealth!=48||b.Energy!=2)throw new Exception("Ready pose changed battle rules");
                    var left=w[0].position;var right=w[1].position;hero.Update(b,world.Camera,0,(hz*6-1)*dt);
                    if(Vector3.Distance(left,w[0].position)>.0001f||Vector3.Distance(right,w[1].position)>.0001f)throw new Exception("Repeated pose accumulates");
                    metrics.AppendLine($"{hz}Hz maxLocalHandStep={maxStep:F5} repeat=passed hits=2 health=48");
                }
                finally{world.Camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
            }
            File.WriteAllText(folder+"/trace.csv",trace.ToString());File.WriteAllText(folder+"/validation.txt",metrics.ToString());
            using(var sha=System.Security.Cryptography.SHA256.Create())
                File.WriteAllText(folder+"/actor-sha256.txt",BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Application.dataPath+"/Scripts/Runtime/RiggedActor.cs"))).Replace("-","").ToLowerInvariant());
            Debug.Log("[ReadyArmsReview] "+version+" passed\n"+metrics);
        }
    }
}
