using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class MonsterBackstepReview
    {
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/monster-backstep"));
        public static void Release(){ComboStrikeReview.After();Validate();Interruptions();StaggerReview.CheckRecovery();ComboStrikeReview.Validate();ComboStrikeReview.Interruptions();}
        static Battle Ready(int punches=0,bool hold=true)
        {
            var state=new Battle();state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
            for(int i=0;i<120;i++)state.Tick(.02f,new PlayerInput{Tracking=true});
            for(int n=0;n<punches;n++){state.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});for(int i=0;i<25;i++)state.Tick(.02f,new PlayerInput{Tracking=true});}
            if(hold)state.GiveInstructionTime(20);while(state.TryCue(out _)){}return state;
        }
        public static void Validate()
        {
            Directory.CreateDirectory(Folder+"/validation");File.Delete(Folder+"/step-validation.txt");var report=new StringBuilder();
            foreach(string id in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})foreach(int rate in id=="Tiga"?new[]{15,30,60}:new[]{60})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(930);
                var world=new GameWorld();var state=Ready();var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,1,0);
                Vector3 homeL=enemy.FootPosition(true),homeR=enemy.FootPosition(false),oldL=homeL,oldR=homeR,planted=Vector3.zero;
                float support=0,lift=0,back=0,slide=0,minY=10,maxStep=0,gap=0;int contacts=0,landings=0;float health=state.EnemyHealth,dt=1f/rate,time=0;bool landing=false;
                var mesh=new Mesh();var target=new RenderTexture(1280,720,24){antiAliasing=4};target.Create();world.Camera.targetTexture=target;world.Camera.aspect=16f/9;
                try
                {
                    for(int n=1;n<=10;n++)for(int f=0;f<rate;f++)
                    {
                        state.Tick(dt,new PlayerInput{Tracking=true,LeftPunch=f==0&&n%2==1,RightPunch=f==0&&n%2==0});while(state.TryCue(out var cue))world.Cue(cue,state);
                        hero.Update(state,world.Camera,dt,time);enemy.Update(state,world.Camera,dt,time);world.Tick(state,dt,time);time+=dt;
                        if(world.MonsterStaggerLanded)landings++;
                        var l=enemy.FootPosition(true);var r=enemy.FootPosition(false);
                        maxStep=Mathf.Max(maxStep,Vector3.Distance(l,oldL),Vector3.Distance(r,oldR));oldL=l;oldR=r;
                        if(enemy.StaggerAge<MonsterStaggerMotion.Duration)
                        {
                            var moving=enemy.StaggerLeft?l:r;var rest=enemy.StaggerLeft?homeL:homeR;
                            support=Mathf.Max(support,Vector3.Distance(enemy.StaggerLeft?r:l,enemy.StaggerLeft?homeR:homeL));
                            lift=Mathf.Max(lift,moving.y-rest.y);back=Mathf.Max(back,Vector3.Dot(moving-rest,world.BattleAxis));
                            if(enemy.StaggerAge>=MonsterStaggerMotion.Landing&&enemy.StaggerAge<=MonsterStaggerMotion.Return)
                            {
                                if(!landing){planted=moving;landing=true;CharacterReview.Save(world.Camera,target,$"{Folder}/validation/{id}-{rate}-landing-{n}.png");}
                                slide=Mathf.Max(slide,Vector3.Distance(planted,moving));
                            }
                            else landing=false;
                            // Repeat the same actor sample, without changing its target.
                            var root=enemy.Root.position;enemy.Update(state,world.Camera,0,time-dt);
                            if(Vector3.Distance(root,enemy.Root.position)>.002f||Vector3.Distance(l,enemy.FootPosition(true))>.002f||Vector3.Distance(r,enemy.FootPosition(false))>.002f)
                                throw new Exception($"Backstep accumulates on repeated sampling: {id}/{rate} strike={n} stepAge={enemy.StaggerAge:F3} root={Vector3.Distance(root,enemy.Root.position):F4} left={Vector3.Distance(l,enemy.FootPosition(true)):F4} right={Vector3.Distance(r,enemy.FootPosition(false)):F4}");
                        }
                        else landing=false;
                        if(f%3==0)foreach(var skin in enemy.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {skin.BakeMesh(mesh,true);foreach(var v in mesh.vertices)minY=Mathf.Min(minY,skin.transform.TransformPoint(v).y);}
                        if(state.EnemyHealth<health)
                        {
                            contacts++;float nearest=100;Vector3 fist=hero.StrikeOrigin(state.Action)+world.BattleAxis*.12f;
                            foreach(var skin in enemy.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                            {
                                skin.BakeMesh(mesh,true);var vertices=mesh.vertices;var weights=skin.sharedMesh.boneWeights;
                                for(int v=0;v<vertices.Length;v++)
                                {
                                    var w=weights[v];float Chest(int b,float weight)=>skin.bones[b].name.StartsWith("bip_spine",StringComparison.Ordinal)?weight:0;
                                    if(Chest(w.boneIndex0,w.weight0)+Chest(w.boneIndex1,w.weight1)+Chest(w.boneIndex2,w.weight2)+Chest(w.boneIndex3,w.weight3)<.75f)continue;
                                    nearest=Mathf.Min(nearest,Vector3.Distance(fist,skin.transform.TransformPoint(vertices[v])));
                                }
                            }
                            gap=Mathf.Max(gap,nearest);
                            if(nearest>.35f)Debug.Log($"[BackstepGap] hero={id} rate={rate} strike={n} age={state.ActionAge:F4} gap={nearest:F4} fist={fist} torso={enemy.BeamSurfaceContact} stepAge={enemy.StaggerAge:F3}");
                        }
                        health=state.EnemyHealth;
                    }
                    string result=$"{id}/{rate}Hz contacts={contacts} landings={landings} support={support:F4} lift={lift:F4} back={back:F4} plantedSlide={slide:F4} minGround={minY:F4} footStep={maxStep:F4} contactGap={gap:F4}";
                    Debug.Log("[BackstepValidation] "+result);report.AppendLine(result);
                    if(contacts!=10||landings!=2||state.EnemyHealth!=40||state.Energy!=10||support>.035f||lift<.12f||back<.45f||slide>.035f||minY<-.035f||maxStep>dt*5||gap>.36f)
                        throw new Exception("Backstep support/contact failed: "+result);
                }
                finally {world.Camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(mesh);}
            }
            File.WriteAllText(Folder+"/step-validation.txt",report.ToString());
        }
        public static void Interruptions()
        {
            File.Delete(Folder+"/step-interruptions.txt");var report=new StringBuilder();
            foreach(int rate in new[]{15,30,60})foreach(string mode in new[]{"rapid","pause","new-round","beam","late-warning"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var world=new GameWorld();var state=Ready(mode=="beam"?14:4,mode!="late-warning");
                if(mode=="late-warning")for(int i=0;i<2000;i++){state.Tick(.02f,new PlayerInput{Tracking=true});if(state.Enemy==EnemyPhase.Windup&&state.EnemyAge>4.7f)break;}
                var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(null,enemy);enemy.Update(state,world.Camera,0,0);
                float dt=1f/rate,time=0,footStep=0;Vector3 l=enemy.FootPosition(true),r=enemy.FootPosition(false);bool stopped=false,beamed=false;int events=0;
                for(int frame=0;frame<rate*7;frame++)
                {
                    if(!stopped&&enemy.StaggerAge>.3f&&enemy.StaggerAge<1&&mode!="rapid")
                    {stopped=true;if(mode=="pause")state.Pause();if(mode=="new-round")state=new Battle();}
                    bool beam=mode=="beam"&&stopped;
                    state.Tick(world.BattleDelta(dt,state),new PlayerInput{Tracking=true,LeftPunch=frame==0||mode=="rapid"&&frame%Mathf.Max(1,rate/3)==0,Beam=beam,Shield=mode=="late-warning"&&frame>rate/5});
                    beamed|=state.Action==HeroAction.Beam;while(state.TryCue(out var cue))world.Cue(cue,state);
                    enemy.Update(state,world.Camera,dt,time);world.Tick(state,dt,time);time+=dt;
                    if(world.MonsterStaggerLanded)events++;
                    if(mode=="rapid")footStep=Mathf.Max(footStep,Vector3.Distance(l,enemy.FootPosition(true)),Vector3.Distance(r,enemy.FootPosition(false)));
                    l=enemy.FootPosition(true);r=enemy.FootPosition(false);
                    if(mode=="late-warning"&&enemy.StaggerAge<10)throw new Exception("Late warning stole the attack footwork");
                }
                if(mode=="rapid"&&(state.Punches<15||footStep>dt*7))throw new Exception($"Rapid hits teleport foot: {rate}Hz punches={state.Punches} footStep={footStep}");
                if(mode=="beam"&&(!beamed||enemy.StaggerAge<10))throw new Exception("Beam did not supersede step");
                if(mode=="late-warning"&&(state.Blocks!=1||state.HitsTaken!=0||events!=0))throw new Exception("Late warning / guard timing changed");
                if((mode=="pause"||mode=="new-round")&&(!stopped||enemy.StaggerAge<10))throw new Exception("Interrupted step persisted");
                report.AppendLine($"{rate}Hz {mode} punches={state.Punches} events={events} maxFootStep={footStep:F4} passed");
            }
            File.WriteAllText(Folder+"/step-interruptions.txt",report.ToString());Debug.Log("[BackstepInterruptions]\n"+report);
        }
    }
}
