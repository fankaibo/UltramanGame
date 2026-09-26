using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class FinisherReview
    {
        public static void Before()=>Render("before");
        public static void After(){Render("after");ValidateFlow();}
        static void ValidateFlow()
        {
            var reports=new StringBuilder();
            foreach(int fps in new[]{15,30,60})ValidateSequence("Tiga",fps,-1,reports);
            foreach(string hero in new[]{"Mebius","Zero","Geed","Grigio"})ValidateSequence(hero,60,-1,reports);
            // Interrupt flight, sustained contact and fade, then clear the
            // presentation exactly as the live pause/restart path does.
            foreach(float interrupt in new[]{.33f,.68f,1.36f})ValidateSequence("Tiga",60,interrupt,reports);
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/finisher/after/flow-validation.txt")),reports.ToString());
        }
        static void ValidateSequence(string heroId,int fps,float interrupt,StringBuilder reports)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var world=new GameWorld();var state=new Battle(50);
            var hero=new AnimatedActor(heroId,world.HeroHome,world.EnemyHome);
            var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});for(int i=0;i<120;i++)state.Tick(.02f,new PlayerInput{Tracking=true});
            for(int hit=0;hit<15;hit++)
            {state.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});for(int i=0;i<22;i++)state.Tick(.02f,new PlayerInput{Tracking=true});}
            state.GiveInstructionTime(10);while(state.TryCue(out _)){}
            hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,1,0);
            var stream=GameObject.Find("Zeperion traveling stream").GetComponent<LineRenderer>();
            float health=state.EnemyHealth,maxTravel=0;Vector3 restingTarget=world.BeamTarget;int damage=0,flight=0,contact=0,releases=0;
            float dt=1f/fps;
            for(int f=0;f<fps*5;f++)
            {
                state.Tick(world.BattleDelta(dt,state),new PlayerInput{Tracking=true,Beam=f==0});
                while(state.TryCue(out var cue))world.Cue(cue,state);
                hero.Update(state,world.Camera,dt,f*dt);enemy.Update(state,world.Camera,dt,f*dt);
                if(state.EnemyHealth<health){damage++;world.Hit(true,state);}health=state.EnemyHealth;
                world.Tick(state,dt,f*dt);
                if(world.BeamStarted)releases++;
                if(world.Closeup.Active&&(world.BeamVisible||damage!=0))throw new Exception("Beam/damage during closeup");
                if(world.BeamVisible)
                {
                    if(Vector3.Distance(stream.GetPosition(0),hero.BeamOrigin)>.001f)throw new Exception("Detached beam muzzle");
                    float toTarget=Vector3.Distance(stream.GetPosition(1),world.BeamTarget);
                    if(state.ActionAge<Battle.BeamHitSeconds)
                    {flight++;if(damage!=0||toTarget<=.005f)throw new Exception("Beam arrived before rule contact");}
                    else
                    {contact++;if(damage!=1||toTarget>.001f)throw new Exception("Stream does not track recoiling chest");}
                    maxTravel=Mathf.Max(maxTravel,Vector3.Distance(restingTarget,world.BeamTarget));
                }
                if(interrupt>0&&state.Action==HeroAction.Beam&&state.ActionAge>=interrupt)
                {
                    state.Pause();world.Tick(state,dt,f*dt);
                    if(world.BeamVisible||GameObject.Find("Energy spill").GetComponent<Light>().intensity!=0||
                        GameObject.Find("Impact spill").GetComponent<Light>().intensity!=0)throw new Exception("Pause leaves beam light/stream visible");
                    world.ResetPresentation();world.Tick(new Battle(),dt,f*dt);
                    if(world.BeamVisible)throw new Exception("Restart leaves beam visible");
                    string canceled=$"{heroId} {fps}fps cancelAt={interrupt:F2} pause-and-reset=passed";
                    reports.AppendLine(canceled);Debug.Log("[FinisherFlow] "+canceled);return;
                }
            }
            if(damage!=1||health!=26||releases!=1||flight==0||contact==0||maxTravel<.04f||world.BeamVisible)
                throw new Exception($"Invalid flow hero={heroId} fps={fps} damage={damage} health={health} release={releases} flight={flight} contact={contact} chestTravel={maxTravel}");
            string result=$"{heroId} {fps}fps flight={flight} contact={contact} damage=9 releases=1 chestTravel={maxTravel:F4} recovery=passed";
            reports.AppendLine(result);Debug.Log("[FinisherFlow] "+result);
        }
        static void Render(string version)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            UnityEngine.Random.InitState(926);
            var world=new GameWorld();var state=new Battle(50);
            var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);
            var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
            for(int i=0;i<120;i++)state.Tick(.02f,new PlayerInput{Tracking=true});
            for(int hit=0;hit<15;hit++)
            {
                state.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});
                for(int i=0;i<22;i++)state.Tick(.02f,new PlayerInput{Tracking=true});
            }
            state.GiveInstructionTime(10);while(state.TryCue(out _)){}
            hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,1,0);
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/finisher",version));
            Directory.CreateDirectory(folder+"/frames");File.Delete(folder+"/validation.txt");
            var source=new StringBuilder("UTC: "+DateTime.UtcNow.ToString("O")+"\nUnity: "+Application.unityVersion+"\n");
            using(var sha=System.Security.Cryptography.SHA256.Create())
                foreach(string file in new[]{"Scripts/Runtime/CombatVfx.cs","Scripts/Runtime/GameWorld.cs","Scripts/Runtime/RiggedActor.cs","Scripts/Runtime/BeamStream.cs","Resources/BeamStream.shader","Editor/FinisherReview.cs"})
                {string path=Path.Combine(Application.dataPath,file);if(File.Exists(path))source.AppendLine(file+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant());}
            File.WriteAllText(folder+"/render-source.txt",source.ToString());
            var rt=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
            var csv=new StringBuilder("frame,time,actionAge,closeup,beam,health,originX,originY,originZ\n");
            float health=state.EnemyHealth;int contacts=0,releases=0;var markers=new bool[5];
            float[] times={.31f,.48f,.66f,1.02f,1.42f};string[] names={"travel","contact","sustain","recoil","fade"};
            try
            {
                for(int frame=0;frame<252;frame++)
                {
                    const float dt=1/60f;float time=frame*dt;
                    state.Tick(world.BattleDelta(dt,state),new PlayerInput{Tracking=true,Beam=frame==12});
                    while(state.TryCue(out var cue))world.Cue(cue,state);
                    hero.Update(state,world.Camera,dt,time);enemy.Update(state,world.Camera,dt,time);
                    if(state.EnemyHealth<health){contacts++;world.Hit(true,state);}
                    health=state.EnemyHealth;world.Tick(state,dt,time);enemy.SetPresentationOpacity(world.EnemyOpacity);
                    if(world.BeamStarted)releases++;
                    var origin=world.BeamOrigin;
                    csv.AppendLine(FormattableString.Invariant($"{frame},{time:F4},{state.ActionAge:F4},{world.Closeup.Active},{world.BeamVisible},{health},{origin.x:F4},{origin.y:F4},{origin.z:F4}"));
                    if(frame%2==0)CharacterReview.Save(world.Camera,rt,$"{folder}/frames/{frame/2:D4}.png");
                    if(state.Action==HeroAction.Beam&&!world.Closeup.Active)
                        for(int i=0;i<times.Length;i++)if(!markers[i]&&state.ActionAge>=times[i])
                        {markers[i]=true;CharacterReview.Save(world.Camera,rt,folder+"/"+names[i]+".png");}
                }
                if(contacts!=1||releases!=1||state.EnemyHealth!=26||world.BeamVisible)throw new Exception("Incomplete beam sequence");
                foreach(bool marker in markers)if(!marker)throw new Exception("Missing finisher stage");
                string result=$"{version}: passed frames=126 contacts={contacts} releases={releases} health={health} closeup-and-recovery=passed";
                File.WriteAllText(folder+"/sequence.csv",csv.ToString());File.WriteAllText(folder+"/validation.txt",result);Debug.Log("[FinisherReview] "+result);
            }
            finally {world.Camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
        }
    }
}
