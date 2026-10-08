using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class ClawArcReview
    {
        public static void Before(){Run("before",60,1,false,true);Run("before",60,5,true,true);}
        public static void After()
        {foreach(int hz in new[]{15,30,60})foreach(int attack in new[]{1,5})foreach(bool block in new[]{false,true})Run("after",hz,attack,block,hz==60&&block==(attack==5));}
        public static void Release(){After();ClawPoseReview.Stability();ExchangeReview.ClawTiming();MonsterAnticipationReview.Validate();}
        static Transform Bone(Transform root,string name)
        {foreach(var bone in root.GetComponentsInChildren<Transform>())if(bone.name==name)return bone;throw new Exception(name);}
        static void Run(string version,int hz,int attack,bool block,bool movie)
        {
            string label=$"{(attack==1?"right":"left")}-{(block?"block":"hurt")}-{hz}";
            string folder=Path.GetFullPath("../artifacts/claw-arc-20261009/"+version+"/"+label);Directory.CreateDirectory(folder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(1056);
            var world=new GameWorld();var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);
            if(!hero.IsRigged||!enemy.IsRigged)throw new Exception("Actual rigs required");world.BindActors(hero,enemy);
            var state=new Battle();state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
            for(int n=0;n<20000;n++)
            {
                if(state.Phase==GamePhase.Battle&&state.Enemy==EnemyPhase.Windup&&state.EnemyAttackCount==attack-1&&state.WarningDuration-state.EnemyAge<1.7f)break;
                state.Tick(1/60f,new PlayerInput{Tracking=true,Shield=true});
            }
            if(state.EnemyAttackCount!=attack-1||state.Enemy!=EnemyPhase.Windup)throw new Exception("Scenario not reached");while(state.TryCue(out _)){}
            float time=0,dt=1f/hz;for(int n=0;n<30;n++){hero.Update(state,world.Camera,1/60f,time);enemy.Update(state,world.Camera,1/60f,time);world.Tick(state,1/60f,time);time+=1/60f;}
            var rt=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
            if(movie)Directory.CreateDirectory(folder+"/frames");
            var hands=new[]{Bone(enemy.Root,"bip_hand_L"),Bone(enemy.Root,"bip_hand_R")};
            var lower=new[]{Bone(enemy.Root,"bip_lowerArm_L"),Bone(enemy.Root,"bip_lowerArm_R")};
            Vector3[] previous={hands[0].position,hands[1].position};
            var axis=-world.BattleAxis;var side=Vector3.Cross(Vector3.up,axis);var mesh=new Mesh();float bend=0,step=0,minY=99,zeroError=0;
            int oldBlocks=state.Blocks,oldHits=state.HitsTaken;var markers=new System.Collections.Generic.HashSet<string>();
            int lead=attack==1?1:0;float loadGap=-99,sweepMin=99,sweepMax=-99;
            var trace=new StringBuilder("frame,phase,age,attack,hero,hits,blocks,health,energy\n");var motion=new StringBuilder("frame,phase,age,hand,side,up,forward\n");
            try
            {
                for(int f=0;f<hz*5;f++)
                {
                    time+=dt;state.Tick(dt,new PlayerInput{Tracking=true,Shield=block});while(state.TryCue(out var cue))world.Cue(cue,state);
                    hero.Update(state,world.Camera,dt,time);enemy.Update(state,world.Camera,dt,time);world.Tick(state,dt,time);
                    trace.AppendLine(FormattableString.Invariant($"{f},{state.Enemy},{state.EnemyAge:F5},{state.EnemyAttackCount},{state.Action},{state.HitsTaken},{state.Blocks},{state.EnemyHealth:F3},{state.Energy:F3}"));
                    for(int h=0;h<2;h++)
                    {
                        var p=hands[h].position;step=Mathf.Max(step,Vector3.Distance(p,previous[h]));previous[h]=p;
                        var relative=p-enemy.Root.position;motion.AppendLine(FormattableString.Invariant($"{f},{state.Enemy},{state.EnemyAge:F5},{h},{Vector3.Dot(relative,side):F4},{relative.y:F4},{Vector3.Dot(relative,axis):F4}"));
                        Vector3 center=Vector3.zero;foreach(var finger in new[]{"index","middle","ring","pinky"})center+=Bone(enemy.Root,$"bip_{finger}_0_{(h==0?"L":"R")}").position;
                        bend=Mathf.Max(bend,Vector3.Angle(center/4-p,p-lower[h].position));
                    }
                    enemy.Update(state,world.Camera,0,time);for(int h=0;h<2;h++)zeroError=Mathf.Max(zeroError,Vector3.Distance(hands[h].position,previous[h]));
                    if(state.Enemy==EnemyPhase.Windup&&state.WarningDuration-state.EnemyAge<.12f)loadGap=hands[lead].position.y-hands[1-lead].position.y;
                    if(state.Enemy==EnemyPhase.Attack&&state.EnemyAge<.64f)
                    {float lane=Vector3.Dot(hands[lead].position-enemy.Root.position,side)*(lead==0?-1:1);sweepMin=Mathf.Min(sweepMin,lane);sweepMax=Mathf.Max(sweepMax,lane);}
                    if(f%3==0)foreach(var skin in enemy.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {skin.BakeMesh(mesh,true);foreach(var v in mesh.vertices)minY=Mathf.Min(minY,skin.transform.TransformPoint(v).y);}
                    string key=null;
                    if(state.Enemy==EnemyPhase.Windup&&state.WarningDuration-state.EnemyAge<.12f)key="load";
                    if(state.Enemy==EnemyPhase.Attack&&state.EnemyAge>=Battle.EnemyHitSeconds)key=state.EnemyAge<.55f?"contact":"follow";
                    if(state.Enemy==EnemyPhase.Recover&&state.EnemyAge>.30f)key="recover";
                    if(movie&&key!=null&&markers.Add(key))CharacterReview.Save(world.Camera,rt,folder+"/"+key+".png");
                    if(movie&&f%2==0)CharacterReview.Save(world.Camera,rt,folder+"/frames/"+(f/2).ToString("D4")+".png");
                }
            }
            finally{world.Camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(mesh);}
            File.WriteAllText(folder+"/trace.csv",trace.ToString());File.WriteAllText(folder+"/motion.csv",motion.ToString());
            var sources=new StringBuilder();foreach(string path in Directory.GetFiles(Application.dataPath+"/Scripts","*.cs",SearchOption.AllDirectories))
                using(var sha=System.Security.Cryptography.SHA256.Create())sources.AppendLine(path.Substring(Application.dataPath.Length+1)+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant());
            File.WriteAllText(folder+"/sources.txt",sources.ToString());
            string report=$"{label} blocks={state.Blocks-oldBlocks} hits={state.HitsTaken-oldHits} bend={bend:F3} handStep={step:F4} ground={minY:F4} zeroError={zeroError:F6} loadGap={loadGap:F4} sweepWidth={sweepMax-sweepMin:F4} crossLane={sweepMin:F4}";
            Debug.Log("[ClawArc] "+report);
            if(state.Blocks-oldBlocks!=(block?1:0)||state.HitsTaken-oldHits!=(block?0:1)||bend>36||zeroError>.0001f||minY<-.07f||hz==60&&step>.50f)throw new Exception(report);
            if(version=="after"&&(loadGap<.20f||sweepMax-sweepMin<.85f||sweepMin>0))throw new Exception("Lead claw did not prepare and sweep across: "+report);
            File.WriteAllText(folder+"/validation.txt",report+" passed\n");
        }
    }
}
