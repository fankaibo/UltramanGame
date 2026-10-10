using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class LightingReview
    {
        public static void Before()=>Render("before",ColorSpace.Gamma);
        public static void After()=>Render("after",ColorSpace.Linear);
        public static void Validate()
        {After();PhotoColorChecks.Run();HeroLightReview.Validate();SurfaceImpactReview.Validate();GuardImpactReview.LightInterruptions();}
        static void CheckShaders()
        {
            foreach(string name in new[]{"HeroSurface","KaijuSurface","PhotoLayer","VolcanicPlume","BeamImpactVolume","CinematicComposite"})
            {var shader=Resources.Load<Shader>(name);if(!shader||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new Exception("Invalid lighting shader: "+name);}
        }
        static void Render(string version,ColorSpace expected)
        {
            if(QualitySettings.activeColorSpace!=expected)throw new Exception("Wrong active color space: "+QualitySettings.activeColorSpace);
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/linear-lighting",version));Directory.CreateDirectory(folder);File.Delete(folder+"/validation.txt");
            var report=new StringBuilder();
            foreach(string id in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(927);
                var world=new GameWorld();var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                var state=new Battle(80);state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});for(int i=0;i<(Battle.TransformationSeconds+0.6f)/(.02f);i++)state.Tick(.02f,new PlayerInput{Tracking=true});state.GiveInstructionTime(30);while(state.TryCue(out _)){}
                hero.Update(state,world.Camera,.1f,2);enemy.Update(state,world.Camera,.1f,2);world.Tick(state,1,2);
                var target=new RenderTexture(1280,720,24){antiAliasing=4};target.Create();world.Camera.targetTexture=target;world.Camera.aspect=16f/9;
                try
                {
                    CharacterReview.Save(world.Camera,target,folder+"/"+id+"-arena.png");
                    var position=world.Camera.transform.position;var rotation=world.Camera.transform.rotation;float fov=world.Camera.fieldOfView;
                    enemy.Root.gameObject.SetActive(false);var side=Vector3.Cross(Vector3.up,world.BattleAxis);
                    world.Camera.transform.position=world.HeroHome+world.BattleAxis*5.8f+side*1.6f+Vector3.up*2.5f;world.Camera.transform.LookAt(world.HeroHome+Vector3.up*2.05f);world.Camera.fieldOfView=31;
                    CharacterReview.Save(world.Camera,target,folder+"/"+id+"-front.png");
                    world.Camera.transform.SetPositionAndRotation(position,rotation);world.Camera.fieldOfView=fov;enemy.Root.gameObject.SetActive(true);
                    for(int n=0;n<15;n++){state.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});for(int i=0;i<25;i++)state.Tick(.02f,new PlayerInput{Tracking=true});}
                    while(state.TryCue(out _)){}float health=state.EnemyHealth;
                    bool peak=false,contact=false;
                    for(int frame=0;frame<220;frame++)
                    {
                        const float dt=1/60f;float time=3+frame*dt;
                        state.Tick(world.BattleDelta(dt,state),new PlayerInput{Tracking=true,Beam=frame==0});while(state.TryCue(out var cue))world.Cue(cue,state);
                        hero.Update(state,world.Camera,dt,time);enemy.Update(state,world.Camera,dt,time);
                        if(state.EnemyHealth<health)world.Hit(true,state);health=state.EnemyHealth;world.Tick(state,dt,time);enemy.SetPresentationOpacity(world.EnemyOpacity);
                        if(!peak&&world.Closeup.Focus>.999f){peak=true;CharacterReview.Save(world.Camera,target,folder+"/"+id+"-closeup.png");}
                        if(!contact&&world.BeamVisible&&state.ActionAge>.72f){contact=true;CharacterReview.Save(world.Camera,target,folder+"/"+id+"-contact.png");}
                    }
                    if(!peak||!contact||state.EnemyHealth!=56)throw new Exception("Incomplete lighting review for "+id);
                    CheckShaders();
                    report.AppendLine($"{id} colorSpace={QualitySettings.activeColorSpace} images=4 health={state.EnemyHealth} energy={state.Energy}");
                }
                finally{world.Camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);}
            }
            File.WriteAllText(folder+"/validation.txt",report.ToString());
            var sources=new StringBuilder();using(var sha=System.Security.Cryptography.SHA256.Create())
                foreach(string path in new[]{"Assets/Scripts/Runtime/RiggedActor.cs","Assets/Scripts/Runtime/GameWorld.cs","Assets/Scripts/Runtime/PhotoComposition.cs","Assets/Resources/HeroSurface.shader","Assets/Resources/KaijuSurface.shader","Assets/Resources/CinematicComposite.shader","Assets/Resources/PhotoLayer.shader","Assets/Resources/VolcanicPlume.shader","Assets/Resources/BeamImpactVolume.shader","Assets/Editor/LightingReview.cs","ProjectSettings/ProjectSettings.asset"})
                {var file=Path.Combine(Application.dataPath,"..",path);sources.AppendLine(path+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file))).Replace("-","").ToLowerInvariant());}
            File.WriteAllText(folder+"/sources.txt",sources.ToString());Debug.Log("[LightingReview]\n"+report);
        }
    }
}
