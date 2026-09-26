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
    public static class KnockdownReview
    {
        public static void Render()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/knockdown-review"));
            Directory.CreateDirectory(folder);Directory.CreateDirectory(folder+"/frames");File.Delete(folder+"/validation.txt");
            var sources=new StringBuilder("Rendered UTC: "+DateTime.UtcNow.ToString("O")+"\nUnity: "+Application.unityVersion+"\n");
            using(var sha=System.Security.Cryptography.SHA256.Create())
                foreach(string path in new[]{"Scripts/Core/KnockdownMotion.cs","Scripts/Core/Battle.cs","Scripts/Runtime/RiggedActor.cs","Scripts/Runtime/GameWorld.cs","Scripts/Runtime/CombatVfx.cs","Editor/KnockdownReview.cs"})
                    sources.AppendLine(path+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Application.dataPath,path)))).Replace("-","").ToLowerInvariant());
            File.WriteAllText(folder+"/render-source.txt",sources.ToString());
            var report=new StringBuilder();
            for(int id=0;id<HeroRoster.Count;id++)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(426);
                string name=HeroRoster.At(id).Id;
                var world=new GameWorld();var state=new Battle();
                var hero=new AnimatedActor(name,world.HeroHome,world.EnemyHome);
                var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
                for(int i=0;i<2000;i++)
                {
                    state.Tick(.02f,new PlayerInput{Tracking=true});
                    if(state.Enemy==EnemyPhase.Windup&&state.EnemyAge>state.WarningDuration-.3f)break;
                }
                while(state.TryCue(out _)){}
                var rt=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32){antiAliasing=4};rt.Create();
                world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
                hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,.1f,0);
                bool[] saved=new bool[4];int landings=0;float minGround=100,minX=1,minY=1,maxX=0,maxY=0;
                var baked=new Mesh();
                try
                {
                    for(int frame=0;frame<190;frame++)
                    {
                        const float dt=1/60f;float time=frame*dt;
                        state.Tick(world.BattleDelta(dt,state),new PlayerInput{Tracking=true});
                        while(state.TryCue(out var cue)){world.Cue(cue,state);if(cue==GameCue.HeroLanded)landings++;}
                        hero.Update(state,world.Camera,dt,time);enemy.Update(state,world.Camera,dt,time);world.Tick(state,dt,time);
                        if(id==0&&frame%2==0)CharacterReview.Save(world.Camera,rt,folder+"/frames/"+(frame/2).ToString("D4")+".png");
                        if(state.Action!=HeroAction.Hurt)continue;
                        float age=state.ActionAge;
                        if(frame%3==0&&age>.18f)
                            foreach(var skin in hero.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                            {
                                skin.BakeMesh(baked,true);
                                foreach(var vertex in baked.vertices)
                                {
                                    Vector3 p=skin.transform.TransformPoint(vertex),v=world.Camera.WorldToViewportPoint(p);
                                    minGround=Mathf.Min(minGround,p.y);minX=Mathf.Min(minX,v.x);maxX=Mathf.Max(maxX,v.x);
                                    minY=Mathf.Min(minY,v.y);maxY=Mathf.Max(maxY,v.y);
                                }
                            }
                        float[] moments={.2f,.45f,.9f,1.4f};string[] names={"fall","landed","rising","recovered"};
                        for(int k=0;k<4;k++)if(!saved[k]&&age>=moments[k])
                        {CharacterReview.Save(world.Camera,rt,folder+"/"+name+"-"+names[k]+".png");saved[k]=true;}
                    }
                    string metrics=$"{name}: landings={landings} ground={minGround:F3} viewport=({minX:F3},{minY:F3})-({maxX:F3},{maxY:F3})";
                    Debug.Log("[KnockdownReview] "+metrics);report.AppendLine(metrics);
                    if(landings!=1||!Array.TrueForAll(saved,v=>v)||state.Action!=HeroAction.None||state.HitsTaken!=1)
                        throw new Exception("Fall/recovery sequence incomplete: "+metrics);
                    if(minGround<-.05f||minX<.02f||minY<.045f||maxX>.98f||maxY>.94f)
                        throw new Exception("Fall penetrates ground or leaves the HUD-safe viewport: "+metrics);
                }
                finally
                {
                    world.Camera.targetTexture=null;RenderTexture.active=null;rt.Release();
                    UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(baked);
                }
            }
            File.WriteAllText(folder+"/validation.txt",report.ToString());Debug.Log("[KnockdownReview] passed all heroes");
        }
    }
}
