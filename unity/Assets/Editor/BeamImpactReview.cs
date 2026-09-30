using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class BeamImpactReview
    {
        public static void After()=>Render("after");
        static void Render(string version)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(734);
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/beam-impact",version));Directory.CreateDirectory(folder+"/frames");
            File.Delete(folder+"/validation.txt");
            var world=new GameWorld();var state=new Battle(80);var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);
            var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});for(int i=0;i<(Battle.TransformationSeconds+0.6f)/(.02f);i++)state.Tick(.02f,new PlayerInput{Tracking=true});state.GiveInstructionTime(30);
            for(int n=0;n<15;n++){state.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});for(int i=0;i<25;i++)state.Tick(.02f,new PlayerInput{Tracking=true});}
            while(state.TryCue(out _)){}hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,1,0);
            var target=new RenderTexture(1280,720,24){antiAliasing=4};target.Create();world.Camera.targetTexture=target;world.Camera.aspect=16f/9;
            var csv=new StringBuilder("frame,phase,action,age,health,energy\n");float health=state.EnemyHealth;int impacts=0,volumeFrames=0;
            try
            {
                for(int f=0;f<360;f++)
                {
                    const float dt=1/60f;float time=f*dt;
                    state.Tick(world.BattleDelta(dt,state),new PlayerInput{Tracking=true,Beam=f==12});while(state.TryCue(out var cue))world.Cue(cue,state);
                    hero.Update(state,world.Camera,dt,time);enemy.Update(state,world.Camera,dt,time);
                    if(state.EnemyHealth<health){world.Hit(true,state);impacts++;}health=state.EnemyHealth;
                    world.Tick(state,dt,time);enemy.SetPresentationOpacity(world.EnemyOpacity);
                    if(world.BeamImpactVisible)volumeFrames++;
                    csv.AppendLine(FormattableString.Invariant($"{f},{state.Phase},{state.Action},{state.ActionAge:F5},{health},{state.Energy}"));
                    if(f%2==0)CharacterReview.Save(world.Camera,target,$"{folder}/frames/{f/2:D4}.png");
                }
                if(impacts!=1||state.EnemyHealth!=56||state.Punches!=15||state.Energy!=0)throw new Exception("Beam presentation changed combat");
                if(world.BeamImpactCount!=1||world.BeamImpactVisible||volumeFrames<90)throw new Exception("Missing, duplicated or uncleared beam volume");
                File.WriteAllText(folder+"/sequence.csv",csv.ToString());
                var sources=new StringBuilder();using(var sha=System.Security.Cryptography.SHA256.Create())
                    foreach(string file in new[]{"Scripts/Runtime/CombatVfx.cs","Scripts/Runtime/ImpactAtmosphere.cs","Scripts/Runtime/PresentationWarmup.cs","Scripts/Runtime/GameWorld.cs","Scripts/Runtime/BeamImpactVolume.cs","Resources/BeamImpactVolume.shader","Editor/BeamImpactReview.cs"})
                    {string path=Path.Combine(Application.dataPath,file);if(File.Exists(path))sources.AppendLine(file+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant());}
                File.WriteAllText(folder+"/sources.txt",sources.ToString());
                string report=$"{version}: frames=180 samples=360 impacts={impacts} health={health} punches={state.Punches} energy={state.Energy} volumeFrames={volumeFrames}";
                world.ResetPresentation();if(world.BeamImpactCount!=0||world.BeamImpactVisible)throw new Exception("Beam reset retained effects");
                ValidateVolume(folder);
                File.WriteAllText(folder+"/validation.txt",report);Debug.Log("[BeamImpactReview] "+report);
            }
            finally{world.Camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);}
        }
        static void ValidateVolume(string folder)
        {
            var root=new GameObject("Isolated volume depth review").transform;
            // Keep the arena alive for its normal cleanup path, but render only
            // the review layer, with no postprocessing or time-driven assets.
            const int layer=29;
            var camera=new GameObject("Volume depth camera").AddComponent<Camera>();camera.cullingMask=1<<layer;
            camera.transform.position=new Vector3(0,.3f,-5);camera.transform.rotation=Quaternion.identity;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.allowHDR=false;camera.depthTextureMode=DepthTextureMode.Depth;
            var target=new RenderTexture(320,180,24);target.Create();camera.targetTexture=target;
            var volume=new BeamImpactVolume(root);foreach(Transform child in root)child.gameObject.layer=layer;
            Texture2D Capture(string name)
            {
                camera.Render();RenderTexture.active=target;var texture=new Texture2D(320,180,TextureFormat.RGB24,false);
                texture.ReadPixels(new Rect(0,0,320,180),0,0);texture.Apply();File.WriteAllBytes(folder+"/"+name+".png",texture.EncodeToPNG());return texture;
            }
            int Difference(Texture2D a,Texture2D b)
            {
                var x=a.GetPixels32();var y=b.GetPixels32();int max=0;
                for(int i=0;i<x.Length;i++)max=Math.Max(max,Math.Max(Math.Abs(x[i].r-y[i].r),Math.Max(Math.Abs(x[i].g-y[i].g),Math.Abs(x[i].b-y[i].b))));return max;
            }
            var textures=new System.Collections.Generic.List<Texture2D>();Material material=null;
            try
            {
                volume.Hit(Vector3.zero,Vector3.forward);volume.Tick(.4f);
                var live=Capture("volume");textures.Add(live);volume.Tick(0);var frozen=Capture("volume-frozen");textures.Add(frozen);
                if(Difference(live,frozen)!=0)throw new Exception("Frozen beam volume changed without time");
                volume.Tick(2);var empty=Capture("volume-expired");textures.Add(empty);
                if(volume.Visible||Difference(live,empty)<30)throw new Exception("Volume failed to render or expire");
                material=new Material(Shader.Find("Standard"));material.color=new Color(.18f,.18f,.18f);
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Foreground occluder";wall.transform.SetParent(root,false);
                wall.transform.position=new Vector3(0,.3f,-2);wall.transform.localScale=new Vector3(12,12,.2f);wall.GetComponent<Renderer>().sharedMaterial=material;wall.layer=layer;
                var reference=Capture("depth-reference");textures.Add(reference);
                volume.Hit(Vector3.zero,Vector3.forward);volume.Tick(.4f);var covered=Capture("depth-occluded");textures.Add(covered);
                int error=Difference(reference,covered);if(error>1)throw new Exception("Beam smoke rendered over opaque foreground: "+error);
                volume.Clear();if(volume.Visible)throw new Exception("Volume survived Clear");
                foreach(var message in ShaderUtil.GetShaderMessages(Resources.Load<Shader>("BeamImpactVolume")))
                    if(message.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)throw new Exception(message.message);
                File.WriteAllText(folder+"/volume-validation.txt",$"frozenPixelError=0 depthPixelError={error} visibleContrast={Difference(live,empty)} expired=True cleared=True\n");
            }
            finally
            {
                camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);
                foreach(var texture in textures)UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(root.gameObject);UnityEngine.Object.DestroyImmediate(camera.gameObject);if(material)UnityEngine.Object.DestroyImmediate(material);
            }
        }
    }
}
