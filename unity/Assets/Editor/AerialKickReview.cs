using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor.SceneManagement;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class AerialKickReview
    {
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/aerial-kick"));
        public static void Before()=>Render("before",false);
        public static void After()=>Render("after",true);
        static Battle Ready(bool warning)
        {
            var s=new Battle(50);s.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
            for(int f=0;f<240;f++)s.Tick(.02f,new PlayerInput{Tracking=true});
            s.GiveInstructionTime(30);
            for(int p=0;p<24;p++){s.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});for(int f=0;f<25;f++)s.Tick(.02f,new PlayerInput{Tracking=true});}
            if(warning)for(int f=0;f<3000&&(s.Enemy!=EnemyPhase.Windup||s.EnemyAge<1);f++)s.Tick(.02f,new PlayerInput{Tracking=true});
            while(s.TryCue(out _)){}return s;
        }
        static void Render(string version,bool check)
        {
            string folder=Folder+"/"+version;Directory.CreateDirectory(folder);
            var sources=new StringBuilder();using(var sha=System.Security.Cryptography.SHA256.Create())
            foreach(string file in new[]{"Scripts/Core/HeroKickMotion.cs","Scripts/Runtime/RiggedActor.cs","Scripts/Runtime/GameWorld.cs","Scripts/Runtime/CombatVfx.cs","Scripts/Runtime/AnimatedActor.cs","Editor/AerialKickReview.cs"})
                sources.AppendLine(file+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Application.dataPath,file)))).Replace("-","").ToLowerInvariant());
            File.WriteAllText(folder+"/sources.txt",sources.ToString());
            var report=new StringBuilder();
            foreach(string id in check?new[]{"Tiga","Mebius","Zero","Geed","Grigio"}:new[]{"Tiga"})foreach(bool left in check?new[]{true,false}:new[]{true})
            foreach(bool warning in id=="Tiga"&&left?new[]{false,true}:new[]{false})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(1001);
                var world=new GameWorld();var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                var state=Ready(warning);hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,1,0);
                var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
                string name=id+"-"+(left?"left":"right")+(warning?"-warning":""),dir=folder+"/"+name;Directory.CreateDirectory(dir+"/frames");
                float supportY=hero.FootPosition(!left).y,minFloor=100,minScreen=1,maxScreen=0,peakAir=0,gap=100,maxStep=0,health=state.EnemyHealth;
                var last=hero.FootPosition(left);var mesh=new Mesh();int hits=0;var csv=new StringBuilder("frame,age,punches,health,energy,supportY\n");
                try
                {
                    for(int f=0;f<120;f++)
                    {
                        const float dt=1/60f;state.Tick(world.BattleDelta(dt,state),new PlayerInput{Tracking=true,LeftPunch=f==10&&left,RightPunch=f==10&&!left});while(state.TryCue(out var cue))world.Cue(cue,state);
                        hero.Update(state,world.Camera,dt,f*dt);enemy.Update(state,world.Camera,dt,f*dt);
                        if(state.EnemyHealth<health)
                        {
                            hits++;world.Hit(false,state);
                            foreach(var skin in enemy.Root.GetComponentsInChildren<SkinnedMeshRenderer>()){skin.BakeMesh(mesh,true);foreach(var v in mesh.vertices)gap=Mathf.Min(gap,Vector3.Distance(hero.StrikeContact(state),skin.transform.TransformPoint(v)));}
                            CharacterReview.Save(world.Camera,rt,dir+"/contact.png");
                        }
                        health=state.EnemyHealth;world.Tick(state,dt,f*dt);
                        peakAir=Mathf.Max(peakAir,Mathf.Min(hero.FootPosition(left).y,hero.FootPosition(!left).y)-supportY);
                        maxStep=Mathf.Max(maxStep,Vector3.Distance(last,hero.FootPosition(left)));last=hero.FootPosition(left);
                        if(f%3==0)foreach(var skin in hero.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {skin.BakeMesh(mesh,true);foreach(var v in mesh.vertices){var p=skin.transform.TransformPoint(v);minFloor=Mathf.Min(minFloor,p.y);var s=world.Camera.WorldToViewportPoint(p);minScreen=Mathf.Min(minScreen,s.y);maxScreen=Mathf.Max(maxScreen,s.y);}}
                        if(f%2==0)CharacterReview.Save(world.Camera,rt,$"{dir}/frames/{f/2:D4}.png");
                        csv.AppendLine(FormattableString.Invariant($"{f},{state.ActionAge:F4},{state.Punches},{state.EnemyHealth},{state.Energy},{hero.FootPosition(!left).y:F4}"));
                    }
                    float returned=Mathf.Abs(hero.FootPosition(!left).y-supportY);
                    string result=$"{name} hits={hits} punches={state.Punches} health={state.EnemyHealth} energy={state.Energy} bothFeetLift={peakAir:F4} ground={minFloor:F4} contactGap={gap:F4} footStep={maxStep:F4} returnY={returned:F4} viewport=({minScreen:F3},{maxScreen:F3})";
                    Debug.Log("[AerialKickReview] "+result);report.AppendLine(result);File.WriteAllText(dir+"/sequence.csv",csv.ToString());
                    if(hits!=1||state.Punches!=25||state.EnemyHealth!=25||state.Energy!=15)throw new Exception("Aerial strike changed damage: "+result);
                    if(check&&(peakAir<.15f||minFloor<-.04f||gap>.28f||maxStep>.8f||returned>.10f||!warning&&(minScreen<.04f||maxScreen>.91f)))throw new Exception("Aerial geometry/framing failed: "+result);
                }
                finally{world.Camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(mesh);}
            }
            File.WriteAllText(folder+"/validation.txt",report.ToString());
        }
    }
}
