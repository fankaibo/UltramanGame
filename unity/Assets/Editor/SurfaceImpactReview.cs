using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class SurfaceImpactReview
    {
        public static void Release(){RosterPunchReview.After();CheckPunchRecovery();Validate();ContactReactionReview.After();CinematicFlashReview.Run();StaggerReview.CheckRecovery();RiggedReview.ValidateMotion();}
        public static void CheckPunchRecovery()
        {
            for(int h=1;h<HeroRoster.Count;h++)foreach(int fps in new[]{15,30,60})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                string id=HeroRoster.At(h).Id;var world=new GameWorld();var state=Ready(false);
                var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);
                var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                float dt=1f/fps,t=0;int samples=0;var hands=new Vector3[2];
                hero.Update(state,world.Camera,0,t);enemy.Update(state,world.Camera,0,t);
                for(int f=0;f<fps*5;f++)
                {
                    int cadence=Mathf.RoundToInt(fps*.6f),half=cadence/2;
                    state.Tick(dt,new PlayerInput{Tracking=true,LeftPunch=f%cadence==0,RightPunch=f%cadence==half});
                    hero.Update(state,world.Camera,dt,t);enemy.Update(state,world.Camera,dt,t);
                    if(state.ActionAge>=.1f&&state.ActionAge<=.3f&&
                        (state.Action==HeroAction.LeftPunch||state.Action==HeroAction.RightPunch))
                    {
                        // The opponent has just moved: settle its new target once,
                        // then ensure sampling without time cannot compound IK.
                        hero.Update(state,world.Camera,0,t);
                        hands[0]=hero.StrikeOrigin(HeroAction.LeftPunch);hands[1]=hero.HandPosition;
                        for(int i=0;i<5;i++)hero.Update(state,world.Camera,0,t);
                        if(Vector3.Distance(hands[0],hero.StrikeOrigin(HeroAction.LeftPunch))>.001f||
                            Vector3.Distance(hands[1],hero.HandPosition)>.001f)
                            throw new Exception($"Retargeted punch accumulates: {id}/{fps}");
                        samples++;
                    }
                    t+=dt;
                }
                if(state.Punches<10||samples<5)throw new Exception("Rapid punch sequence not exercised");
                state.Pause();hero.Update(state,world.Camera,dt,t);
                for(int f=0;f<fps*2;f++){state.Tick(dt,new PlayerInput{Tracking=true});hero.Update(state,world.Camera,dt,t);t+=dt;}
                if(state.Phase!=GamePhase.Battle)throw new Exception("Punch pause failed to resume");
                state=new Battle();var fresh=new AnimatedActor(id,world.HeroHome,world.EnemyHome);fresh.SetOpponent(enemy);
                for(int f=0;f<fps;f++){hero.Update(state,world.Camera,dt,t);fresh.Update(state,world.Camera,dt,t);t+=dt;}
                if(Vector3.Distance(hero.StrikeOrigin(HeroAction.LeftPunch),fresh.StrikeOrigin(HeroAction.LeftPunch))>.001f||
                    Vector3.Distance(hero.HandPosition,fresh.HandPosition)>.001f)
                    throw new Exception($"Punch correction remained after restart: {id}/{fps}");
                Debug.Log($"[RosterPunchRecovery] {id}/{fps}Hz repeatedSamples={samples} rapidCombo=passed pauseResume=passed newRound=passed");
            }
        }
        public static void Validate()
        {
            var shader=Resources.Load<Shader>("KaijuSurface");
            if(!shader||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new Exception("Kaiju surface shader failed");
            for(int hero=0;hero<HeroRoster.Count;hero++)for(int kind=0;kind<3;kind++)Run(HeroRoster.At(hero).Id,kind,false);
            Run("Tiga",0,true);Run("Tiga",2,true);
        }
        static Battle Ready(bool beam)
        {
            var state=new Battle();state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
            for(int i=0;i<120;i++)state.Tick(.02f,new PlayerInput{Tracking=true});
            if(beam)for(int hit=0;hit<15;hit++)
            {state.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});for(int f=0;f<26;f++)state.Tick(.02f,new PlayerInput{Tracking=true});}
            state.GiveInstructionTime(20);while(state.TryCue(out _)){}return state;
        }
        static void Run(string id,int kind,bool interrupt)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var world=new GameWorld();var state=Ready(kind==2);var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);
            var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);
            var materialList=new List<Material>();foreach(var skin in enemy.Root.GetComponentsInChildren<Renderer>())foreach(var m in skin.sharedMaterials)if(m.HasProperty("_ImpactPoint")&&!materialList.Contains(m))materialList.Add(m);
            if(materialList.Count==0)throw new Exception("Missing local impact material");
            var materials=materialList.ToArray();var mat=materials[0];
            Transform chest=null;foreach(var bone in enemy.Root.GetComponentsInChildren<Transform>())if(bone.name=="bip_spine_2")chest=bone;
            if(!chest)throw new Exception("Missing chest binding");
            var camera=new GameObject("Isolated material review").AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<ContactShadows.ActorLayer;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.allowHDR=true;camera.aspect=1;camera.fieldOfView=37;
            camera.transform.position=world.EnemyHome-world.BattleAxis*6.9f+Vector3.up*2.5f;camera.transform.LookAt(world.EnemyHome+Vector3.up*1.8f);
            // Measure this shader separately from temporal shadow filtering.
            // Full-scene captures above retain the actual game shadows.
            foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))light.shadows=LightShadows.None;
            var target=new RenderTexture(384,384,24);target.Create();camera.targetTexture=target;
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/surface-impact/emission"));Directory.CreateDirectory(folder);
            string name=id+"-"+(kind==0?"left":kind==1?"right":"beam")+(interrupt?"-interrupted":"");File.Delete(folder+"/"+name+".txt");
            float health=state.EnemyHealth,age=-1,bindingError=0;Vector3 local=default;bool peak=false,decayed=false;int changed=0;float lowerFraction=0;
            try
            {
                for(int f=0;f<150;f++)
                {
                    const float dt=1/60f;float t=f*dt;if(age>=0)age+=dt;
                    state.Tick(dt,new PlayerInput{Tracking=true,LeftPunch=f==6&&kind==0,RightPunch=f==6&&kind==1,Beam=f==6&&kind==2});
                    hero.Update(state,world.Camera,dt,t);enemy.Update(state,world.Camera,dt,t);
                    if(state.EnemyHealth<health)
                    {
                        world.Hit(kind==2,state);age=0;var point=(Vector3)mat.GetVector("_ImpactPoint");local=chest.InverseTransformPoint(point);
                        Vector3 contact=kind==2?enemy.BeamContact:hero.StrikeOrigin(state.Action);
                        if(Vector3.Distance(point,contact)>.001f)throw new Exception("Skin light missed actual contact");
                    }
                    health=state.EnemyHealth;world.Tick(state,dt,t);
                    if(age>=0)bindingError=Mathf.Max(bindingError,Vector3.Distance((Vector3)mat.GetVector("_ImpactPoint"),chest.TransformPoint(local)));
                    if(!peak&&age>=(kind==2?.2f:.09f))
                    {
                        peak=true;var color=mat.GetColor("_ImpactColor");var point=mat.GetVector("_ImpactPoint");
                        enemy.Update(state,world.Camera,0,t);
                        if(Vector4.Distance(point,mat.GetVector("_ImpactPoint"))>.001f||Mathf.Abs(color.a-mat.GetColor("_ImpactColor").a)>.001f)throw new Exception("Zero-time sample advances light");
                        if(interrupt)
                        {
                            state.Pause();enemy.Update(state,world.Camera,dt,t);
                            if(mat.GetColor("_ImpactColor").a>.001f)throw new Exception("Pause left skin emission");
                            for(int i=0;i<80;i++){state.Tick(dt,new PlayerInput{Tracking=true});enemy.Update(state,world.Camera,dt,t+i*dt);}
                            if(mat.GetColor("_ImpactColor").a>.001f)throw new Exception("Resume replayed skin emission");
                            enemy.Update(new Battle(),world.Camera,dt,t+2);
                            if(mat.GetColor("_ImpactColor").a>.001f)throw new Exception("New round retained skin emission");
                            decayed=true;break;
                        }
                        (changed,lowerFraction)=Compare(camera,target,hero,materials,world.EnemyHome,folder+"/"+name);
                        if(changed<30||lowerFraction>.01f)throw new Exception($"Unbounded or invisible skin light {name}: pixels={changed} lower={lowerFraction} point={mat.GetVector("_ImpactPoint")} chest={enemy.BeamContact} hero={hero.Root.position}");
                    }
                    if(peak&&!decayed&&age>(kind==2?1.08f:.30f))
                    {
                        if(mat.GetColor("_ImpactColor").a>.001f)throw new Exception("Surface light did not fade");
                        var difference=Compare(camera,target,hero,materials,world.EnemyHome,null);
                        if(difference.Item1!=0)throw new Exception("Faded impact still changes skin pixels");
                        decayed=true;
                    }
                }
                if(!peak||!decayed||bindingError>.001f)throw new Exception($"Missing surface lifetime/binding proof {name}: error={bindingError}");
                string result=$"{name}: visibleEmissionPixels={changed} belowWaistEmissionFraction={lowerFraction:F6} bindingError={bindingError:F6} decay=passed zeroTime=passed";
                File.WriteAllText(folder+"/"+name+".txt",result);Debug.Log("[SurfaceImpactReview] "+result);
            }
            finally{camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);}
        }
        static (int,float) Compare(Camera camera,RenderTexture target,AnimatedActor hero,Material[] materials,Vector3 home,string prefix)
        {
            var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);var active=RenderTexture.active;var colors=new Color[materials.Length];bool fog=RenderSettings.fog;
            for(int i=0;i<materials.Length;i++)colors[i]=materials[i].GetColor("_ImpactColor");
            hero.Root.gameObject.SetActive(false);
            Color32[] Read(string suffix)
            {camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();if(prefix!=null)File.WriteAllBytes(prefix+suffix+".png",image.EncodeToPNG());return image.GetPixels32();}
            try
            {
                if(prefix!=null)Read("-lit");
                // Use the emission produced by the real surf function. Its
                // final-color inspection path bypasses lighting, not the mask,
                // normals, texture modulation or spatial contact calculation.
                RenderSettings.fog=false;foreach(var mat in materials)mat.SetFloat("_EmissionAudit",1);
                camera.Render();camera.Render();
                var on=Read("-on");foreach(var mat in materials)mat.SetColor("_ImpactColor",Color.clear);var off=Read("-off");var repeat=Read("-off-repeat");
                long repeatNoise=0;int repeatMax=0;for(int i=0;i<off.Length;i++){int d=Math.Abs(off[i].r-repeat[i].r)+Math.Abs(off[i].g-repeat[i].g)+Math.Abs(off[i].b-repeat[i].b);repeatNoise+=d;repeatMax=Math.Max(repeatMax,d);}
                if(repeatNoise!=0)throw new Exception("Emission render changed without input: "+repeatMax);
                int changed=0;double total=0,lower=0;int waist=(int)(camera.WorldToViewportPoint(home+Vector3.up*1.3f).y*target.height);
                for(int i=0;i<on.Length;i++)
                {
                    int difference=Math.Abs(on[i].r-off[i].r)+Math.Abs(on[i].g-off[i].g)+Math.Abs(on[i].b-off[i].b);
                    if(difference>12)changed++;total+=difference;if(i/target.width<waist)lower+=difference;
                }
                return (changed,(float)(lower/Math.Max(1,total)));
            }
            finally{for(int i=0;i<materials.Length;i++){materials[i].SetFloat("_EmissionAudit",0);materials[i].SetColor("_ImpactColor",colors[i]);}RenderSettings.fog=fog;hero.Root.gameObject.SetActive(true);RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(image);}
        }
    }
}
