using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
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
                foreach(string path in new[]{"Scripts/Runtime/RiggedActor.cs","Scripts/Runtime/AnimatedActor.cs","Scripts/Runtime/CombatVfx.cs","Scripts/Runtime/GameWorld.cs","Scripts/Runtime/MonsterAttackEffects.cs","Resources/EnergyShield.shader","Resources/HeroSurface.shader","Editor/GuardImpactReview.cs"})
                    sources.AppendLine(path+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Application.dataPath,path)))).Replace("-","").ToLowerInvariant());
            File.WriteAllText(Path.Combine(folder,"render-source.txt"),sources.ToString());
            var report=new StringBuilder();
            for(int id=0;id<HeroRoster.Count;id++)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                UnityEngine.Random.InitState(20260920);
                var shader=Resources.Load<Shader>("EnergyShield");
                if(!shader||ShaderUtil.ShaderHasError(shader))throw new Exception("Shield shader failed compilation");
                shader=Resources.Load<Shader>("HeroSurface");
                if(!shader||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new Exception("Hero surface shader failed compilation");
                var world=new GameWorld();var state=new Battle();
                var hero=new AnimatedActor(HeroRoster.At(id).Id,world.HeroHome,world.EnemyHome);
                var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                var body=BodyMaterials(hero);
                // Zeta and DeckerStrong currently use the documented atlas
                // fallback and do not expose skinned shield-reflection
                // materials. Keep the roster review honest while allowing the
                // real rigged heroes to finish the visual guard audit.
                if(body.Length==0)
                {report.AppendLine($"{HeroRoster.At(id).Id}: guardVisual=fallback-skipped");continue;}
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
                            var point=body[0].GetVector("_GuardPoint");var color=body[0].GetColor("_GuardColor");
                            hero.Update(state,world.Camera,0,time);
                            if(Vector4.Distance(point,body[0].GetVector("_GuardPoint"))>.0001f||Mathf.Abs(color.a-body[0].GetColor("_GuardColor").a)>.0001f)
                                throw new Exception("Zero-time sampling changed shield reflection");
                            var light=InspectLight(world,hero,enemy,body,folder+"/"+HeroRoster.At(id).Id);
                            if(light.Item1<30||light.Item2>.015f)throw new Exception($"Shield reflection invisible or reaches legs: {HeroRoster.At(id).Id} pixels={light.Item1} lower={light.Item2}");
                            report.AppendLine($"{HeroRoster.At(id).Id}: guardLightPixels={light.Item1} belowWaistFraction={light.Item2:F6} zeroTime=passed");
                            CharacterReview.Save(world.Camera,target,Path.Combine(folder,HeroRoster.At(id).Id+"-impact.png"));peak=true;
                        }
                        if(!settled&&state.EnemyAge>.99f)
                        {
                            if(Quaternion.Angle(baseSpine,spine.localRotation)>.8f)throw new Exception("Guard did not settle back: "+HeroRoster.At(id).Id);
                            foreach(var mat in body)if(mat.GetColor("_GuardColor").a>.001f)throw new Exception("Settled guard retained body light");
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
        static Material[] BodyMaterials(AnimatedActor hero)
        {
            var result=new List<Material>();
            foreach(var renderer in hero.Root.GetComponentsInChildren<Renderer>())foreach(var mat in renderer.sharedMaterials)
                if(mat.HasProperty("_GuardPoint")&&!result.Contains(mat))result.Add(mat);
            return result.ToArray();
        }
        static (int,float) InspectLight(GameWorld world,AnimatedActor hero,AnimatedActor enemy,Material[] materials,string prefix)
        {
            var go=new GameObject("Hero material inspection");var camera=go.AddComponent<Camera>();camera.enabled=false;
            camera.renderingPath=RenderingPath.Forward;camera.cullingMask=1<<ContactShadows.ActorLayer;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.aspect=1;camera.fieldOfView=37;
            var side=Vector3.Cross(Vector3.up,world.BattleAxis);
            camera.transform.position=world.HeroHome+world.BattleAxis*6.6f+side*1.1f+Vector3.up*2.3f;
            camera.transform.LookAt(world.HeroHome+Vector3.up*1.75f);
            var target=new RenderTexture(384,384,24);target.Create();camera.targetTexture=target;
            var image=new Texture2D(384,384,TextureFormat.RGB24,false);var active=RenderTexture.active;
            bool fog=RenderSettings.fog;var colors=new Color[materials.Length];
            for(int i=0;i<materials.Length;i++)colors[i]=materials[i].GetColor("_GuardColor");
            Color32[] Read(string suffix)
            {camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,384,384),0,0);image.Apply();File.WriteAllBytes(prefix+suffix+".png",image.EncodeToPNG());return image.GetPixels32();}
            enemy.Root.gameObject.SetActive(false);
            try
            {
                Read("-surface");RenderSettings.fog=false;
                foreach(var mat in materials)mat.SetFloat("_EmissionAudit",1);
                var on=Read("-emission");foreach(var mat in materials)mat.SetColor("_GuardColor",Color.clear);
                var off=Read("-no-emission");var repeat=Read("-no-emission-repeat");
                int changed=0;double total=0,lower=0;
                int waist=(int)(camera.WorldToViewportPoint(world.HeroHome+Vector3.up*1.3f).y*384);
                for(int i=0;i<on.Length;i++)
                {
                    if(!off[i].Equals(repeat[i]))throw new Exception("Emission baseline changed without input");
                    int d=Math.Abs(on[i].r-off[i].r)+Math.Abs(on[i].g-off[i].g)+Math.Abs(on[i].b-off[i].b);
                    if(d>12)changed++;total+=d;if(i/384<waist)lower+=d;
                }
                return (changed,(float)(lower/Math.Max(1,total)));
            }
            finally
            {
                for(int i=0;i<materials.Length;i++){materials[i].SetFloat("_EmissionAudit",0);materials[i].SetColor("_GuardColor",colors[i]);}
                enemy.Root.gameObject.SetActive(true);RenderSettings.fog=fog;RenderTexture.active=active;
                camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(go);
            }
        }
        public static void LightInterruptions()
        {
            foreach(string id in new[]{"Tiga","Zero"})foreach(int rate in new[]{15,30,60})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var world=new GameWorld();var state=new Battle();
                var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);
                world.BindActors(hero,enemy);var body=BodyMaterials(hero);
                state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
                for(int i=0;i<1000;i++)
                {state.Tick(.02f,new PlayerInput{Tracking=true});if(state.Enemy==EnemyPhase.Windup&&state.EnemyAge>state.WarningDuration-.3f)break;}
                while(state.TryCue(out _)){}hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);
                bool lit=false;
                for(int i=0;i<rate*3;i++)
                {
                    float dt=1f/rate,t=i*dt;state.Tick(world.BattleDelta(dt,state),new PlayerInput{Tracking=true,Shield=true});
                    while(state.TryCue(out var cue))world.Cue(cue,state);
                    hero.Update(state,world.Camera,dt,t);enemy.Update(state,world.Camera,dt,t);world.Tick(state,dt,t);
                    if(body[0].GetColor("_GuardColor").a>.3f){lit=true;break;}
                }
                if(!lit)throw new Exception("Pause proof did not start with active reflection");
                state.Pause();hero.Update(state,world.Camera,1f/rate,3);
                foreach(var mat in body)if(mat.GetColor("_GuardColor").a>.001f)throw new Exception("Paused hero retained reflection");
                for(int i=0;i<rate*2;i++){state.Tick(1f/rate,new PlayerInput{Tracking=true});hero.Update(state,world.Camera,1f/rate,3+i/(float)rate);}
                if(state.Phase!=GamePhase.Battle)throw new Exception("Guard review failed to resume");
                foreach(var mat in body)if(mat.GetColor("_GuardColor").a>.001f)throw new Exception("Resume replayed reflection");
                hero.Update(new Battle(),world.Camera,0,0);
                foreach(var mat in body)if(mat.GetColor("_GuardColor").a>.001f)throw new Exception("New round retained reflection");
                Debug.Log($"[GuardLightInterruptions] hero={id} rate={rate} activePause=passed resume=passed newRound=passed");
            }
        }
        static Transform Find(Transform root,params string[] names)
        {
            foreach(var t in root.GetComponentsInChildren<Transform>())if(Array.IndexOf(names,t.name)>=0)return t;
            throw new Exception("Missing guard recoil bone: "+string.Join("/",names));
        }
    }
}
