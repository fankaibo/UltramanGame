using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class ArenaLightReview
    {
        public static void Validate()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/arena-light-20261011"));
            Directory.CreateDirectory(folder);var report=new StringBuilder();
            foreach(string id in new[]{"Tiga","Zero"})foreach(int hz in new[]{15,30,60})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var world=new GameWorld();var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);
                var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);world.SetHeroProfile(id);
                var state=new Battle();state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
                while(state.Phase!=GamePhase.Battle)state.Tick(.02f,new PlayerInput{Tracking=true});state.GiveInstructionTime(30);while(state.TryCue(out _)){}
                var lamp=GameObject.Find("Ranged muzzle spill").GetComponent<Light>();
                int lights=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length;
                float peak=0,dt=1f/hz;bool rendered=false;
                for(int f=0;f<hz*2;f++)
                {
                    state.Tick(dt,new PlayerInput{Tracking=true,RangedAttack=f==hz/2,RightPunch=f==hz/2,AttackSpeed=1});
                    while(state.TryCue(out var cue))world.Cue(cue,state);
                    hero.Update(state,world.Camera,dt,f*dt);enemy.Update(state,world.Camera,dt,f*dt);world.Tick(state,dt,f*dt);
                    peak=Mathf.Max(peak,lamp.intensity);
                    if(lamp.intensity>0)
                    {
                        var muzzle=GameObject.Find("Ranged launch volume");
                        if(!muzzle||Vector3.Distance(lamp.transform.position,muzzle.transform.position)>.15f)throw new Exception("Light detached from visible muzzle");
                        var position=lamp.transform.position;float intensity=lamp.intensity;
                        world.Tick(state,0,f*dt);
                        if(lamp.transform.position!=position||Mathf.Abs(lamp.intensity-intensity)>.0001f)throw new Exception("Zero-time sample drifts muzzle light");
                        if(id=="Tiga"&&hz==60&&!rendered&&state.ActionAge>.08f)
                        {
                            rendered=true;var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
                            try
                            {
                                CharacterReview.Save(world.Camera,rt,folder+"/muzzle-lit.png");lamp.intensity=0;
                                CharacterReview.Save(world.Camera,rt,folder+"/muzzle-unlit.png");lamp.intensity=intensity;
                            }
                            finally{world.Camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
                        }
                    }
                }
                if((id=="Tiga"?peak<=0:peak>0)||lamp.intensity!=0)throw new Exception("Wrong hero or expired muzzle remains lit");
                // Interrupt a fresh preparation: no ghost light on pause or reset.
                state.Tick(.07f,new PlayerInput{Tracking=true,RangedAttack=true,LeftPunch=true});
                hero.Update(state,world.Camera,.07f,3);world.Tick(state,.07f,3);state.Pause();world.Tick(state,0,3);
                if(lamp.intensity!=0)throw new Exception("Pause leaves muzzle lit");
                world.ResetPresentation();world.Tick(new Battle(),0,4);
                if(lamp.intensity!=0||UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length!=lights)throw new Exception("Reset or light pool leaked");
                report.AppendLine($"{id}-{hz} peak={peak:F4} muzzleAttached=pass zeroTime=pass expires=pass pause=pass reset=pass lights={lights}");
            }
            File.WriteAllText(folder+"/light-lifecycle.txt",report.ToString());Debug.Log("[ArenaLightReview] "+report);
        }
    }
}
