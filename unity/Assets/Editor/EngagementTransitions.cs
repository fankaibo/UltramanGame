using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class EngagementTransitions
    {
        static Transform Bone(Transform root,string name)
        {foreach(var bone in root.GetComponentsInChildren<Transform>())if(bone.name==name)return bone;throw new Exception("Missing "+name);}
        public static void Validate()=>Run(new[]{"Tiga","Mebius","Zero","Geed","Grigio"});
        public static void Tiga()=>Run(new[]{"Tiga"});
        static void Run(string[] heroes)
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--engagement-output");
            string output=at>=0&&at+1<args.Length?args[at+1]:"../artifacts/engagement-20261008";
            var report=new StringBuilder();string folder=Path.GetFullPath(Path.Combine(output,"transitions"));Directory.CreateDirectory(folder);File.Delete(folder+"/validation.txt");
            foreach(string id in heroes)foreach(int hz in new[]{15,30,60})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(1009);
                var world=new GameWorld();var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);world.SetHeroProfile(id);
                var heroChest=id=="Tiga"?Bone(hero.Root,"spineLower"):null;var monsterChest=Bone(enemy.Root,"bip_spine_2");
                float chestGap=100;int closeSamples=0;
                var closeTrace=new StringBuilder("age,heroRoot,heroStance,heroChest,monsterRoot,monsterChest,gap\n");
                var rt=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;var mesh=new Mesh();
                float dt=1f/hz,time=0,minY=10,maxStep=0,health=200;Vector3 previous=hero.Root.position;Battle b=null;
                void Step(PlayerInput input)
                {
                    time+=dt;b.Tick(dt,input);while(b.TryCue(out var cue))world.Cue(cue,b);
                    hero.Update(b,world.Camera,dt,time);enemy.Update(b,world.Camera,dt,time);
                    if(heroChest&&b.Action==HeroAction.Hurt&&b.ActionAge>.05f&&b.ActionAge<.35f)
                    {
                        float gap=Vector3.Distance(heroChest.position,monsterChest.position);closeSamples++;
                        closeTrace.AppendLine($"{b.ActionAge:F4},\"{hero.Root.position:F4}\",\"{hero.StancePosition:F4}\",\"{heroChest.position:F4}\",\"{enemy.Root.position:F4}\",\"{monsterChest.position:F4}\",{gap:F4}");
                        if(gap<chestGap&&gap<.8f)CharacterReview.Save(world.Camera,rt,folder+"/"+id+"-"+hz+"-body-intersection.png");
                        chestGap=Mathf.Min(chestGap,gap);
                    }
                    if(b.EnemyHealth<health)world.Hit(b.Action==HeroAction.Beam,b);health=b.EnemyHealth;world.Tick(b,dt,time);
                    maxStep=Mathf.Max(maxStep,Vector3.Distance(previous,hero.Root.position));previous=hero.Root.position;
                }
                void Idle(float seconds,bool shield=false){for(int f=0;f<(int)Math.Ceiling(seconds/dt);f++)Step(new PlayerInput{Tracking=true,Shield=shield});}
                void Start()
                {
                    b=new Battle(200);b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});while(b.TryCue(out _)){}health=200;world.ResetPresentation();Idle(.15f);maxStep=0;
                }
                void CheckFloor()
                {foreach(var skin in hero.Root.GetComponentsInChildren<SkinnedMeshRenderer>()){skin.BakeMesh(mesh,true);foreach(var v in mesh.vertices)minY=Mathf.Min(minY,skin.transform.TransformPoint(v).y);}}
                void Save(string key){if(hz==60)CharacterReview.Save(world.Camera,rt,folder+"/"+id+"-"+key+".png");}
                try
                {
                    Start();b.GiveInstructionTime(20);Step(new PlayerInput{Tracking=true,LeftPunch=true});Idle(.22f);
                    Idle(.12f,true);Save("guard-retreat");Idle(1,true);CheckFloor();
                    if(!b.Shield||hero.EngagementWeight>.01f)throw new Exception("Guard failed to regain distance");
                    Start();b.GiveInstructionTime(20);Step(new PlayerInput{Tracking=true,LeftPunch=true});Idle(.43f);Step(new PlayerInput{Tracking=true,RightPunch=true,RangedAttack=true});Idle(.24f);Save("ranged-retreat");Idle(1);CheckFloor();
                    if(b.Punches!=2||hero.EngagementWeight>.01f)throw new Exception("Melee to ranged continuity failed");
                    Start();
                    for(int i=0;i<15;i++){Step(new PlayerInput{Tracking=true,LeftPunch=true});Idle(.47f);}
                    Step(new PlayerInput{Tracking=true,Beam=true});Idle(.22f);Save("close-beam");
                    if(b.Action!=HeroAction.Beam||hero.EngagementWeight<.99f)throw new Exception("Beam teleported home");Idle(3);CheckFloor();
                    Start();
                    for(int i=0;i<hz*30&&b.Enemy!=EnemyPhase.Attack;i++)Step(new PlayerInput{Tracking=true});
                    Step(new PlayerInput{Tracking=true,LeftPunch=true});
                    for(int i=0;i<hz*3&&b.Action!=HeroAction.Hurt;i++)Step(new PlayerInput{Tracking=true});
                    if(b.Action!=HeroAction.Hurt||hero.EngagementWeight<.9f)throw new Exception("Near-contact hurt was not exercised");
                    Vector3 anchor=hero.StancePosition;Idle(.25f);Save("near-fall");CheckFloor();Idle(.55f);Save("near-land");CheckFloor();
                    if(Vector3.Distance(anchor,hero.StancePosition)>.01f)throw new Exception("Fall anchor moved during knockdown");Idle(4);CheckFloor();
                    Start();b.GiveInstructionTime(20);Step(new PlayerInput{Tracking=true,RightPunch=true});Idle(.44f);Vector3 near=hero.StancePosition;b.Pause();hero.Update(b,world.Camera,dt,time);
                    if(Vector3.Distance(near,hero.StancePosition)>.001f)throw new Exception("Pause snapped the stance");Idle(2);CheckFloor();
                    var fresh=new Battle();hero.Update(fresh,world.Camera,0,0);if(hero.EngagementWeight!=0||Vector3.Distance(hero.StancePosition,world.HeroHome)>.001f)throw new Exception("New round retained approach");
                    if(minY<-.07f)throw new Exception($"Ground intersection {id}-{hz}: {minY}");
                    File.WriteAllText(folder+"/"+id+"-"+hz+"-close.csv",closeTrace.ToString());
                    if(heroChest&&(closeSamples==0||chestGap<.80f))throw new Exception($"Close-range bodies overlap: samples={closeSamples} chestGap={chestGap:F4}");
                    string line=$"{id}-{hz} guard/ranged/beam/hurt/pause/newRound=pass ground={minY:F4} chestGap={chestGap:F4} closeSamples={closeSamples}";report.AppendLine(line);Debug.Log("[EngagementTransitions] "+line);
                }
                finally{world.Camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(mesh);}
            }
            File.WriteAllText(folder+"/validation.txt",report.ToString());
        }
    }
}
