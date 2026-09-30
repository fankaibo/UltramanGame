using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    // Read actual forward lighting on the skinned mesh; emission-only checks
    // cannot detect an over-wide point light washing out the whole character.
    public static class ImpactSpillReview
    {
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/impact-spill"));
        public static void Before()=>Capture("before");
        public static void After(){Capture("after");Lifecycle();ExchangeReview.After();}
        static Battle Ready()
        {
            var state=new Battle();state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
            for(int i=0;i<(Battle.TransformationSeconds+0.2f)/(.02f);i++)state.Tick(.02f,new PlayerInput{Tracking=true});
            state.GiveInstructionTime(20);while(state.TryCue(out _)){}return state;
        }
        static void Capture(string version)
        {
            string folder=Folder+"/"+version+"/lighting";Directory.CreateDirectory(folder);
            var report=new StringBuilder("hero,hand,lit_pixels,outside_chest_fraction,peak_delta,range,intensity\n");
            foreach(string id in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})foreach(bool left in new[]{true,false})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(932);
                var world=new GameWorld();var state=Ready();
                var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);
                var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,1,0);
                float health=state.EnemyHealth;bool hit=false;
                for(int frame=0;frame<40;frame++)
                {
                    const float dt=1/60f;float t=frame*dt;
                    state.Tick(world.BattleDelta(dt,state),new PlayerInput{Tracking=true,LeftPunch=frame==0&&left,RightPunch=frame==0&&!left});
                    while(state.TryCue(out var cue))world.Cue(cue,state);
                    hero.Update(state,world.Camera,dt,t);enemy.Update(state,world.Camera,dt,t);
                    hit=state.EnemyHealth<health;if(hit)world.Hit(false,state);health=state.EnemyHealth;world.Tick(state,dt,t);
                    if(hit)break;
                }
                if(!hit)throw new Exception("Punch never reached contact");
                string name=id+(left?"-left":"-right");
                var target=new RenderTexture(1280,720,24){antiAliasing=4};target.Create();
                world.Camera.targetTexture=target;world.Camera.aspect=16f/9;
                CharacterReview.Save(world.Camera,target,folder+"/"+name+"-scene.png");
                world.Camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);
                var spill=GameObject.Find("Impact spill").GetComponent<Light>();
                // Isolate this light from Unity's automatic per-object light
                // selection: turning it off must not promote another point light.
                // The scene image above retains the actual gameplay lighting.
                foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                {light.shadows=LightShadows.None;if(light!=spill)light.enabled=false;}
                hero.Root.gameObject.SetActive(false);
                var camera=new GameObject("Contact spill inspection").AddComponent<Camera>();camera.enabled=false;
                camera.cullingMask=1<<ContactShadows.ActorLayer;camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=Color.black;camera.allowHDR=true;camera.aspect=1;camera.fieldOfView=37;
                camera.transform.position=world.EnemyHome-world.BattleAxis*6.9f+Vector3.up*2.5f;
                camera.transform.LookAt(world.EnemyHome+Vector3.up*1.8f);
                target=new RenderTexture(512,512,24);target.Create();camera.targetTexture=target;
                var image=new Texture2D(512,512,TextureFormat.RGB24,false);
                Color32[] Read(string suffix)
                {camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,512,512),0,0);image.Apply();File.WriteAllBytes(folder+"/"+name+suffix+".png",image.EncodeToPNG());return image.GetPixels32();}
                try
                {
                    var on=Read("-on");float power=spill.intensity;spill.intensity=0;
                    var off=Read("-off");var repeat=Read("-repeat");
                    int low=Mathf.FloorToInt(camera.WorldToViewportPoint(enemy.Root.position+Vector3.up*1.3f).y*512);
                    int high=Mathf.CeilToInt(camera.WorldToViewportPoint(enemy.Root.position+Vector3.up*2.95f).y*512);
                    long total=0,outside=0;int count=0,peak=0;
                    for(int p=0;p<on.Length;p++)
                    {
                        if(!off[p].Equals(repeat[p]))throw new Exception("Lighting comparison changed without input");
                        int delta=Math.Abs(on[p].r-off[p].r)+Math.Abs(on[p].g-off[p].g)+Math.Abs(on[p].b-off[p].b);
                        if(delta>12)count++;peak=Math.Max(peak,delta);total+=delta;if(p/512<low||p/512>high)outside+=delta;
                    }
                    float fraction=outside/(float)Math.Max(1,total);
                    if(version=="after"&&(count<30||fraction>.05f))throw new Exception($"Missing or unbounded spill {name}: pixels={count} outside={fraction:F5} low/high={low}/{high} actor={enemy.Root.position:F3} light={spill.transform.position:F3}");
                    report.AppendLine($"{id},{(left?"left":"right")},{count},{fraction:F6},{peak},{spill.range:F3},{power:F3}");
                }
                finally{camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);}
            }
            File.WriteAllText(folder+"/measurements.csv",report.ToString());
            File.WriteAllText(folder+"/source.sha256",BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(Path.Combine(Application.dataPath,"Scripts/Runtime/CombatVfx.cs")))).Replace("-","").ToLowerInvariant());
            Debug.Log("[ImpactSpillReview] "+version+"\n"+report);
        }
        static void Lifecycle()
        {
            var result=new StringBuilder();
            foreach(int hz in new[]{15,30,60})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var root=new GameObject("Spill lifecycle").transform;var fx=new CombatVfx(root);var camera=new GameObject("Review camera").AddComponent<Camera>();
                var light=GameObject.Find("Impact spill").GetComponent<Light>();var state=Ready();
                void Tick(float dt)=>fx.Tick(state,camera,dt,Vector3.zero,Vector3.forward,Vector3.zero,Vector3.forward,false,0,false,Vector3.forward,0,Vector3.zero,Vector3.zero);
                foreach(int kind in new[]{0,1,2,3})
                {
                    fx.Clear();fx.Impact(Vector3.zero,kind==1,blocked:kind==2,hurt:kind==3);Tick(1f/hz);
                    if(light.intensity<=0)throw new Exception("Missing impact spill");
                    float power=light.intensity;Tick(0);if(light.intensity!=power)throw new Exception("Frozen light advances");
                    for(int f=0;f<hz;f++)Tick(1f/hz);
                    if(light.intensity!=0)throw new Exception("Contact spill did not decay");
                }
                fx.Impact(Vector3.zero,false);Tick(1f/hz);state.Pause();Tick(1f/hz);
                if(light.intensity!=0)throw new Exception("Paused light remains");
                state=Ready();fx.Impact(Vector3.zero,false);Tick(1f/hz);fx.Clear();
                if(light.intensity!=0)throw new Exception("Reset light remains");
                result.AppendLine($"{hz}Hz punch/beam/block/hurt=visible frozen=stable decay=passed pause=clear reset=clear");
            }
            File.WriteAllText(Folder+"/lifecycle.txt",result.ToString());Debug.Log("[ImpactSpillLifecycle] "+result);
        }
    }
}
