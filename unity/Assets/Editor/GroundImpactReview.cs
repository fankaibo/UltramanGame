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
    public static class GroundImpactReview
    {
        static string Folder=>Path.GetFullPath(Environment.GetEnvironmentVariable("ULTRAMAN_GROUND_REVIEW")??Path.Combine(Application.dataPath,"../../artifacts/ground-impact"));
        public static void Before()=>Render("before");
        public static void After()=>Render("after");
        public static void Release(){After();Checks();Roster();Audio();}
        public static void ScannedRelease()
        {
            foreach(string name in new[]{"rock_07","rock_09"})
                AssetDatabase.ImportAsset("Assets/Resources/Environment/GroundDebris/"+name+".fbx",ImportAssetOptions.ForceUpdate);
            ScannedAssets();Release();
        }
        public static void ScannedAssets()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            Directory.CreateDirectory(Folder+"/inspection");
            var root=new GameObject("Scanned fragment inspection");var camera=new GameObject("Camera").AddComponent<Camera>();
            var ground=new GroundImpact(root.transform);ground.Burst(Vector3.zero,Vector3.right,"scan-review");ground.Tick(camera,0);
            var meshes=new HashSet<Mesh>();var materials=new HashSet<Material>();var report=new StringBuilder();
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if(filter.name!="Ground basalt fragment")continue;
                var mesh=filter.sharedMesh;meshes.Add(mesh);var material=filter.GetComponent<Renderer>().sharedMaterial;materials.Add(material);
                if(mesh.triangles.Length!=960||!mesh.isReadable||mesh.uv.Length!=mesh.vertexCount||mesh.tangents.Length!=mesh.vertexCount)
                    throw new Exception("Scanned fragment missing topology/UV/tangents: "+mesh.name);
                float radius=0;foreach(var vertex in mesh.vertices){radius=Mathf.Max(radius,vertex.magnitude);if(float.IsNaN(vertex.x))throw new Exception("Invalid fragment vertex");}
                if(radius<.89f||radius>.91f)throw new Exception("Fragment scale invalid "+mesh.bounds);
                foreach(string property in new[]{"_MainTex","_Normal","_ARM"})if(!material.GetTexture(property))throw new Exception("Missing scan map "+property);
            }
            if(meshes.Count!=2||materials.Count!=2)throw new Exception("Scans/materials not shared by pool");
            foreach(var mesh in meshes)report.AppendLine($"{mesh.name} vertices={mesh.vertexCount} triangles={mesh.triangles.Length/3} bounds={mesh.bounds} uv/tangents/readable=passed");
            // Magnified asset inspection, separate from the unchanged battle
            // camera in Before/After. Do not pass this off as gameplay scale.
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))renderer.gameObject.SetActive(false);
            int selected=0;
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if(filter.name!="Ground basalt fragment"||selected>=4)continue;
                filter.gameObject.SetActive(true);filter.transform.localScale=Vector3.one*.65f;
                filter.transform.rotation=Quaternion.Euler(20+selected*23,selected*60,15);
                float bottom=0;foreach(var v in filter.sharedMesh.vertices)bottom=Mathf.Min(bottom,(filter.transform.rotation*(v*.65f)).y);
                filter.transform.position=new Vector3((selected%2-.5f)*1.5f,-bottom,(selected/2-.5f)*1.3f);selected++;
            }
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(0,-.055f,0);floor.transform.localScale=new Vector3(20,.1f,20);
            var floorMaterial=new Material(Shader.Find("Standard")){color=new Color(.09f,.10f,.12f)};floor.GetComponent<Renderer>().sharedMaterial=floorMaterial;
            var key=new GameObject("Cool side light").AddComponent<Light>();key.type=LightType.Directional;key.transform.eulerAngles=new Vector3(35,-40,0);key.intensity=1.2f;key.color=new Color(.76f,.84f,1);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.07f,.09f,.12f);camera.fieldOfView=36;camera.aspect=16f/9;
            camera.transform.position=new Vector3(2.2f,2.5f,-5);camera.transform.LookAt(new Vector3(0,.3f,0));
            var target=new RenderTexture(1280,720,24){antiAliasing=4};target.Create();camera.targetTexture=target;
            try{CharacterReview.Save(camera,target,Folder+"/inspection/scanned-assets-magnified.png");}
            finally{camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(floorMaterial);}
            UnityEngine.Object.DestroyImmediate(root);
            foreach(var mat in materials)if(mat)throw new Exception("Owned fragment material leaked");
            foreach(var mesh in meshes)if(!mesh)throw new Exception("Cleanup destroyed shared asset");
            report.AppendLine("pool=72 sharedMeshes=2 sharedMaterials=2 noCopiedTextures=passed ownedCleanup=passed sourceMeshesRetained=passed");
            File.WriteAllText(Folder+"/scan-validation.txt",report.ToString());Debug.Log("[ScannedGroundAssets] "+report);
        }
        public static void Audio()
        {
            Directory.CreateDirectory(Folder);var clip=GameAudio.CreateGroundCrunch();var data=new float[clip.samples];clip.GetData(data,0);
            double power=0;float peak=0;foreach(float sample in data){if(float.IsNaN(sample)||float.IsInfinity(sample))throw new Exception("Invalid ground audio");power+=sample*sample;peak=Mathf.Max(peak,Mathf.Abs(sample));}
            double rms=Math.Sqrt(power/data.Length);if(peak>.9f||rms<.01||rms>.3)throw new Exception("Ground audio clipping or silence");
            using(var file=new BinaryWriter(File.Create(Folder+"/ground-crunch.wav")))
            {
                file.Write(Encoding.ASCII.GetBytes("RIFF"));file.Write(36+data.Length*2);file.Write(Encoding.ASCII.GetBytes("WAVEfmt "));file.Write(16);file.Write((short)1);file.Write((short)1);
                file.Write(clip.frequency);file.Write(clip.frequency*2);file.Write((short)2);file.Write((short)16);file.Write(Encoding.ASCII.GetBytes("data"));file.Write(data.Length*2);
                foreach(float sample in data)file.Write((short)Mathf.RoundToInt(sample*32767));
            }
            File.WriteAllText(Folder+"/audio-validation.txt",$"samples={data.Length} rate={clip.frequency} peak={peak:F4} rms={rms:F4} finite=passed");UnityEngine.Object.DestroyImmediate(clip);
        }
        public static void Checks()
        {
            Directory.CreateDirectory(Folder+"/inspection");File.Delete(Folder+"/effect-validation.txt");var report=new StringBuilder();
            foreach(string name in new[]{"GroundDust","GroundStone"})
            {var shader=Resources.Load<Shader>(name);if(!shader||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new Exception("Invalid ground shader "+name);}
            Vector3[] reference=null;
            foreach(int rate in new[]{15,30,60})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var root=new GameObject("Ground inspection");var camera=new GameObject("Camera").AddComponent<Camera>();var fx=new GroundImpact(root.transform);
                fx.Burst(new Vector3(0,99,0),Vector3.right,"inspection");fx.Tick(camera,0);
                if(fx.ActiveStones!=18||fx.ActiveClouds!=12)throw new Exception("Missing ground pieces");
                var transforms=root.GetComponentsInChildren<Transform>();var meshes=root.GetComponentsInChildren<MeshFilter>();
                float age=0,minimum=10,maximum=0;Vector3[] samples=new Vector3[36];int sample=0;
                foreach(float stop in new[]{.3f,.8f,2f})
                {
                    while(age<stop-.00001f)
                    {
                        float dt=Mathf.Min(1f/rate,stop-age);age+=dt;fx.Tick(camera,dt);
                        foreach(var filter in meshes)if(filter.gameObject.activeSelf&&filter.name=="Ground basalt fragment")
                            foreach(var v in filter.sharedMesh.vertices){float y=filter.transform.TransformPoint(v).y;minimum=Mathf.Min(minimum,y);maximum=Mathf.Max(maximum,y);}
                    }
                    if(stop<1)
                    {
                        foreach(var filter in meshes)if(filter.name=="Ground basalt fragment")samples[sample++]=filter.transform.position;
                        var positions=new Vector3[transforms.Length];for(int i=0;i<positions.Length;i++)positions[i]=transforms[i].position;
                        fx.Tick(camera,0);for(int i=0;i<positions.Length;i++)if(Vector3.Distance(positions[i],transforms[i].position)>.00001f)throw new Exception("Zero time moves ground pieces");
                    }
                }
                if(minimum<.007f||maximum>.9f||maximum<.35f||fx.ActiveStones!=0||fx.ActiveClouds!=0)throw new Exception($"Ground penetration/expiry {minimum}..{maximum}");
                float difference=0;if(reference==null)reference=samples;else for(int i=0;i<samples.Length;i++)difference=Mathf.Max(difference,Vector3.Distance(reference[i],samples[i]));
                if(difference>.0001f)throw new Exception("Ballistic path depends on render interval");
                int objectCount=root.GetComponentsInChildren<Transform>(true).Length;
                for(int i=0;i<20;i++)fx.Burst(new Vector3(i*.01f,0,0),Vector3.zero,"pool-inspection");fx.Tick(camera,0);
                if(fx.ActiveStones!=72||fx.ActiveClouds!=48||root.GetComponentsInChildren<Transform>(true).Length!=objectCount)throw new Exception("Ground pool not bounded");
                fx.Clear();fx.Tick(camera,.1f);if(fx.ActiveStones!=0||fx.ActiveClouds!=0)throw new Exception("Clear revived ground pieces");
                report.AppendLine($"{rate}Hz verticesY={minimum:F5}..{maximum:F5} commonTimeDifference={difference:F6} zeroTime=passed pool=72/48 expiry=passed clear=passed");
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var parent=new GameObject("GPU inspection");var cam=new GameObject("GPU camera").AddComponent<Camera>();cam.depthTextureMode=DepthTextureMode.Depth;
            cam.transform.position=new Vector3(0,1.6f,-5);cam.transform.LookAt(new Vector3(0,.4f,0));cam.fieldOfView=38;cam.aspect=16f/9;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.10f,.12f,.15f);
            var light=new GameObject("Key").AddComponent<Light>();light.type=LightType.Directional;light.transform.eulerAngles=new Vector3(45,-30,0);light.intensity=1;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(0,-.05f,0);floor.transform.localScale=new Vector3(10,.1f,10);
            var opaque=new Material(Shader.Find("Standard")){color=new Color(.18f,.19f,.22f)};floor.GetComponent<Renderer>().sharedMaterial=opaque;
            var ground=new GroundImpact(parent.transform);var rt=new RenderTexture(512,288,24);rt.Create();cam.targetTexture=rt;var texture=new Texture2D(512,288,TextureFormat.RGB24,false);
            Color32[] Read(string name){cam.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,512,288),0,0);texture.Apply();File.WriteAllBytes(Folder+"/inspection/"+name+".png",texture.EncodeToPNG());return texture.GetPixels32();}
            int Difference(Color32[] a,Color32[] b,int threshold){int n=0;for(int i=0;i<a.Length;i++)if(Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b)>threshold)n++;return n;}
            try
            {
                var empty=Read("empty");ground.Burst(Vector3.zero,Vector3.right,"gpu");ground.Tick(cam,.24f);var visible=Read("visible");
                if(Difference(visible,Read("repeat"),0)!=0)throw new Exception("Ground render changes at frozen time");
                foreach(var r in parent.GetComponentsInChildren<Renderer>())if(r.name=="Ground basalt fragment")r.enabled=false;
                int dust=Difference(empty,Read("dust"),6);
                foreach(var r in parent.GetComponentsInChildren<Renderer>())r.enabled=r.name=="Ground basalt fragment";
                int stones=Difference(empty,Read("stones"),6);
                if(dust<400||stones<80)throw new Exception($"Ground effect invisible: dust={dust} stones={stones}");
                var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.transform.position=new Vector3(0,1,-2.5f);blocker.transform.localScale=new Vector3(10,10,.2f);blocker.GetComponent<Renderer>().sharedMaterial=opaque;
                foreach(var r in parent.GetComponentsInChildren<Renderer>())r.enabled=false;var covered=Read("covered");
                foreach(var r in parent.GetComponentsInChildren<Renderer>())r.enabled=true;int leak=Difference(covered,Read("covered-effects"),1);if(leak!=0)throw new Exception("Ground effects show through foreground "+leak);
                report.AppendLine($"GPU dustPixels={dust} stonePixels={stones} foregroundLeakPixels={leak} zeroTime=passed");
            }
            finally{cam.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(opaque);}
            File.WriteAllText(Folder+"/effect-validation.txt",report.ToString());Debug.Log("[GroundImpactChecks]\n"+report);
        }
        public static void Roster()
        {
            Directory.CreateDirectory(Folder+"/roster");File.Delete(Folder+"/roster-validation.txt");var report=new StringBuilder();
            foreach(string id in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var world=new GameWorld();var state=Ready(0);
                for(int f=0;f<1500;f++){state.Tick(1/60f,new PlayerInput{Tracking=true});if(state.Enemy==EnemyPhase.Windup&&state.EnemyAge>state.WarningDuration-.22f)break;}
                while(state.TryCue(out _)){}var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                var rt=new RenderTexture(1280,720,24);rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
                bool landing=false;
                try
                {
                    for(int f=0;f<180;f++)
                    {
                        const float dt=1/60f;state.Tick(dt,new PlayerInput{Tracking=true});while(state.TryCue(out var cue))world.Cue(cue,state);
                        hero.Update(state,world.Camera,dt,f*dt);enemy.Update(state,world.Camera,dt,f*dt);world.Tick(state,dt,f*dt);
                        if(state.Action==HeroAction.Hurt&&state.ActionAge>.47f)
                        {
                            if(world.ActiveGroundStones<18||world.ActiveGroundDust<12)throw new Exception("No actual hero landing effect: "+id);
                            CharacterReview.Save(world.Camera,rt,Folder+"/roster/"+id+".png");landing=true;break;
                        }
                    }
                    if(!landing)throw new Exception("No landing exercised");state.Pause();world.Tick(state,.02f,4);
                    if(world.ActiveGroundStones!=0||world.ActiveGroundDust!=0)throw new Exception("Pause retained effects");
                    state=new Battle();world.ResetPresentation();world.Tick(state,.02f,5);
                    if(world.ActiveGroundStones!=0||world.ActiveGroundDust!=0)throw new Exception("New round revived effects");
                    report.AppendLine(id+" heroLanding=passed pause=passed newRound=passed");
                }
                finally{world.Camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
            }
            File.WriteAllText(Folder+"/roster-validation.txt",report.ToString());Debug.Log("[GroundImpactRoster]\n"+report);
        }
        static Battle Ready(int hits,int health=50)
        {
            var state=new Battle(health);state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
            for(int f=0;f<(Battle.TransformationSeconds+0.2f)/(.02f);f++)state.Tick(.02f,new PlayerInput{Tracking=true});
            for(int n=0;n<hits;n++){state.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});for(int f=0;f<25;f++)state.Tick(.02f,new PlayerInput{Tracking=true});}
            while(state.TryCue(out _)){}return state;
        }
        static void Render(string version)
        {
            string folder=Folder+"/"+version;Directory.CreateDirectory(folder+"/frames");File.Delete(folder+"/validation.txt");
            var csv=new StringBuilder("segment,frame,phase,enemy,enemyAge,action,actionAge,health,punches,hurt\n");int frameIndex=0;
            foreach(string segment in new[]{"rush","stagger","defeat"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(934);
                var world=new GameWorld();var state=Ready(segment=="stagger"?4:segment=="defeat"?9:0,segment=="defeat"?10:50);
                var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                if(segment=="rush")
                {for(int f=0;f<1500;f++){state.Tick(1/60f,new PlayerInput{Tracking=true});if(state.Enemy==EnemyPhase.Windup&&state.EnemyAge>state.WarningDuration-.22f)break;}if(state.Enemy!=EnemyPhase.Windup)throw new Exception("No rush prepared");}
                else state.GiveInstructionTime(20);
                while(state.TryCue(out _)){}hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,1,0);
                var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
                float health=state.EnemyHealth;int frames=segment=="stagger"?240:180;
                try
                {
                    for(int f=0;f<frames;f++)
                    {
                        const float dt=1/60f;float time=f*dt;
                        state.Tick(world.BattleDelta(dt,state),new PlayerInput{Tracking=true,RightPunch=f==24&&segment!="rush"});
                        while(state.TryCue(out var cue))world.Cue(cue,state);
                        hero.Update(state,world.Camera,dt,time);enemy.Update(state,world.Camera,dt,time);
                        if(state.EnemyHealth<health)world.Hit(false,state);health=state.EnemyHealth;world.Tick(state,dt,time);
                        csv.AppendLine(FormattableString.Invariant($"{segment},{f},{state.Phase},{state.Enemy},{state.EnemyAge:F5},{state.Action},{state.ActionAge:F5},{state.EnemyHealth},{state.Punches},{state.HitsTaken}"));
                        if(f%2==0)CharacterReview.Save(world.Camera,rt,$"{folder}/frames/{frameIndex++:D4}.png");
                        if(f%15==0)CharacterReview.Save(world.Camera,rt,$"{folder}/{segment}-{f:D3}.png");
                    }
                    if(segment=="rush"&&state.HitsTaken!=1||segment=="stagger"&&(state.Punches!=5||state.EnemyHealth!=45)||segment=="defeat"&&state.Phase!=GamePhase.Victory)
                        throw new Exception("Ground effect changed battle result: "+segment);
                }
                finally{world.Camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
            }
            File.WriteAllText(folder+"/sequence.csv",csv.ToString());
            var sources=new StringBuilder();using(var sha=System.Security.Cryptography.SHA256.Create())
                foreach(string file in new[]{"Scripts/Runtime/GameWorld.cs","Scripts/Runtime/CombatVfx.cs","Scripts/Runtime/ImpactAtmosphere.cs","Scripts/Runtime/GroundImpact.cs","Resources/GroundDust.shader","Resources/GroundStone.shader","Scripts/Runtime/GameAudio.cs","Scripts/Runtime/ArenaController.cs","Scripts/Core/Battle.cs","Scripts/Runtime/RiggedActor.cs","Editor/GroundImpactReview.cs"})
                {string path=Path.Combine(Application.dataPath,file);if(File.Exists(path))sources.AppendLine(file+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant());}
            File.WriteAllText(folder+"/sources.txt",sources.ToString());File.WriteAllText(folder+"/validation.txt",$"{version}: three continuous segments rush/stagger/defeat frames={frameIndex} samples=600 passed");
            Debug.Log("[GroundImpactReview] "+version+" frames="+frameIndex);
        }
    }
}
