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
    public static class BattleVarietyReview
    {
        static readonly string Folder=Path.GetFullPath("../artifacts/battle-variety-20261008");
        static StringBuilder report=new StringBuilder();
        static GameWorld world;static AnimatedActor hero,enemy;static Battle state;static RenderTexture rt;
        static float clock;static float health;
        static void Setup(string name)
        {
            if(rt){world.Camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(1008);
            world=new GameWorld();hero=new AnimatedActor(name,world.HeroHome,world.EnemyHome);enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);
            if(!hero.IsRigged||!enemy.IsRigged)throw new Exception("Actual rig missing");
            world.BindActors(hero,enemy);world.SetHeroProfile(name);state=new Battle();clock=0;health=state.EnemyHealth;
            state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(state.Phase!=GamePhase.Battle)state.Tick(.02f,new PlayerInput{Tracking=true});
            while(state.TryCue(out _)){}
            rt=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
            for(int n=0;n<45;n++)Frame(new PlayerInput{Tracking=true});
            if(Mathf.Abs(Vector3.Distance(world.HeroHome,world.EnemyHome)-CombatSpacing.StandingDistance)>.01f)throw new Exception("Standing distance mismatch");
        }
        static void Frame(PlayerInput input)
        {
            const float dt=1/60f;clock+=dt;state.Tick(dt,input);
            while(state.TryCue(out var cue))world.Cue(cue,state);
            hero.Update(state,world.Camera,dt,clock);enemy.Update(state,world.Camera,dt,clock);
            if(state.EnemyHealth<health)world.Hit(false,state);health=state.EnemyHealth;
            world.Tick(state,dt,clock);
        }
        static void Save(string name){CharacterReview.Save(world.Camera,rt,Folder+"/"+name+".png");}
        static void Require(bool pass,string line){report.AppendLine(line);Debug.Log("[BattleVariety] "+line);if(!pass)throw new Exception(line);}
        public static void Render()
        {
            Directory.CreateDirectory(Folder);File.Delete(Folder+"/validation.txt");
            var sources=new StringBuilder();foreach(string path in Directory.GetFiles(Application.dataPath+"/Scripts","*.cs",SearchOption.AllDirectories))
                using(var sha=SHA256.Create())sources.AppendLine(path.Substring(Application.dataPath.Length+1)+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant());
            File.WriteAllText(Folder+"/render-source.txt",sources.ToString());
            foreach(string name in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})
            {
                Setup(name);Save(name+"-spacing");state.GiveInstructionTime(20);
                string frames=Folder+"/"+name+"-melee";Directory.CreateDirectory(frames);float gap=99,minY=99;var mesh=new Mesh();
                for(int f=0;f<75;f++)
                {
                    Frame(new PlayerInput{Tracking=true,LeftPunch=f==10,AttackSpeed=.8f});
                    if(state.Punches==1&&gap==99)
                    {gap=Vector3.Distance(name=="Mebius"?world.Blade.Tip:hero.StrikeOrigin(HeroAction.LeftPunch),world.BeamTarget);Save(name+"-contact");}
                    if(f%5==0&&state.IsPunch)foreach(var skin in hero.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {skin.BakeMesh(mesh,true);foreach(var v in mesh.vertices)minY=Mathf.Min(minY,skin.transform.TransformPoint(v).y);}
                    if(name=="Mebius"&&f%2==0)Save(name+"-melee/"+(f/2).ToString("D4"));
                }
                UnityEngine.Object.DestroyImmediate(mesh);
                Require(state.Punches==1&&minY>-.07f&&gap<.55f,$"{name} melee hits={state.Punches} surfaceGap={gap:F3} ground={minY:F3}");
                float kickGap=99;
                for(int f=0;f<175;f++)
                {
                    Frame(new PlayerInput{Tracking=true,LeftPunch=f%40==0&&f<=120});
                    if(state.Punches==5&&kickGap==99)
                    {kickGap=Vector3.Distance(hero.StrikeContact(state),world.BeamTarget-Vector3.up*.64f);Save(name+"-kick");}
                }
                Require(state.Punches==5&&kickGap<.65f,$"{name} fifth strike kickGap={kickGap:F3}");
                if(name!="Zero")continue;
                state=new Battle();state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(state.Phase!=GamePhase.Battle)state.Tick(.02f,new PlayerInput{Tracking=true});state.GiveInstructionTime(20);while(state.TryCue(out _)){}health=state.EnemyHealth;world.ResetPresentation();
                Directory.CreateDirectory(Folder+"/Zero-sluggers");bool outward=false,back=false;
                for(int f=0;f<75;f++)
                {
                    Frame(new PlayerInput{Tracking=true,RightPunch=f==10,RangedAttack=true,AttackSpeed=.65f});
                    if(world.Projectile.Visible&&state.Shot.Age>.20f&&state.Shot.Age<.28f&&!outward){outward=true;Save("Zero-outbound");}
                    if(world.Projectile.Visible&&state.Shot.Age>.34f&&!back){back=true;Save("Zero-return");}
                    if(f%2==0)Save("Zero-sluggers/"+(f/2).ToString("D4"));
                }
                Require(outward&&back&&state.Punches==1&&world.Projectile.Launches==1,$"Zero sluggers outbound={outward} return={back} hits={state.Punches} launches={world.Projectile.Launches}");
            }
            foreach(bool shield in new[]{true,false})
            {
                Setup("Tiga");int n=0;
                while(!(state.Enemy==EnemyPhase.Windup&&state.EnemyAttackCount==1&&state.WarningDuration-state.EnemyAge<1.5f)&&n++<3000)Frame(new PlayerInput{Tracking=true,Shield=true});
                string name=shield?"rock-block":"rock-hurt";Directory.CreateDirectory(Folder+"/"+name);
                int hits=state.HitsTaken,blocks=state.Blocks;bool held=false,flight=false,fragments=false;
                for(int f=0;f<210;f++)
                {
                    Frame(new PlayerInput{Tracking=true,Shield=shield});
                    if(world.Rock.Holding&&!held&&state.WarningDuration-state.EnemyAge<.35f){held=true;Save(name+"-hold");}
                    if(world.Rock.Flying&&!flight&&state.EnemyAge>.45f){flight=true;Save(name+"-flight");}
                    if(world.Rock.Fragments&&!fragments){fragments=true;Save(name+"-impact");}
                    if(world.Rock.Flying&&state.EnemyAge<MonsterRockMotion.Contact)Require(state.HitsTaken==hits&&state.Blocks==blocks,"rock precontact age="+state.EnemyAge.ToString("F3"));
                    if(f%2==0)Save(name+"/"+(f/2).ToString("D4"));
                }
                Require(held&&flight&&fragments&&state.HitsTaken==hits+(shield?0:1)&&state.Blocks==blocks+(shield?1:0),$"{name} held={held} flight={flight} fragments={fragments} hits={state.HitsTaken-hits} blocks={state.Blocks-blocks} launches={world.Rock.Launches}");
                if(shield)
                {
                    for(int i=0;i<4000&&!(state.EnemyAttackCount==5&&state.Enemy==EnemyPhase.Attack&&state.EnemyAge>=.40f);i++)Frame(new PlayerInput{Tracking=true,Shield=true});
                    Save("monster-left-claw");
                    Require(state.EnemyAttackCount==5&&state.Blocks==5,"fifth attack left claw blocks="+state.Blocks);
                }
                state.Pause();Frame(new PlayerInput{Tracking=true});Require(!world.Rock.Flying&&!world.Rock.Fragments&&!world.Blade.Visible,"pause clears weapons");
            }
            File.WriteAllText(Folder+"/validation.txt",report.ToString());
        }
    }
}
