using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class PausedReadyReview
    {
        public static void Before()=>Run(false);
        public static void After()=>Run(true);
        static Transform Bone(Transform root,string a,string b)=>root.GetComponentsInChildren<Transform>().Single(t=>t.name==a||t.name==b);
        static void Run(bool check)
        {
            string folder=Path.GetFullPath("../artifacts/paused-ready-20261010/"+(check?"after":"before"));
            Directory.CreateDirectory(folder);var report=new StringBuilder();var trace=new StringBuilder();
            foreach(string id in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})foreach(int hz in new[]{15,30,60})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(101076);
                var world=new GameWorld();var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);
                var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                var upper=new[]{Bone(hero.Root,"armBase_L","bip_upperArm_L"),Bone(hero.Root,"armBase_R","bip_upperArm_R")};
                var lower=new[]{Bone(hero.Root,"ForearmBase_L","bip_lowerArm_L"),Bone(hero.Root,"ForearmBase_R","bip_lowerArm_R")};
                var wrist=new[]{Bone(hero.Root,"HandBase_L","bip_hand_L"),Bone(hero.Root,"HandBase_R","bip_hand_R")};
                var state=new Battle();state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
                while(state.Phase!=GamePhase.Battle)state.Tick(.02f,new PlayerInput{Tracking=true});
                state.GiveInstructionTime(20);while(state.TryCue(out _)){}
                float dt=1f/hz,time=0,maxStep=0;var old=new Vector3[2];
                void Step(bool tracking)
                {
                    state.Tick(dt,new PlayerInput{Tracking=tracking});while(state.TryCue(out _)){}
                    time+=dt;hero.Update(state,world.Camera,dt,time);enemy.Update(state,world.Camera,dt,time);world.Tick(state,dt,time);
                    for(int i=0;i<2;i++)
                    {var p=hero.Root.InverseTransformPoint(wrist[i].position);if(time>dt*2)maxStep=Mathf.Max(maxStep,Vector3.Distance(p,old[i]));old[i]=p;}
                    trace.AppendLine($"{id},{hz},{state.Phase},{state.Action},{state.Shield},{state.Punches},{state.EnemyHealth},{state.Energy}");
                }
                var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
                void Measure(string stage)
                {
                    for(int i=0;i<2;i++)
                    {
                        float span=Vector3.Distance(upper[i].position,lower[i].position)+Vector3.Distance(lower[i].position,wrist[i].position);
                        float reach=Vector3.Distance(upper[i].position,wrist[i].position)/span;
                        float angle=Vector3.Angle(upper[i].position-lower[i].position,wrist[i].position-lower[i].position);
                        report.AppendLine(FormattableString.Invariant($"{id} {hz}Hz {stage} hand={i} reach={reach:F4} elbow={angle:F2}"));
                        if(check&&(reach<.80f||reach>.94f||angle<100||angle>150))throw new Exception("Folded/locked ready arm: "+report);
                    }
                    if(hz==60)CharacterReview.Save(world.Camera,rt,$"{folder}/{id}-{stage}.png");
                }
                try
                {
                    for(int f=0;f<hz;f++)Step(true);Measure("battle");
                    for(int f=0;f<hz;f++)Step(false);Measure("paused");
                    if(state.Phase!=GamePhase.Paused)throw new Exception("Pause not reached");
                    var frozen=wrist.Select(t=>t.position).ToArray();hero.Update(state,world.Camera,0,time);
                    for(int i=0;i<2;i++)if(Vector3.Distance(frozen[i],wrist[i].position)>.0001f)throw new Exception("Repeat drift");
                    for(int f=0;f<hz*2;f++)Step(true);Measure("resumed");
                    if(state.Phase!=GamePhase.Battle||state.Punches!=0||state.EnemyHealth!=50||state.Energy!=0)throw new Exception("Pause changed battle state");
                    if(check&&maxStep>.08f)throw new Exception("Ready hand discontinuity: "+id+" "+maxStep);
                    report.AppendLine($"{id} {hz}Hz maxLocalStep={maxStep:F5} repeat=passed resume=passed");
                }
                finally{world.Camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
            }
            File.WriteAllText(folder+"/metrics.txt",report.ToString());File.WriteAllText(folder+"/trace.csv",trace.ToString());
            if(check&&File.ReadAllText(Path.Combine(folder,"../before/trace.csv"))!=trace.ToString())throw new Exception("Battle trace changed");
            Debug.Log("[PausedReadyReview] PASS "+folder+"\n"+report);
        }
    }
}
