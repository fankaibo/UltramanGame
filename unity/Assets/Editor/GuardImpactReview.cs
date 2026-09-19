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
    public static class GuardImpactReview
    {
        public static void Render()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--guard-output");
            string folder=Path.GetFullPath(at>=0&&at+1<args.Length?args[at+1]:Path.Combine(Application.dataPath,"../../artifacts/guard-review",DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")));
            Directory.CreateDirectory(folder);
            File.Delete(Path.Combine(folder,"validation.txt"));
            var sources=new StringBuilder("Rendered UTC: "+DateTime.UtcNow.ToString("O")+"\nUnity: "+Application.unityVersion+"\n");
            using(var sha=System.Security.Cryptography.SHA256.Create())
                foreach(string path in new[]{"Scripts/Runtime/RiggedActor.cs","Scripts/Runtime/CombatVfx.cs","Scripts/Runtime/GameWorld.cs","Scripts/Runtime/MonsterAttackEffects.cs","Resources/EnergyShield.shader","Editor/GuardImpactReview.cs"})
                    sources.AppendLine(path+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Application.dataPath,path)))).Replace("-","").ToLowerInvariant());
            File.WriteAllText(Path.Combine(folder,"render-source.txt"),sources.ToString());
            var report=new StringBuilder();
            for(int id=0;id<HeroRoster.Count;id++)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                UnityEngine.Random.InitState(20260920);
                var shader=Resources.Load<Shader>("EnergyShield");
                if(!shader||ShaderUtil.ShaderHasError(shader))throw new Exception("Shield shader failed compilation");
                var world=new GameWorld();var state=new Battle();
                var hero=new AnimatedActor(HeroRoster.At(id).Id,world.HeroHome,world.EnemyHome);
                var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                var spine=Find(hero.Root,"spineLower","bip_spine_0");
                state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
                for(int i=0;i<1500;i++)
                {
                    state.Tick(.02f,new PlayerInput{Tracking=true,Shield=true});
                    if(state.Enemy==EnemyPhase.Windup&&state.EnemyAge>state.WarningDuration-.9f)break;
                }
                while(state.TryCue(out _)){}
                var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32){antiAliasing=4};target.Create();
                world.Camera.targetTexture=target;world.Camera.aspect=16f/9;
                hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,1,0);
                Quaternion baseSpine=spine.localRotation;Vector3 left=default,right=default;
                bool before=false,peak=false,settled=false;float recoil=0,monsterRecoil=0,footError=0;
                try
                {
                    for(int frame=0;frame<150;frame++)
                    {
                        const float dt=1/60f;float time=frame*dt;
                        state.Tick(world.BattleDelta(dt,state),new PlayerInput{Tracking=true,Shield=true});
                        while(state.TryCue(out var cue))world.Cue(cue,state);
                        hero.Update(state,world.Camera,dt,time);enemy.Update(state,world.Camera,dt,time);world.Tick(state,dt,time);
                        if(state.Enemy!=EnemyPhase.Attack)continue;
                        if(!before&&state.EnemyAge>.32f&&state.EnemyAge<Battle.EnemyHitSeconds)
                        {
                            baseSpine=spine.localRotation;left=hero.FootPosition(true);right=hero.FootPosition(false);before=true;
                            CharacterReview.Save(world.Camera,target,Path.Combine(folder,HeroRoster.At(id).Id+"-brace.png"));
                        }
                        if(!peak&&state.EnemyAge>Battle.EnemyHitSeconds+.12f)
                        {
                            recoil=Quaternion.Angle(baseSpine,spine.localRotation);
                            footError=Mathf.Max(Vector3.Distance(left,hero.FootPosition(true)),Vector3.Distance(right,hero.FootPosition(false)));
                            var shield=GameObject.Find("Light shield");var material=shield.GetComponent<Renderer>().sharedMaterial;
                            if(!before||state.Blocks!=1||recoil<7||footError>.025f||material.GetFloat("_HitAge")>.32f||shield.transform.localScale.z>=.38f)
                                throw new Exception($"Guard did not visibly absorb contact with planted feet: {HeroRoster.At(id).Id} recoil={recoil} feet={footError}");
                            // Fresh observer samples the exact same attack without
                            // having observed the block event: compare rebound to
                            // that authored pose, including identical foot keys.
                            var baseline=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);
                            try
                            {
                                baseline.Update(state,world.Camera,0,time);
                                monsterRecoil=Quaternion.Angle(Find(enemy.Root,"bip_spine_2").localRotation,Find(baseline.Root,"bip_spine_2").localRotation);
                                if(monsterRecoil<10||Vector3.Distance(enemy.FootPosition(true),baseline.FootPosition(true))>.01f||Vector3.Distance(enemy.FootPosition(false),baseline.FootPosition(false))>.01f)
                                    throw new Exception("Blocked monster must recoil above planted legs");
                            }
                            finally {UnityEngine.Object.DestroyImmediate(baseline.Root.gameObject);}
                            CharacterReview.Save(world.Camera,target,Path.Combine(folder,HeroRoster.At(id).Id+"-impact.png"));peak=true;
                        }
                        if(!settled&&state.EnemyAge>.99f)
                        {
                            if(Quaternion.Angle(baseSpine,spine.localRotation)>.8f)throw new Exception("Guard did not settle back: "+HeroRoster.At(id).Id);
                            CharacterReview.Save(world.Camera,target,Path.Combine(folder,HeroRoster.At(id).Id+"-settled.png"));settled=true;
                        }
                    }
                    if(!peak||!settled)throw new Exception("Guard review did not reach contact/recovery");
                    state.Pause();hero.Update(state,world.Camera,.02f,3);enemy.Update(state,world.Camera,.02f,3);world.Tick(state,.02f,3);
                    if(GameObject.Find("Light shield"))throw new Exception("Paused guard left an active shield");
                    state=new Battle();hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.ResetPresentation();world.Tick(state,0,0);
                    if(GameObject.Find("Light shield"))throw new Exception("New round replayed a block");
                    report.AppendLine($"{HeroRoster.At(id).Id}: recoil={recoil:F2}deg monsterRecoil={monsterRecoil:F2}deg footDrift={footError:F4}m settled=passed pause=passed restart=passed");
                }
                finally {world.Camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);}
            }
            File.WriteAllText(Path.Combine(folder,"validation.txt"),report.ToString());Debug.Log("[GuardImpactReview] "+report);
        }
        static Transform Find(Transform root,params string[] names)
        {
            foreach(var t in root.GetComponentsInChildren<Transform>())if(Array.IndexOf(names,t.name)>=0)return t;
            throw new Exception("Missing guard recoil bone: "+string.Join("/",names));
        }
    }
}
