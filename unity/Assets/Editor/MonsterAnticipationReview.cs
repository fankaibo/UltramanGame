using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class MonsterAnticipationReview
    {
        public static void Release(){ThreatCameraReview.After();Validate();}
        static Transform Bone(Transform root,string name)
        {foreach(var b in root.GetComponentsInChildren<Transform>())if(b.name==name)return b;throw new Exception(name);}
        static Battle Ready()
        {
            var s=new Battle();s.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
            for(int i=0;i<1000;i++){s.Tick(.02f,new PlayerInput{Tracking=true});if(s.Enemy==EnemyPhase.Windup)break;}
            while(s.TryCue(out _)){}return s;
        }
        public static void Validate()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/monster-anticipation"));Directory.CreateDirectory(folder);File.Delete(folder+"/flow-validation.txt");var report=new StringBuilder();
            foreach(int rate in new[]{15,30,60})foreach(bool extended in new[]{false,true})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var world=new GameWorld();var state=Ready();var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);
                var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                var left=Bone(enemy.Root,"bip_hand_L");var right=Bone(enemy.Root,"bip_hand_R");var chest=Bone(enemy.Root,"bip_spine_2");
                Transform[] hands={left,right},lower={Bone(enemy.Root,"bip_lowerArm_L"),Bone(enemy.Root,"bip_lowerArm_R")};
                var finger=new Transform[2,4];string[] names={"index","middle","ring","pinky"};
                for(int side=0;side<2;side++)for(int i=0;i<4;i++)finger[side,i]=Bone(enemy.Root,"bip_"+names[i]+"_0_"+(side==0?"L":"R"));
                Vector3[] anchor={enemy.FootPosition(true),enemy.FootPosition(false)},prior={left.position,right.position};
                float maxWrist=0,footDrift=0,maxStep=0,minGround=100,chestMotion=0,repeatError=0;int extensions=0,samples=0;Quaternion firstChest=Quaternion.identity;string stepAt="";
                var mesh=new Mesh();float dt=1f/rate;
                try
                {
                    for(int f=0;f<rate*40;f++)
                    {
                        float time=f*dt;
                        if(extended&&state.Enemy==EnemyPhase.Windup&&state.EnemyAge>state.WarningDuration-.35f&&extensions<state.EnemyAttackCount+1)
                        {state.GiveInstructionTime(2,true);extensions++;}
                        state.Tick(world.BattleDelta(dt,state),new PlayerInput{Tracking=true,Shield=true});while(state.TryCue(out var cue))world.Cue(cue,state);
                        hero.Update(state,world.Camera,dt,time);enemy.Update(state,world.Camera,dt,time);world.Tick(state,dt,time);
                        for(int side=0;side<2;side++)
                        {
                            Vector3 center=Vector3.zero;for(int i=0;i<4;i++)center+=finger[side,i].position;center/=4;
                            maxWrist=Mathf.Max(maxWrist,Vector3.Angle(center-hands[side].position,hands[side].position-lower[side].position));
                            float step=Vector3.Distance(prior[side],hands[side].position);
                            if(f>0&&step>maxStep){maxStep=step;stepAt=$"{state.Enemy}/{state.EnemyAttackCount}/{state.EnemyAge:F3}/hand{side}";}prior[side]=hands[side].position;
                        }
                        if(state.Enemy==EnemyPhase.Windup&&state.EnemyAge>.5f)
                        {
                            for(int side=0;side<2;side++)footDrift=Mathf.Max(footDrift,Vector3.ProjectOnPlane(enemy.FootPosition(side==0)-anchor[side],Vector3.up).magnitude);
                            if(state.EnemyAge<3.2f&&state.EnemyAttackCount==0)
                            {if(samples++==0)firstChest=chest.rotation;chestMotion=Mathf.Max(chestMotion,Quaternion.Angle(firstChest,chest.rotation));}
                            Vector3 beforeLeft=left.position,beforeRight=right.position;enemy.Update(state,world.Camera,0,time);
                            repeatError=Mathf.Max(repeatError,Vector3.Distance(beforeLeft,left.position),Vector3.Distance(beforeRight,right.position));
                        }
                        if(f%Math.Max(1,rate/10)==0&&state.Enemy==EnemyPhase.Windup)
                            foreach(var skin in enemy.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                            {skin.BakeMesh(mesh,true);foreach(var v in mesh.vertices)minGround=Mathf.Min(minGround,skin.transform.TransformPoint(v).y);}
                        if(state.Blocks==2&&state.Enemy==EnemyPhase.Recover&&state.EnemyAge>.5f)break;
                    }
                    string result=$"{rate}Hz extended={extended} blocks={state.Blocks} extensions={extensions} wrist={maxWrist:F3} footDrift={footDrift:F4} handStep={maxStep:F4} stepAt={stepAt} minGround={minGround:F4} chestMotion={chestMotion:F3} repeatError={repeatError:F5}";
                    Debug.Log("[MonsterAnticipation] "+result);
                    if(state.Blocks!=2||state.HitsTaken!=0||state.EnemyHealth!=50||maxWrist>36||footDrift>.035f||maxStep>.70f||minGround<-.035f||chestMotion<5||repeatError>.015f||(extended&&extensions!=2))throw new Exception(result);
                    report.AppendLine(result+" passed");
                    // A new round and pause must not retain the old coil layer.
                    state=Ready();for(int f=0;f<rate*2;f++){state.Tick(dt,new PlayerInput{Tracking=true});enemy.Update(state,world.Camera,dt,f*dt);}
                    state.Pause();for(int f=0;f<rate*2;f++)enemy.Update(state,world.Camera,dt,4+f*dt);
                    if(Vector3.Distance(enemy.Root.position,world.EnemyHome)>.001f)throw new Exception("Pause retained anticipation offset");
                    var waiting=new Battle();var fresh=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);
                    for(int f=0;f<rate;f++){enemy.Update(waiting,world.Camera,dt,6+f*dt);fresh.Update(waiting,world.Camera,dt,6+f*dt);}
                    if(Vector3.Distance(enemy.HandPosition,fresh.HandPosition)>.001f||Vector3.Distance(enemy.StrikeOrigin(HeroAction.LeftPunch),fresh.StrikeOrigin(HeroAction.LeftPunch))>.001f)throw new Exception("New round retained anticipation pose");
                    report.AppendLine($"{rate}Hz extended={extended} pause and newRound passed");
                }
                finally{UnityEngine.Object.DestroyImmediate(mesh);}
            }
            foreach(float punchAt in new[]{.8f,1.8f,4.5f})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var world=new GameWorld();var state=Ready();var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);
                var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                bool punched=false;float maxStep=0,health=state.EnemyHealth;Vector3 prior=enemy.HandPosition;
                for(int f=0;f<900;f++)
                {
                    const float dt=1/60f;bool punch=!punched&&state.EnemyAge>=punchAt;punched|=punch;
                    state.Tick(world.BattleDelta(dt,state),new PlayerInput{Tracking=true,LeftPunch=punch,Shield=punched&&state.Action==HeroAction.None&&!punch});
                    while(state.TryCue(out var cue))world.Cue(cue,state);
                    hero.Update(state,world.Camera,dt,f*dt);enemy.Update(state,world.Camera,dt,f*dt);
                    if(state.EnemyHealth<health)world.Hit(false,state);health=state.EnemyHealth;world.Tick(state,dt,f*dt);
                    if(f>0)maxStep=Mathf.Max(maxStep,Vector3.Distance(prior,enemy.HandPosition));prior=enemy.HandPosition;
                    if(state.Blocks==1&&state.Enemy==EnemyPhase.Recover&&state.EnemyAge>.5f)break;
                }
                if(state.Punches!=1||state.EnemyHealth!=49||state.Energy!=1||state.Blocks!=1||state.HitsTaken!=0||maxStep>.7f)
                    throw new Exception($"Hit during warning failed at {punchAt}: step={maxStep} punches={state.Punches} blocks={state.Blocks}");
                report.AppendLine($"punchAt={punchAt:F1} handStep={maxStep:F4} hurt-to-warning-and-block passed");
            }
            File.WriteAllText(folder+"/flow-validation.txt",report.ToString());
        }
    }
}
