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
    public static class DefeatImpactReview
    {
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/defeat-climax"));
        public static void Checks()
        {
            Directory.CreateDirectory(Folder);File.Delete(Folder+"/checks.txt");var report=new StringBuilder();
            var shader=Resources.Load<Shader>("DefeatImpact");
            if(!shader||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new Exception("Defeat shader unavailable");
            Occlusion(report);
            foreach(int rate in new[]{15,30,60})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var world=new GameWorld();var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                int objects=world.Camera.GetComponentsInParent<Transform>(true).Length;
                int volumes=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
                float dt=1f/rate;
                for(int round=0;round<2;round++)
                {
                    world.ResetPresentation();var waiting=new Battle();hero.Update(waiting,world.Camera,0,0);enemy.Update(waiting,world.Camera,0,0);world.Tick(waiting,0,0);
                    var state=Won();int signals=0;bool observed=false;float first=-1;
                    for(int f=0;f<rate*6;f++)
                    {
                        float age=(f+1)*dt;
                        hero.Update(state,world.Camera,dt,age);enemy.Update(state,world.Camera,dt,age);world.Tick(state,dt,age);
                        if(world.MonsterLanded){signals++;first=age;if(!world.DefeatImpactVisible)throw new Exception("Landing lacks dust burst");}
                        if(age<VictoryMotion.LandingSeconds-.001f&&world.DefeatImpactVisible)throw new Exception("Dust precedes ground contact");
                        if(world.DefeatImpactVisible)
                        {
                            observed=true;float held=world.DefeatImpactAge;
                            world.Tick(state,0,age);
                            if(world.MonsterLanded||world.DefeatImpactStarts!=1||world.DefeatImpactAge!=held)throw new Exception("Frozen render restarts or advances landing");
                        }
                        if(age>4&&world.DefeatImpactVisible)throw new Exception("Dust leaks into celebration/photo");
                    }
                    if(signals!=1||world.DefeatImpactStarts!=1||!observed||first<VictoryMotion.LandingSeconds-.001f||first>VictoryMotion.LandingSeconds+dt+.001f)throw new Exception("Landing event missing or repeated");
                }
                // Cancel during visible dust, not only after the effect expired.
                world.ResetPresentation();var wait=new Battle();hero.Update(wait,world.Camera,0,0);enemy.Update(wait,world.Camera,0,0);world.Tick(wait,0,0);
                var won=Won();
                for(int f=0;f<Mathf.CeilToInt(rate*1.5f);f++)
                {hero.Update(won,world.Camera,dt,f*dt);enemy.Update(won,world.Camera,dt,f*dt);world.Tick(won,dt,f*dt);}
                if(!world.DefeatImpactVisible)throw new Exception("Mid-effect reset was not exercised");
                world.ResetPresentation();
                if(world.DefeatImpactVisible||world.DefeatImpactStarts!=0||GameObject.Find("Defeat contact light").GetComponent<Light>().enabled)throw new Exception("Restart retains dust/light");
                if(volumes!=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length||objects!=world.Camera.GetComponentsInParent<Transform>(true).Length)throw new Exception("Landing allocates scene objects");
                report.AppendLine($"{rate}Hz two-victories=passed contact-once=passed frozen=passed mid-effect-reset=passed photo-clear=passed pool=passed");
            }
            Audio(report);
            File.WriteAllText(Folder+"/checks.txt",report.ToString());
            using(var sha=System.Security.Cryptography.SHA256.Create())
                File.WriteAllText(Folder+"/checks-source.txt",BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Application.dataPath,"Editor/DefeatImpactReview.cs")))).Replace("-","").ToLowerInvariant());
            Debug.Log("[DefeatImpactReview] "+report);
        }
        static Battle Won()
        {
            var state=new Battle(10);state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
            for(int f=0;f<(Battle.TransformationSeconds+.2f)/.02f;f++)state.Tick(.02f,new PlayerInput{Tracking=true});
            for(int i=0;i<10;i++){state.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});for(int f=0;f<22;f++)state.Tick(.02f,new PlayerInput{Tracking=true});}
            while(state.TryCue(out _)){}if(state.Phase!=GamePhase.Victory)throw new Exception("Test did not reach victory");return state;
        }
        static void Occlusion(StringBuilder report)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);RenderSettings.fog=false;
            var root=new GameObject("Dust GPU probe").transform;var effect=new DefeatImpact(root);
            var camera=new GameObject("Probe camera").AddComponent<Camera>();camera.enabled=false;camera.transform.position=new Vector3(0,1,-6);camera.depthTextureMode=DepthTextureMode.Depth;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.fieldOfView=36;camera.aspect=1;
            var rt=new RenderTexture(192,192,24){antiAliasing=1};rt.Create();camera.targetTexture=rt;
            var image=new Texture2D(192,192,TextureFormat.RGB24,false);
            Color32[] Read(string name){camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,192,192),0,0);image.Apply();File.WriteAllBytes(Folder+"/gpu-"+name+".png",image.EncodeToPNG());return image.GetPixels32();}
            try
            {
                var empty=Read("empty");effect.Begin(Vector3.zero,false);effect.Tick(.32f);var live=Read("live");
                int changed=Difference(empty,live);if(changed<500)throw new Exception("Smoke shader is blank");
                effect.Tick(0);if(Difference(live,Read("frozen"))!=0)throw new Exception("Frozen GPU image changes");
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(0,1,-2.8f);wall.transform.localScale=new Vector3(5,5,.2f);
                var material=RuntimeResources.Own(root,new Material(Shader.Find("Standard")));material.color=new Color(.22f,.32f,.42f);material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",new Color(.1f,.2f,.3f));wall.GetComponent<Renderer>().sharedMaterial=material;
                var covered=Read("covered");effect.Clear();var wallOnly=Read("wall-only");
                int wrong=Difference(covered,wallOnly);if(wrong>0)throw new Exception("Smoke draws through opaque foreground: "+wrong);
                wall.SetActive(false);effect.Begin(Vector3.zero,false);effect.Tick(DefeatImpact.Lifetime);
                if(effect.Visible||effect.LightIntensity!=0||Difference(empty,Read("cleared"))!=0)throw new Exception("Expired smoke leaves pixels or light");
                report.AppendLine($"GPU visiblePixels={changed} foregroundMismatch={wrong} frozen=passed depth-occlusion=passed fully-cleared=passed");
            }
            finally{camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);}
        }
        static int Difference(Color32[] a,Color32[] b)
        {int n=0;for(int i=0;i<a.Length;i++)if(Mathf.Abs(a[i].r-b[i].r)>2||Mathf.Abs(a[i].g-b[i].g)>2||Mathf.Abs(a[i].b-b[i].b)>2)n++;return n;}
        static void Audio(StringBuilder report)
        {
            var clip=GameAudio.CreateDefeatSurge();var samples=new float[clip.samples];clip.GetData(samples,0);float peak=0;double energy=0;
            foreach(float v in samples){if(float.IsNaN(v)||float.IsInfinity(v))throw new Exception("Non-finite audio");peak=Mathf.Max(peak,Mathf.Abs(v));energy+=v*v;}
            double rms=Math.Sqrt(energy/samples.Length);if(peak>.55f||rms<.01||rms>.10||Mathf.Abs(samples[0])>.001f||Mathf.Abs(samples[samples.Length-1])>.001f)throw new Exception("Audio silent, clipped or discontinuous at ends");
            using(var file=new BinaryWriter(File.Create(Folder+"/landing-surge.wav")))
            {file.Write(Encoding.ASCII.GetBytes("RIFF"));file.Write(36+samples.Length*2);file.Write(Encoding.ASCII.GetBytes("WAVEfmt "));file.Write(16);file.Write((short)1);file.Write((short)1);file.Write(clip.frequency);file.Write(clip.frequency*2);file.Write((short)2);file.Write((short)16);file.Write(Encoding.ASCII.GetBytes("data"));file.Write(samples.Length*2);foreach(float v in samples)file.Write((short)Mathf.RoundToInt(v*32767));}
            UnityEngine.Object.DestroyImmediate(clip);report.AppendLine($"audio peak={peak:F5} rms={rms:F5} finite=passed endpoints=passed");
        }
    }
}
