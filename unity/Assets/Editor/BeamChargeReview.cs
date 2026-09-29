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
    public static class BeamChargeReview
    {
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/beam-charge/inspection"));
        public static void Run()
        {
            FinisherReview.After();Directory.CreateDirectory(Folder);File.Delete(Folder+"/validation.txt");var report=new StringBuilder();
            foreach(string id in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})ValidateHero(id,report);
            ValidatePixels(report);
            var clip=GameAudio.CreateBeamGather();var samples=new float[clip.samples];clip.GetData(samples,0);
            float peak=0;double energy=0;foreach(float sample in samples){peak=Mathf.Max(peak,Mathf.Abs(sample));energy+=sample*sample;}
            if(peak>=.9f||energy/samples.Length<.002||Mathf.Abs(samples[0]-samples[samples.Length-1])>.035f)
                throw new Exception("Invalid or discontinuous gather sound");
            report.AppendLine($"Audio: loop={clip.length:F2}s peak={peak:F4} RMS={Math.Sqrt(energy/samples.Length):F4} seam={Mathf.Abs(samples[0]-samples[samples.Length-1]):F5}");
            UnityEngine.Object.DestroyImmediate(clip);
            File.WriteAllText(Folder+"/validation.txt",report.ToString());Debug.Log("[BeamChargeReview] PASS\n"+report);
        }
        static Battle Ready()
        {
            var state=new Battle(50);state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
            for(int i=0;i<120;i++)state.Tick(.02f,new PlayerInput{Tracking=true});
            for(int hit=0;hit<15;hit++){state.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});for(int i=0;i<22;i++)state.Tick(.02f,new PlayerInput{Tracking=true});}
            state.GiveInstructionTime(20);while(state.TryCue(out _)){}return state;
        }
        static void ValidateHero(string id,StringBuilder report)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var world=new GameWorld();var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);
            var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            var state=Ready();hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,1,0);
            var target=new RenderTexture(1280,720,24){antiAliasing=4};target.Create();world.Camera.targetTexture=target;world.Camera.aspect=16f/9;
            var captures=new bool[3];float[] moments={.32f,.72f,1.12f};int held=0,bridge=0;float maxOffset=0;
            try
            {
                for(int frame=0;frame<225;frame++)
                {
                    const float dt=1/60f;state.Tick(world.BattleDelta(dt,state),new PlayerInput{Tracking=true,Beam=frame==0});
                    while(state.TryCue(out var cue))world.Cue(cue,state);
                    hero.Update(state,world.Camera,dt,frame*dt);enemy.Update(state,world.Camera,dt,frame*dt);world.Tick(state,dt,frame*dt);enemy.SetPresentationOpacity(world.EnemyOpacity);
                    if(world.Closeup.Active&&world.Closeup.Age>.18f)
                    {
                        if(!world.ChargeVisible)throw new Exception("Missing held charge: "+id);held++;
                        float offset=Vector3.Distance(world.ChargeCenter,(hero.StrikeOrigin(HeroAction.LeftPunch)+hero.HandPosition)*.5f);
                        maxOffset=Mathf.Max(maxOffset,offset);
                        for(int i=0;i<moments.Length;i++)if(!captures[i]&&world.Closeup.Age>=moments[i])
                        {captures[i]=true;CharacterReview.Save(world.Camera,target,Folder+"/"+id+"-"+i+".png");}
                    }
                    if(!world.Closeup.Active&&state.Action==HeroAction.Beam&&state.ActionAge<BeamStream.LaunchSeconds)
                    {if(!world.ChargeVisible)throw new Exception("Dark gap before beam launch: "+id);bridge++;}
                }
                if(held<60||bridge<10||world.ChargeVisible||world.ChargePower!=0)throw new Exception("Uncleared charge or incomplete handoff: "+id);
                foreach(bool capture in captures)if(!capture)throw new Exception("Missing hero charge phase: "+id);
                // Interrupt a second charge during its closeup, then re-enter a
                // fresh round: no old volume, trail or local light may remain.
                state=Ready();world.ResetPresentation();state.Tick(.02f,new PlayerInput{Tracking=true,Beam=true});
                while(state.TryCue(out var cue))world.Cue(cue,state);
                hero.Update(state,world.Camera,.5f,0);world.Tick(state,.1f,0);world.Tick(state,.1f,0);
                if(!world.ChargeVisible)throw new Exception("No charge before interrupt");
                state.Pause();world.Tick(state,.02f,0);
                if(world.ChargeVisible||GameObject.Find("Energy spill").GetComponent<Light>().intensity!=0)throw new Exception("Paused charge remains visible");
                world.ResetPresentation();world.Tick(new Battle(),.02f,0);
                if(world.ChargeVisible)throw new Exception("New round retained charge");
                report.AppendLine($"{id}: heldFrames={held} launchBridgeFrames={bridge} maxHandOffset={maxOffset:F4} three-stages=rendered expire/pause/reset=passed");
            }
            finally{world.Camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);}
        }
        static void ValidatePixels(StringBuilder report)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("Isolated energy charge").transform;var charge=new BeamCharge(root);
            var camera=new GameObject("Energy depth inspection").AddComponent<Camera>();camera.enabled=false;
            camera.transform.position=new Vector3(0,0,-5);camera.transform.rotation=Quaternion.identity;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.depthTextureMode=DepthTextureMode.Depth;
            var target=new RenderTexture(320,180,24);target.Create();camera.targetTexture=target;
            var shots=new List<Texture2D>();Material wallMaterial=null;
            Texture2D Capture(string name)
            {
                camera.Render();RenderTexture.active=target;var texture=new Texture2D(320,180,TextureFormat.RGB24,false);
                texture.ReadPixels(new Rect(0,0,320,180),0,0);texture.Apply();shots.Add(texture);
                File.WriteAllBytes(Folder+"/"+name+".png",texture.EncodeToPNG());return texture;
            }
            int Changed(Texture2D a,Texture2D b,int threshold=0)
            {
                var x=a.GetPixels32();var y=b.GetPixels32();int count=0;
                for(int i=0;i<x.Length;i++)if(Math.Abs(x[i].r-y[i].r)>threshold||Math.Abs(x[i].g-y[i].g)>threshold||Math.Abs(x[i].b-y[i].b)>threshold)count++;
                return count;
            }
            try
            {
                var empty=Capture("empty");charge.Sample(true,.7f,Vector3.zero,Vector3.left*.25f,Vector3.right*.25f,Vector3.forward);
                var live=Capture("energy");charge.Sample(true,.7f,Vector3.zero,Vector3.left*.25f,Vector3.right*.25f,Vector3.forward);
                int frozen=Changed(live,Capture("frozen")),visible=Changed(empty,live,10);
                if(frozen!=0||visible<150)throw new Exception($"Energy invisible or unstable: visible={visible} frozen={frozen}");
                charge.Sample(true,.88f,Vector3.zero,Vector3.left*.25f,Vector3.right*.25f,Vector3.forward);
                int moved=Changed(live,Capture("converged"),10);if(moved<100)throw new Exception("Charge has no visible progression");
                charge.Clear();if(Changed(empty,Capture("cleared"))!=0)throw new Exception("Charge left pixels after Clear");
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(root,false);
                wall.transform.position=new Vector3(0,0,-2);wall.transform.localScale=new Vector3(10,10,.15f);
                wallMaterial=new Material(Shader.Find("Standard")){color=new Color(.15f,.22f,.28f)};wall.GetComponent<Renderer>().sharedMaterial=wallMaterial;
                var opaque=Capture("wall");charge.Sample(true,.7f,Vector3.zero,Vector3.left*.25f,Vector3.right*.25f,Vector3.forward);
                int leak=Changed(opaque,Capture("occluded"),1);if(leak!=0)throw new Exception("Energy renders through opaque character: "+leak);
                foreach(string name in new[]{"BeamChargeVolume","ChargeFilament"})
                    if(ShaderUtil.ShaderHasError(Resources.Load<Shader>(name)))throw new Exception("Invalid shader: "+name);
                report.AppendLine($"GPU: visiblePixels={visible} movingPixels={moved} zero-timePixels={frozen} foregroundLeakPixels={leak} clear=passed");
            }
            finally
            {
                camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);
                foreach(var texture in shots)UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(root.gameObject);UnityEngine.Object.DestroyImmediate(camera.gameObject);if(wallMaterial)UnityEngine.Object.DestroyImmediate(wallMaterial);
            }
        }
    }
}
