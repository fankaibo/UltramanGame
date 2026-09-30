using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class HeroKickReview
    {
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/hero-kick"));
        public static void Release(){Validate();HurtTransitions();ComboStrikeReview.Validate();ComboStrikeReview.Interruptions();ComboStrikeReview.After();}
        static Battle Ready(int punches=4)
        {
            var state=new Battle(50);state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
            for(int f=0;f<(Battle.TransformationSeconds+.2f)/.02f;f++)state.Tick(.02f,new PlayerInput{Tracking=true});
            for(int p=0;p<punches;p++){state.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});for(int f=0;f<25;f++)state.Tick(.02f,new PlayerInput{Tracking=true});}
            state.GiveInstructionTime(20);while(state.TryCue(out _)){}return state;
        }
        public static void Validate()
        {
            Directory.CreateDirectory(Folder);File.Delete(Folder+"/kick-validation.txt");var report=new StringBuilder();
            for(int id=0;id<HeroRoster.Count;id++)foreach(bool left in new[]{true,false})
            {
                string name=HeroRoster.At(id).Id;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var world=new GameWorld();var hero=new AnimatedActor(name,world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                var state=Ready();hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,1,0);
                string folder=$"{Folder}/roster/{name}-{(left?"left":"right")}";Directory.CreateDirectory(folder+"/frames");
                var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
                var mesh=new Mesh();Vector3 support=hero.FootPosition(!left),lastBoot=hero.FootPosition(left);
                float drift=0,ground=100,contactGap=100,step=0,peakLift=0,frozen=0,minScreen=1,maxScreen=0;int hits=0,frames=0;float health=state.EnemyHealth;
                try
                {
                    for(int f=0;f<66;f++)
                    {
                        const float dt=1/60f;float time=f*dt;
                        state.Tick(world.BattleDelta(dt,state),new PlayerInput{Tracking=true,LeftPunch=f==0&&left,RightPunch=f==0&&!left});
                        while(state.TryCue(out var cue))world.Cue(cue,state);
                        hero.Update(state,world.Camera,dt,time);enemy.Update(state,world.Camera,dt,time);
                        if(state.EnemyHealth<health)
                        {
                            hits++;contactGap=SurfaceGap(enemy,hero.StrikeContact(state),mesh);world.Hit(false,state);
                            CharacterReview.Save(world.Camera,rt,folder+"/contact.png");
                        }
                        health=state.EnemyHealth;world.Tick(state,dt,time);
                        var boot=hero.FootPosition(left);step=Mathf.Max(step,Vector3.Distance(boot,lastBoot));lastBoot=boot;
                        if(HeroKickMotion.Active(state))
                        {
                            frames++;if(frames==1)support=hero.FootPosition(!left);
                            drift=Mathf.Max(drift,Vector3.Distance(support,hero.FootPosition(!left)));peakLift=Mathf.Max(peakLift,boot.y);
                            // The opponent moves only between frames. Re-evaluating
                            // this exact geometry must not feed last-frame IK back.
                            hero.Update(state,world.Camera,0,time);var held=hero.FootPosition(left);var heldSupport=hero.FootPosition(!left);
                            hero.Update(state,world.Camera,0,time);frozen=Mathf.Max(frozen,Vector3.Distance(held,hero.FootPosition(left)),Vector3.Distance(heldSupport,hero.FootPosition(!left)));
                        }
                        if(f%6==0)foreach(var skin in hero.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {
                            skin.BakeMesh(mesh,true);foreach(var v in mesh.vertices)
                            {var p=skin.transform.TransformPoint(v);ground=Mathf.Min(ground,p.y);var screen=world.Camera.WorldToViewportPoint(p);minScreen=Mathf.Min(minScreen,screen.y);maxScreen=Mathf.Max(maxScreen,screen.y);}
                        }
                        if(f%2==0)CharacterReview.Save(world.Camera,rt,$"{folder}/frames/{f/2:D4}.png");
                    }
                    string result=$"{name}/{left} hits={hits} health={state.EnemyHealth} energy={state.Energy} kickFrames={frames} supportDrift={drift:F4} ground={ground:F4} contactGap={contactGap:F4} peakLift={peakLift:F3} footStep={step:F3} frozen={frozen:F6} viewport=({minScreen:F3},{maxScreen:F3})";
                    Debug.Log("[HeroKickReview] "+result);report.AppendLine(result);
                    if(hits!=1||state.Punches!=5||state.EnemyHealth!=45||state.Energy!=5||frames<20||drift>.055f||ground<-.035f||contactGap>.36f||peakLift<1.2f||step>.8f||frozen>.005f||minScreen<.04f||maxScreen>.96f)throw new Exception("Kick pose/contact validation failed: "+result);
                }
                finally{world.Camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(mesh);}
                foreach(string kind in new[]{"shield","beam","pause"})Interruption(name,left,kind,report);
            }
            File.WriteAllText(Folder+"/kick-validation.txt",report.ToString());
            using(var sha=System.Security.Cryptography.SHA256.Create())File.WriteAllText(Folder+"/kick-checks-source.txt",BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Application.dataPath,"Editor/HeroKickReview.cs")))).Replace("-","").ToLowerInvariant());
        }
        static float SurfaceGap(AnimatedActor enemy,Vector3 point,Mesh mesh)
        {
            float gap=100;foreach(var skin in enemy.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {skin.BakeMesh(mesh,true);foreach(var v in mesh.vertices)gap=Mathf.Min(gap,Vector3.Distance(point,skin.transform.TransformPoint(v)));}return gap;
        }
        public static void HurtTransitions()
        {
            var report=new StringBuilder();File.Delete(Folder+"/kick-hurt.txt");
            for(int id=0;id<HeroRoster.Count;id++)foreach(bool left in new[]{true,false})
            {
                string name=HeroRoster.At(id).Id;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var world=new GameWorld();var hero=new AnimatedActor(name,world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                var state=Ready();for(int f=0;f<2000&&(state.Enemy!=EnemyPhase.Attack||state.EnemyAge<.21f);f++)state.Tick(.02f,new PlayerInput{Tracking=true});
                while(state.TryCue(out _)){}hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,0,0);
                float jump=0,ground=100,entryStep=0;int interruptions=0;var mesh=new Mesh();
                RenderTexture rt=null;int capture=0;
                if(name=="Tiga"&&left){Directory.CreateDirectory(Folder+"/hurt-entry");rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;}
                for(int f=0;f<150;f++)
                {
                    const float dt=1/60f;bool wasKick=HeroKickMotion.Active(state);var foot=hero.FootPosition(left);
                    state.Tick(world.BattleDelta(dt,state),new PlayerInput{Tracking=true,LeftPunch=f==0&&left,RightPunch=f==0&&!left});
                    while(state.TryCue(out var cue))world.Cue(cue,state);
                    hero.Update(state,world.Camera,dt,f*dt);enemy.Update(state,world.Camera,dt,f*dt);world.Tick(state,dt,f*dt);
                    if(wasKick&&state.Action==HeroAction.Hurt){interruptions++;jump=Vector3.Distance(foot,hero.FootPosition(left));}
                    if(state.Action==HeroAction.Hurt&&state.ActionAge<.22f)
                    {
                        entryStep=Mathf.Max(entryStep,Vector3.Distance(foot,hero.FootPosition(left)));
                        if(rt)CharacterReview.Save(world.Camera,rt,$"{Folder}/hurt-entry/{capture++:D4}.png");
                    }
                    if(f%3==0)foreach(var skin in hero.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {skin.BakeMesh(mesh,true);foreach(var v in mesh.vertices)ground=Mathf.Min(ground,skin.transform.TransformPoint(v).y);}
                }
                if(rt){world.Camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
                UnityEngine.Object.DestroyImmediate(mesh);
                string result=$"{name}/{left} hurtTransitions={interruptions} entryFootStep={jump:F4} blendFootStep={entryStep:F4} ground={ground:F4} recovered={state.Action==HeroAction.None} hitsTaken={state.HitsTaken}";
                Debug.Log("[KickHurt] "+result);report.AppendLine(result);
                if(interruptions!=1||jump>.7f||entryStep>.7f||ground<-.045f||state.Action!=HeroAction.None||state.HitsTaken!=1)throw new Exception("Kick-to-hurt continuity failed: "+result);
            }
            File.WriteAllText(Folder+"/kick-hurt.txt",report.ToString());
        }
        static void Interruption(string name,bool left,string kind,StringBuilder report)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var world=new GameWorld();var hero=new AnimatedActor(name,world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            var state=Ready(24);float time=0;const float dt=1/60f;
            void Step(PlayerInput input)
            {
                state.Tick(world.BattleDelta(dt,state),input);while(state.TryCue(out var cue))world.Cue(cue,state);
                hero.Update(state,world.Camera,dt,time);enemy.Update(state,world.Camera,dt,time);world.Tick(state,dt,time);time+=dt;
            }
            hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,1,0);
            Step(new PlayerInput{Tracking=true,LeftPunch=left,RightPunch=!left});
            while(state.ActionAge<.16f)Step(new PlayerInput{Tracking=true});
            if(!HeroKickMotion.Active(state)||state.Punches!=25)throw new Exception("Interruption has no extended kick");
            var last=hero.FootPosition(left);float maxStep=0;
            if(kind=="pause")state.Pause();
            for(int f=0;f<90;f++)
            {
                Step(new PlayerInput{Tracking=true,Shield=kind=="shield",Beam=kind=="beam"&&f==0,BeamIntent=kind=="beam"});
                if(HeroKickMotion.Active(state))throw new Exception("Kick blocks new defense/special move");
                if(f==0&&kind=="shield"&&!state.Shield||f==0&&kind=="beam"&&state.Action!=HeroAction.Beam)throw new Exception("Gesture priority delayed");
                if(kind!="pause"&&f<20)maxStep=Mathf.Max(maxStep,Vector3.Distance(last,hero.FootPosition(left)));last=hero.FootPosition(left);
            }
            if(maxStep>.70f||state.Punches!=25)throw new Exception($"Interrupted leg snaps or scores again: {name}/{left}/{kind} step={maxStep}");
            world.ResetPresentation();var fresh=new AnimatedActor(name,world.HeroHome,world.EnemyHome);state=new Battle();
            for(int f=0;f<30;f++){hero.Update(state,world.Camera,dt,time);fresh.Update(state,world.Camera,dt,time);time+=dt;}
            if(Vector3.Distance(hero.FootPosition(left),fresh.FootPosition(left))>.001f||Vector3.Distance(hero.FootPosition(!left),fresh.FootPosition(!left))>.001f)throw new Exception("Reset retains kicking leg");
            report.AppendLine($"{name}/{left}/{kind} priority=passed footStep={maxStep:F4} no-extra-hit=passed new-round=passed");
        }
    }
}
