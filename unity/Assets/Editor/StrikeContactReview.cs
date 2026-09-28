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
    public static class StrikeContactReview
    {
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/strike-contact"));
        public static void Release(){ComboStrikeReview.After();Check();Roster();}
        public static void Check()
        {
            Directory.CreateDirectory(Folder+"/inspection");File.Delete(Folder+"/validation.txt");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var shader=Resources.Load<Shader>("StrikeContact");
            if(!shader||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new Exception("Invalid contact shader");
            var root=new GameObject("Contact review").transform;var burst=new StrikeContactBurst(root);
            var camera=new GameObject("Contact camera").AddComponent<Camera>();camera.transform.position=new Vector3(0,0,-5);camera.orthographic=true;camera.orthographicSize=1.1f;camera.aspect=1;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            var target=new RenderTexture(256,256,24);target.Create();camera.targetTexture=target;
            var pixels=new Texture2D(256,256,TextureFormat.RGB24,false);var report=new StringBuilder();
            Color32[] Read(string name)
            {camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,256,256),0,0);pixels.Apply();File.WriteAllBytes(Folder+"/inspection/"+name+".png",pixels.EncodeToPNG());return pixels.GetPixels32();}
            int Changed(Color32[] a,Color32[] b,int threshold=0)
            {int count=0;for(int i=0;i<a.Length;i++)if(Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b)>threshold)count++;return count;}
            try
            {
                var empty=Read("empty");burst.Hit(Vector3.zero,Vector3.right,false);burst.Tick(camera,.03f);
                var early=Read("contact");int visible=Changed(empty,early,12);
                burst.Tick(camera,0);int repeat=Changed(early,Read("repeat"));
                burst.Tick(camera,.11f);int moving=Changed(early,Read("separating"),9);
                if(visible<150||repeat!=0||moving<150)throw new Exception($"Missing/unstable contact: pixels={visible} repeat={repeat} changing={moving}");
                burst.Tick(camera,.2f);if(burst.ActiveCount!=0||Changed(empty,Read("expired"))!=0)throw new Exception("Punch contact does not expire");
                burst.Hit(Vector3.zero,Vector3.right,true);burst.Tick(camera,.03f);var strong=Read("combo");int comboPixels=Changed(empty,strong,12);
                if(comboPixels<=visible)throw new Exception("Combo contact does not read stronger");
                var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.transform.position=new Vector3(0,0,-2);blocker.transform.localScale=new Vector3(5,5,.1f);
                var opaque=new Material(Shader.Find("Unlit/Color")){color=new Color(.2f,.3f,.4f)};blocker.GetComponent<Renderer>().sharedMaterial=opaque;
                var occluded=Read("occluded");burst.Clear();int leak=Changed(occluded,Read("blocker-only"));
                if(leak!=0)throw new Exception("Contact renders through foreground geometry");
                UnityEngine.Object.DestroyImmediate(blocker);UnityEngine.Object.DestroyImmediate(opaque);
                for(int n=0;n<40;n++)burst.Hit(Vector3.zero,Vector3.right,n%5==0);
                burst.Tick(camera,0);if(burst.ActiveCount!=6)throw new Exception("Contact pool grew or failed to reuse");
                burst.Clear();if(Changed(empty,Read("cleared"))!=0)throw new Exception("Contact remains after clear");
                var fx=new CombatVfx(root);var state=new Battle();state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});for(int n=0;n<120;n++)state.Tick(.02f,new PlayerInput{Tracking=true});
                void Tick()=>fx.Tick(state,camera,.02f,Vector3.zero,Vector3.forward,Vector3.zero,Vector3.forward,false,0,false,Vector3.forward);
                for(int kind=0;kind<4;kind++)
                {
                    fx.Clear();fx.Impact(Vector3.zero,kind==1,blocked:kind==2,hurt:kind==3,direction:Vector3.right);Tick();
                    if((fx.ActiveContactCount>0)!=(kind==0))throw new Exception("Normal punch flash leaked into beam/block/hurt");
                }
                fx.Impact(Vector3.zero,false);Tick();state.Pause();Tick();if(fx.ActiveContactCount!=0)throw new Exception("Pause retained contact");
                state=new Battle();Tick();if(fx.ActiveContactCount!=0)throw new Exception("New round retained contact");
                report.AppendLine($"GPU visiblePixels={visible} comboPixels={comboPixels} changingPixels={moving} zeroTime={repeat} foregroundLeak={leak} expired=passed pool=6 clear=passed");
                report.AppendLine("normal-punch=visible beam/block/hurt=separate pause=cleared newRound=cleared");
                File.WriteAllText(Folder+"/validation.txt",report.ToString());Debug.Log("[StrikeContactChecks] "+report);
            }
            finally{camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(pixels);}
        }
        public static void Roster()
        {
            Directory.CreateDirectory(Folder+"/roster");var report=new StringBuilder();
            foreach(string id in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(931);
                var world=new GameWorld();var state=new Battle();var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});for(int n=0;n<120;n++)state.Tick(.02f,new PlayerInput{Tracking=true});state.GiveInstructionTime(20);while(state.TryCue(out _)){}
                hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,1,0);
                var target=new RenderTexture(1280,720,24){antiAliasing=4};target.Create();world.Camera.targetTexture=target;world.Camera.aspect=16f/9;
                float health=state.EnemyHealth,time=0;int contacts=0;
                try
                {
                    for(int strike=0;strike<2;strike++)for(int frame=0;frame<60;frame++)
                    {
                        const float dt=1/60f;state.Tick(world.BattleDelta(dt,state),new PlayerInput{Tracking=true,LeftPunch=frame==0&&strike==0,RightPunch=frame==0&&strike==1});while(state.TryCue(out var cue))world.Cue(cue,state);
                        hero.Update(state,world.Camera,dt,time);enemy.Update(state,world.Camera,dt,time);
                        bool hit=state.EnemyHealth<health;if(hit){contacts++;world.Hit(false,state);}health=state.EnemyHealth;world.Tick(state,dt,time);time+=dt;
                        if(hit){if(world.ActiveContactCount==0)throw new Exception("Actual punch did not trigger contact");CharacterReview.Save(world.Camera,target,$"{Folder}/roster/{id}-{strike}.png");}
                    }
                    if(contacts!=2||state.EnemyHealth!=48||state.Energy!=2||world.ActiveContactCount!=0)throw new Exception("Punch contact changes battle or persists");
                    world.ResetPresentation();report.AppendLine(id+" left/right contact=passed expired=passed health=48 energy=2 reset=passed");
                }
                finally{world.Camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);}
            }
            File.WriteAllText(Folder+"/roster-validation.txt",report.ToString());Debug.Log("[StrikeContactRoster]\n"+report);
        }
    }
}
