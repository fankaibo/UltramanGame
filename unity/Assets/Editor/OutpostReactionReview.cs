using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class OutpostReactionReview
    {
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/outpost-reaction"));
        public static void Before()=>Render("before");
        public static void After()=>Render("after");
        static void Render(string version)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(940);
            var world=new GameWorld();var state=new Battle();state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
            for(int n=0;n<4000;n++)
            {
                state.Tick(1/60f,new PlayerInput{Tracking=true,Shield=true});
                if(state.EnemyAttackCount==2&&state.Enemy==EnemyPhase.Windup&&state.EnemyAge>=state.WarningDuration-.25f)break;
            }
            if(state.EnemyAttackCount!=2||state.Enemy!=EnemyPhase.Windup)throw new Exception("Missing third-attack preparation");
            while(state.TryCue(out _)){}
            var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,1,0);
            string folder=Folder+"/"+version;Directory.CreateDirectory(folder+"/frames");File.Delete(folder+"/validation.txt");
            var csv=new StringBuilder("frame,phase,enemy,age,health,blocks,hurt\n");
            var target=new RenderTexture(1280,720,24){antiAliasing=4};target.Create();world.Camera.targetTexture=target;world.Camera.aspect=16f/9;
            try
            {
                for(int f=0;f<360;f++)
                {
                    const float dt=1/60f;state.Tick(world.BattleDelta(dt,state),new PlayerInput{Tracking=true,Shield=true});
                    while(state.TryCue(out var cue))world.Cue(cue,state);
                    hero.Update(state,world.Camera,dt,f*dt);enemy.Update(state,world.Camera,dt,f*dt);world.Tick(state,dt,f*dt);
                    csv.AppendLine(FormattableString.Invariant($"{f},{state.Phase},{state.Enemy},{state.EnemyAge:F5},{state.EnemyHealth},{state.Blocks},{state.HitsTaken}"));
                    if(f%2==0)CharacterReview.Save(world.Camera,target,$"{folder}/frames/{f/2:D4}.png");
                    if(f==65||f==110||f==180)CharacterReview.Save(world.Camera,target,$"{folder}/slam-{f}.png");
                }
                if(state.EnemyHealth!=50||state.Blocks!=3||state.HitsTaken!=0)throw new Exception("Slam changed battle result");
                File.WriteAllText(folder+"/sequence.csv",csv.ToString());
                var sources=new StringBuilder();using(var sha=System.Security.Cryptography.SHA256.Create())
                    foreach(string file in new[]{"Scripts/Runtime/VolcanicOutpost.cs","Scripts/Runtime/OutpostDamage.cs","Scripts/Runtime/VolcanoStage.cs","Scripts/Runtime/GameWorld.cs","Scripts/Runtime/CombatVfx.cs","Scripts/Runtime/GroundImpact.cs","Scripts/Core/Battle.cs","Scripts/Runtime/RiggedActor.cs","Resources/OutpostSurface.shader","Resources/GroundDust.shader","Editor/OutpostReactionReview.cs"})
                        if(File.Exists(Path.Combine(Application.dataPath,file)))sources.AppendLine(file+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Application.dataPath,file)))).Replace("-","").ToLowerInvariant());
                File.WriteAllText(folder+"/sources.txt",sources.ToString());File.WriteAllText(folder+"/validation.txt","passed frames=180 samples=360 health=50 blocks=3 hurt=0");
                Debug.Log("[OutpostReactionReview] "+version+" passed frames=180 samples=360");
            }
            finally{world.Camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);}
        }
    }
}
