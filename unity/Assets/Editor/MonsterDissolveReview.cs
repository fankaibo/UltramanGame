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
    public static class MonsterDissolveReview
    {
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/monster-dissolve"));
        public static void Release(){Checks();VictoryReview.After();}
        static Battle Won()
        {
            var state=new Battle(10);state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
            for(int i=0;i<120;i++)state.Tick(.02f,new PlayerInput{Tracking=true});
            for(int hit=0;hit<10;hit++)
            {state.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});for(int i=0;i<22;i++)state.Tick(.02f,new PlayerInput{Tracking=true});}
            while(state.TryCue(out _)){}if(state.Phase!=GamePhase.Victory)throw new Exception("Missing test victory");return state;
        }
        public static void Checks()
        {
            Directory.CreateDirectory(Folder);File.Delete(Folder+"/checks.txt");var report=new StringBuilder();
            foreach(string name in new[]{"KaijuSurface","ShadowSilhouette","DissolveMotes"})
            {var shader=Resources.Load<Shader>(name);if(!shader||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new Exception("Invalid departure shader: "+name);}
            GpuField(report);
            foreach(int rate in new[]{15,30,60})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var world=new GameWorld();var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                float dt=1f/rate;int signals=0,peak=0,objects=enemy.Root.GetComponentsInChildren<Transform>(true).Length;
                var state=Won();hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,0,0);
                // Cancel while the surface and motes are both visible.
                for(int f=0;f<Mathf.CeilToInt(rate*3.1f);f++)
                {hero.Update(state,world.Camera,dt,f*dt);enemy.Update(state,world.Camera,dt,f*dt);world.Tick(state,dt,f*dt);if(world.MonsterDissolving)signals++;peak=Mathf.Max(peak,enemy.DissolveMotes);}
                if(signals!=1||enemy.DissolveStarts!=1||enemy.DissolveMotes==0)throw new Exception("Partial departure was not exercised");
                int heldMotes=enemy.DissolveMotes;enemy.Update(state,world.Camera,0,3.1f);world.Tick(state,0,3.1f);
                if(heldMotes!=enemy.DissolveMotes||enemy.DissolveStarts!=1||world.MonsterDissolving)throw new Exception("Frozen clock changes/replays departure");
                world.ResetPresentation();state=new Battle();hero.Update(state,world.Camera,0,3.2f);enemy.Update(state,world.Camera,0,3.2f);world.Tick(state,0,3.2f);
                if(enemy.DissolveMotes!=0||world.MonsterDissolving)throw new Exception("Restart retained light particles");
                foreach(var skin in enemy.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                {if(!skin.enabled)throw new Exception("Restart retained hidden skin");foreach(var mat in skin.sharedMaterials)if(mat.GetFloat("_DissolveAmount")!=0)throw new Exception("Restart retained clipped surface");}
                state=Won();hero.Update(state,world.Camera,0,4);enemy.Update(state,world.Camera,0,4);world.Tick(state,0,4);
                for(int f=0;f<rate*6;f++)
                {
                    float age=(f+1)*dt;hero.Update(state,world.Camera,dt,4+age);enemy.Update(state,world.Camera,dt,4+age);world.Tick(state,dt,4+age);
                    if(world.MonsterDissolving)signals++;peak=Mathf.Max(peak,enemy.DissolveMotes);
                    if(age<VictoryMotion.FadeStartSeconds-.02f&&enemy.DissolveMotes>0)throw new Exception("Departure starts before landing and hero turn");
                    if(age>VictoryMotion.FadeStartSeconds+.10f&&age<VictoryMotion.FadeStartSeconds+VictoryMotion.FadeSeconds-.10f)
                        foreach(var skin in enemy.Root.GetComponentsInChildren<SkinnedMeshRenderer>())foreach(var mat in skin.sharedMaterials)
                            if(mat.GetFloat("_ZWrite")!=1||mat.color.a<.999f)throw new Exception("Departure reverted to transparent fade");
                }
                if(signals!=2||enemy.DissolveStarts!=2||peak<100||enemy.DissolveMotes!=0||enemy.Root.GetComponentsInChildren<Transform>(true).Length!=objects)throw new Exception("Departure replay/expiry/pool failed");
                foreach(var skin in enemy.Root.GetComponentsInChildren<SkinnedMeshRenderer>())if(skin.enabled)throw new Exception("Skin visible at automatic photo time");
                foreach(var renderer in enemy.Root.GetComponentsInChildren<MeshRenderer>())if(renderer.enabled)throw new Exception("Motes visible at automatic photo time");
                report.AppendLine($"{rate}Hz starts={enemy.DissolveStarts} signals={signals} peakMotes={peak} frozen=passed mid-dissolve-reset=passed second-victory=passed opaque-depth=passed photo-clear=passed pool=passed");
            }
            var clip=GameAudio.CreateDissolveShimmer();var samples=new float[clip.samples];clip.GetData(samples,0);double energy=0;float amplitude=0;
            foreach(float v in samples){if(float.IsNaN(v)||float.IsInfinity(v))throw new Exception("Invalid shimmer");amplitude=Mathf.Max(amplitude,Mathf.Abs(v));energy+=v*v;}
            double rms=Math.Sqrt(energy/samples.Length);if(amplitude>.3f||rms<.02||rms>.10)throw new Exception("Clipped or silent shimmer");
            using(var file=new BinaryWriter(File.Create(Folder+"/departure-shimmer.wav")))
            {
                file.Write(Encoding.ASCII.GetBytes("RIFF"));file.Write(36+samples.Length*2);file.Write(Encoding.ASCII.GetBytes("WAVEfmt "));file.Write(16);file.Write((short)1);file.Write((short)1);file.Write(clip.frequency);file.Write(clip.frequency*2);file.Write((short)2);file.Write((short)16);file.Write(Encoding.ASCII.GetBytes("data"));file.Write(samples.Length*2);foreach(float v in samples)file.Write((short)Mathf.RoundToInt(v*32767));
            }
            UnityEngine.Object.DestroyImmediate(clip);report.AppendLine($"audio peak={amplitude:F5} rms={rms:F5} finite=passed");
            File.WriteAllText(Folder+"/checks.txt",report.ToString());Debug.Log("[MonsterDissolveChecks] "+report);
        }
        static void GpuField(StringBuilder report)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);RenderSettings.fog=false;
            var camera=new GameObject("Departure GPU probe").AddComponent<Camera>();camera.enabled=false;camera.transform.position=new Vector3(0,1,-4);camera.orthographic=true;camera.orthographicSize=1.25f;camera.aspect=1;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);quad.transform.position=Vector3.up;quad.transform.localScale=new Vector3(2,2,1);
            var material=new Material(Resources.Load<Shader>("KaijuSurface"));quad.GetComponent<Renderer>().sharedMaterial=material;
            material.SetFloat("_EmissionAudit",1);material.SetColor("_EmissionColor",Color.white*.25f);material.SetVector("_DissolveBounds",new Vector4(0,2,0,0));
            var target=new RenderTexture(128,128,24){antiAliasing=1};target.Create();camera.targetTexture=target;
            var image=new Texture2D(128,128,TextureFormat.RGB24,false);
            try
            {
                foreach(bool shadow in new[]{false,true})
                {
                    int last=10000,total=0;
                    foreach(float amount in new[]{0,.25f,.5f,.75f,1})
                    {
                        material.SetFloat("_DissolveAmount",amount);
                        if(shadow)camera.RenderWithShader(Resources.Load<Shader>("ShadowSilhouette"),"");else camera.Render();
                        RenderTexture.active=target;image.ReadPixels(new Rect(0,0,128,128),0,0);image.Apply();var pixels=image.GetPixels32();int visible=0,wrong=0;
                        for(int y=16;y<112;y++)for(int x=16;x<112;x++)
                        {
                            var p=camera.ViewportToWorldPoint(new Vector3((x+.5f)/128,(y+.5f)/128,4));
                            float field=MonsterDissolve.Field(p,new Vector2(0,2));bool expected=amount<=0||field>amount;bool actual=pixels[y*128+x].r>12;
                            if(actual)visible++;if(Mathf.Abs(field-amount)>.006f&&expected!=actual)wrong++;
                        }
                        if(wrong>0||visible>last||amount==1&&visible!=0||amount==0&&visible!=96*96)throw new Exception($"Surface/shadow GPU differs from mote field: shadow={shadow} amount={amount} mismatch={wrong} visible={visible}");
                        last=visible;total+=visible;
                        File.WriteAllBytes(Folder+"/probe-"+(shadow?"shadow":"surface")+"-"+amount.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+".png",image.EncodeToPNG());
                    }
                    report.AppendLine($"GPU {(shadow?"contact-shadow":"surface")} 5 stages matches particle origins; visible total={total}; monotonic=passed fully-cleared=passed");
                }
            }
            finally{camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(material);}
        }
    }
}
