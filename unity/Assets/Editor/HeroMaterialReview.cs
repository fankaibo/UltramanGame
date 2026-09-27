using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class HeroMaterialReview
    {
        public static void Before()=>Render("before");
        public static void After()=>Render("after");
        static void Render(string version)
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/hero-environment",version));Directory.CreateDirectory(folder);
            var report=new StringBuilder();
            foreach(string id in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(927);
                var world=new GameWorld();var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);
                var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                enemy.Root.gameObject.SetActive(false);
                var state=new Battle();hero.Update(state,world.Camera,0,0,0);world.Tick(state,0,0);
                // Keep the real arena lights and suit shader, with no extra
                // inspection fill that could conceal a missing environment.
                var camera=new GameObject("Material comparison camera").AddComponent<Camera>();camera.enabled=false;
                camera.cullingMask=1<<ContactShadows.ActorLayer;camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=new Color(.025f,.035f,.05f);camera.aspect=4f/3;camera.fieldOfView=31;
                camera.allowHDR=true;camera.renderingPath=RenderingPath.Forward;
                var target=new RenderTexture(1280,960,24){antiAliasing=4};target.Create();camera.targetTexture=target;
                bool fog=RenderSettings.fog;RenderSettings.fog=false;
                try
                {
                    foreach(int pose in new[]{0,3,4})foreach(bool back in new[]{false,true})
                    {
                        hero.Update(state,world.Camera,0,0,pose);
                        var side=Vector3.Cross(Vector3.up,world.BattleAxis);
                        camera.transform.position=world.HeroHome+world.BattleAxis*(back?-5.8f:5.8f)+side*1.6f+Vector3.up*2.5f;
                        camera.transform.LookAt(world.HeroHome+Vector3.up*1.95f);
                        CharacterReview.Save(camera,target,Path.Combine(folder,$"{id}-{pose}-{(back?"back":"front")}.png"));
                    }
                    report.AppendLine($"{id}: poses=6 reflectionMode={RenderSettings.defaultReflectionMode} environment={RenderSettings.customReflectionTexture?.name??"none"} intensity={RenderSettings.reflectionIntensity:F3}");
                }
                finally {RenderSettings.fog=fog;camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);}
            }
            File.WriteAllText(folder+"/validation.txt",report.ToString());
            var sources=new StringBuilder();using(var sha=System.Security.Cryptography.SHA256.Create())
                foreach(string file in new[]{"Scripts/Runtime/GameWorld.cs","Scripts/Runtime/RiggedActor.cs","Scripts/Runtime/VolcanoStage.cs","Scripts/Runtime/VolcanoEnvironment.cs","Resources/HeroSurface.shader","Resources/VolcanoReflection.shader","Resources/CinematicComposite.shader","Editor/HeroMaterialReview.cs"})
                {var path=Path.Combine(Application.dataPath,file);if(File.Exists(path))sources.AppendLine(file+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant());}
            File.WriteAllText(folder+"/sources.txt",sources.ToString());Debug.Log("[HeroMaterialReview] "+version+"\n"+report);
        }
    }
}
