using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class RangedContactReview
    {
        public static void Before()=>Run(false);
        public static void After()=>Run(true);
        static void Run(bool verify)
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--contact-output");
            string output=index>=0?args[index+1]:Path.Combine(Application.dataPath,"../../artifacts/ranged-contact");
            foreach(int hz in verify?new[]{15,30,60}:new[]{60})
            {
                string folder=Path.GetFullPath(Path.Combine(output,hz.ToString()));Directory.CreateDirectory(folder);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(1988);
                var world=new GameWorld();var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);
                var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);world.SetHeroProfile("Tiga");
                var state=new Battle(50);state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
                while(state.Phase!=GamePhase.Battle)state.Tick(.02f,new PlayerInput{Tracking=true});state.GiveInstructionTime(30);while(state.TryCue(out _)){}
                for(int f=0;f<40;f++){hero.Update(state,world.Camera,1/60f,f/60f);enemy.Update(state,world.Camera,1/60f,f/60f);world.Tick(state,1/60f,f/60f);}
                var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
                int objects=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
                float health=50,dt=1f/hz;int hits=0;
                var csv=new StringBuilder("frame,phase,action,age,shotAge,health,energy,hits,visible\n");
                try
                {
                    for(int f=0;f<hz*4;f++)
                    {
                        var input=new PlayerInput{Tracking=true};
                        if(f==hz/2||f==hz||f==hz*3/2){input.RangedAttack=true;input.LeftPunch=f==hz;input.RightPunch=!input.LeftPunch;input.AttackSpeed=1.7f;}
                        state.Tick(dt,input);while(state.TryCue(out var cue))world.Cue(cue,state);
                        hero.Update(state,world.Camera,dt,f*dt);enemy.Update(state,world.Camera,dt,f*dt);
                        if(state.EnemyHealth<health){hits++;world.Hit(false,state);}health=state.EnemyHealth;world.Tick(state,dt,f*dt);
                        csv.AppendLine(FormattableString.Invariant($"{f},{state.Phase},{state.Action},{state.ActionAge:R},{state.Shot.Age:R},{health},{state.Energy},{hits},{world.Projectile.ImpactVisible}"));
                        if(hz==60&&f%2==0)Save(world.Camera,rt,Path.Combine(folder,$"{f/2:D4}.jpg"));
                    }
                    if(hits!=3||world.Projectile.Impacts!=3||state.EnemyHealth!=47||state.Energy!=3)throw new Exception("Contact changed combat receipts");
                    if(world.Projectile.ImpactVisible)throw new Exception("Contact effects did not expire");
                    if(objects!=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length)throw new Exception("Per-hit objects allocated");
                    world.Projectile.Impact(world.BeamTarget,world.BattleAxis);state.Pause();world.Tick(state,0,4);
                    if(world.Projectile.ImpactVisible)throw new Exception("Pause retained hit effects");world.ResetPresentation();
                    File.WriteAllText(Path.Combine(folder,"trace.csv"),csv.ToString());
                    Debug.Log($"[RangedContactReview] PASS hz={hz} hits={hits} health={health} pool=stable pause=clear");
                }
                finally{world.Camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
            }
        }
        public static void ValidateSparks()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/ranged-contact-20261011/isolated"));Directory.CreateDirectory(folder);
            var root=new GameObject("Spark depth review").transform;var fx=new RangedContactSparks(root);
            foreach(Transform child in root)child.gameObject.layer=29;
            var camera=new GameObject("Spark camera").AddComponent<Camera>();camera.transform.position=new Vector3(0,0,-4);
            camera.cullingMask=1<<29;camera.backgroundColor=Color.black;camera.clearFlags=CameraClearFlags.SolidColor;camera.allowHDR=false;
            var rt=new RenderTexture(320,180,24);rt.Create();camera.targetTexture=rt;
            Texture2D Capture(){camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;var t=new Texture2D(320,180,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,320,180),0,0);t.Apply();RenderTexture.active=old;return t;}
            int Difference(Texture2D a,Texture2D b){var p=a.GetPixels32();var q=b.GetPixels32();int d=0;for(int j=0;j<p.Length;j++)d=Math.Max(d,Math.Max(Math.Abs(p[j].r-q[j].r),Math.Max(Math.Abs(p[j].g-q[j].g),Math.Abs(p[j].b-q[j].b))));return d;}
            var textures=new System.Collections.Generic.List<Texture2D>();Material material=null;GameObject wall=null;
            try
            {
                var empty=Capture();textures.Add(empty);fx.Hit(Vector3.zero,Vector3.forward);fx.Tick(.2f);var lit=Capture();textures.Add(lit);
                fx.Tick(0);fx.Tick(-1);var frozen=Capture();textures.Add(frozen);
                if(Difference(empty,lit)<40||Difference(lit,frozen)>0)throw new Exception("Spark missing or zero-time drift");
                File.WriteAllBytes(folder+"/live.png",lit.EncodeToPNG());
                var lines=root.GetComponentsInChildren<LineRenderer>();var tips=new Vector3[lines.Length];for(int i=0;i<tips.Length;i++)tips[i]=lines[i].GetPosition(5);
                foreach(int hz in new[]{15,30,60})
                {
                    fx.Clear();fx.Hit(Vector3.zero,Vector3.forward);for(int i=0;i<hz/5;i++)fx.Tick(1f/hz);
                    for(int i=0;i<18;i++)if(Vector3.Distance(tips[i],lines[i].GetPosition(5))>.00001f)throw new Exception("Frame rate changed spark trajectory");
                }
                fx.Clear();wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.layer=29;wall.transform.position=new Vector3(0,0,-2);wall.transform.localScale=new Vector3(12,12,.2f);
                material=new Material(Shader.Find("Unlit/Color")){color=new Color(.2f,.3f,.4f)};wall.GetComponent<Renderer>().sharedMaterial=material;
                var covered=Capture();textures.Add(covered);fx.Hit(Vector3.zero,Vector3.forward);fx.Tick(.2f);var behind=Capture();textures.Add(behind);
                if(Difference(covered,behind)>0)throw new Exception("Sparks ignore foreground depth");
                for(int i=0;i<20;i++){fx.Hit(Vector3.zero,Vector3.up);fx.Tick(.04f);}
                if(root.GetComponentsInChildren<LineRenderer>().Length!=72)throw new Exception("Spark pool grew");fx.Tick(1);
                if(fx.Visible)throw new Exception("Spark lifetime leaked");fx.Hit(Vector3.zero,Vector3.zero);fx.Clear();if(fx.Visible)throw new Exception("Spark Clear failed");
                foreach(var m in UnityEditor.ShaderUtil.GetShaderMessages(Resources.Load<Shader>("ContactSpark")))
                    if(m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)throw new Exception(m.message);
                string result="visibleContrast="+Difference(empty,lit)+" frozenPixelError=0 depthPixelError=0 rates=15/30/60 pool=72 expiration=pass clear=pass";
                File.WriteAllText(folder+"/validation.txt",result);Debug.Log("[ContactSparks] PASS "+result);
            }
            finally
            {
                foreach(var t in textures)UnityEngine.Object.DestroyImmediate(t);camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(root.gameObject);UnityEngine.Object.DestroyImmediate(camera.gameObject);if(wall)UnityEngine.Object.DestroyImmediate(wall);if(material)UnityEngine.Object.DestroyImmediate(material);
            }
        }
        static void Save(Camera camera,RenderTexture rt,string path)
        {
            var old=RenderTexture.active;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
            try{camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToJPG(85));}
            finally{RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(image);}
        }
    }
}
