using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class ThreatCameraReview
    {
        public static void Before()=>Render("before","Tiga",true);
        public static void After(){foreach(string id in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})Render("after",id,id=="Tiga");}
        public static void Release(){After();Flow();}
        static Battle Ready(bool charged=false)
        {
            var state=new Battle();state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
            if(charged)
            {
                for(int i=0;i<(Battle.TransformationSeconds+.2f)/.02f;i++)state.Tick(.02f,new PlayerInput{Tracking=true});
                for(int punch=0;punch<15;punch++){state.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});for(int i=0;i<22;i++)state.Tick(.02f,new PlayerInput{Tracking=true});}
            }
            for(int i=0;i<1000;i++){state.Tick(.02f,new PlayerInput{Tracking=true});if(state.Enemy==EnemyPhase.Windup)break;}
            if(state.Enemy!=EnemyPhase.Windup)throw new Exception("No warning in review");
            while(state.TryCue(out _)){}return state;
        }
        public static void Flow()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/threat-camera"));File.Delete(folder+"/flow-validation.txt");var report=new StringBuilder();
            foreach(int rate in new[]{15,30,60})foreach(string mode in new[]{"extended","pause","beam","instruction","new-round","reset"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var world=new GameWorld();var state=Ready(mode=="beam");float dt=1f/rate,time=0;
                if(mode=="extended")state.GiveInstructionTime(5,true);
                for(int f=0;f<rate+rate/2;f++){state.Tick(dt,new PlayerInput{Tracking=true});world.Tick(state,dt,time);time+=dt;}
                if(world.ThreatFocus<.99f)throw new Exception("Missing threat peak");
                var position=world.Camera.transform.position;var rotation=world.Camera.transform.rotation;float fov=world.Camera.fieldOfView;
                world.Tick(state,0,time);
                if(Vector3.Distance(position,world.Camera.transform.position)>.0001f||Quaternion.Angle(rotation,world.Camera.transform.rotation)>.01f||Mathf.Abs(fov-world.Camera.fieldOfView)>.001f)throw new Exception("Repeated sample moved threat camera");
                if(mode=="pause")state.Pause();
                if(mode=="beam"){state.Tick(dt,new PlayerInput{Tracking=true,Beam=true});while(state.TryCue(out var cue))world.Cue(cue,state);}
                if(mode=="instruction")state.GiveInstructionTime(2);
                if(mode=="new-round")state=new Battle();
                if(mode=="reset"){world.ResetPresentation();if(world.ThreatFocus!=0)throw new Exception("Reset kept threat focus");state=new Battle();}
                float biggestReturn=0;
                for(int f=0;f<rate*(mode=="extended"?9:2);f++)
                {
                    var old=world.Camera.transform.position;state.Tick(world.BattleDelta(dt,state),new PlayerInput{Tracking=true,Shield=true});while(state.TryCue(out var cue))world.Cue(cue,state);world.Tick(state,dt,time);time+=dt;
                    if(mode=="instruction"&&f<rate)biggestReturn=Mathf.Max(biggestReturn,Vector3.Distance(old,world.Camera.transform.position)/dt);
                    if(mode=="extended"&&state.Enemy==EnemyPhase.Windup&&state.EnemyAge>=3.4f&&world.ThreatFocus>.001f)throw new Exception("Late warning restarted camera approach");
                    if(mode!="extended"&&f>rate/2&&world.ThreatFocus>.001f)throw new Exception("Cancelled threat camera survived");
                    if((mode=="pause"||mode=="beam"||mode=="new-round")&&f==0&&world.ThreatFocus!=0)throw new Exception("Exclusive presentation retained threat camera");
                }
                if(mode=="extended"&&(state.Blocks!=1||state.HitsTaken!=0))throw new Exception("Extended warning lost block");
                if(biggestReturn>11)throw new Exception("Instruction cancellation jumped camera");
                report.AppendLine($"{rate}Hz {mode} zeroTime=passed returnSpeed={biggestReturn:F3} focus={world.ThreatFocus:F3} blocks={state.Blocks} passed");
            }
            File.WriteAllText(folder+"/flow-validation.txt",report.ToString());Debug.Log("[ThreatCameraFlow]\n"+report);
        }
        static float Coverage(Camera camera)
        {
            var backdrop=GameObject.Find("Realistic Mount Fuji night backdrop").transform;
            var plane=new Plane(backdrop.forward,backdrop.position);float max=0;
            for(int x=0;x<2;x++)for(int y=0;y<2;y++)
            {var ray=camera.ViewportPointToRay(new Vector3(x,y,0));if(!plane.Raycast(ray,out float d))throw new Exception("Backdrop behind camera");var p=backdrop.InverseTransformPoint(ray.GetPoint(d));max=Mathf.Max(max,Mathf.Abs(p.x),Mathf.Abs(p.y));}
            return max;
        }
        static void Render(string version,string id,bool film)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(928);
            var world=new GameWorld();var state=Ready();var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);
            var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            var args=Environment.GetCommandLineArgs();int outputAt=Array.IndexOf(args,"--threat-output");
            string output=outputAt>=0&&outputAt+1<args.Length?args[outputAt+1]:Path.Combine(Application.dataPath,"../../artifacts/threat-camera");
            string folder=Path.GetFullPath(Path.Combine(output,version,id));Directory.CreateDirectory(folder+"/frames");File.Delete(folder+"/validation.txt");
            var sources=new StringBuilder();using(var sha=System.Security.Cryptography.SHA256.Create())
                foreach(string file in new[]{"Scripts/Runtime/GameWorld.cs","Scripts/Runtime/RiggedActor.cs","Scripts/Runtime/CinematicCamera.cs","Scripts/Core/Battle.cs","Scripts/Core/MonsterWindupMotion.cs","Resources/Characters/Golza/Golza.fbx","Editor/ThreatCameraReview.cs"})
                    if(File.Exists(Path.Combine(Application.dataPath,file)))sources.AppendLine(file+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Application.dataPath,file)))).Replace("-","").ToLowerInvariant());
            File.WriteAllText(folder+"/sources.txt",sources.ToString());
            hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,1,0);
            var target=new RenderTexture(1280,720,24){antiAliasing=4};target.Create();world.Camera.targetTexture=target;world.Camera.aspect=16f/9;
            float minY=1,maxY=0,minX=1,maxX=0,edge=0,maxStep=0,peakTop=0,baseSize=0,peakSize=0;Vector3 prior=world.Camera.transform.position;
            var mesh=new Mesh();var samples=new StringBuilder("frame,enemy,age,action,health,blocks,hurt\n");
            var cameraSamples=new StringBuilder("frame,x,y,z,fov\n");
            try
            {
                for(int frame=0;frame<480;frame++)
                {
                    const float dt=1/60f;float time=frame*dt;
                    state.Tick(world.BattleDelta(dt,state),new PlayerInput{Tracking=true,Shield=time>1});
                    while(state.TryCue(out var cue))world.Cue(cue,state);
                    hero.Update(state,world.Camera,dt,time);enemy.Update(state,world.Camera,dt,time);world.Tick(state,dt,time);
                    Vector3 p=world.Camera.transform.position;maxStep=Mathf.Max(maxStep,Vector3.Distance(p,prior));prior=p;
                    samples.AppendLine(FormattableString.Invariant($"{frame},{state.Enemy},{state.EnemyAge:F4},{state.Action},{state.EnemyHealth},{state.Blocks},{state.HitsTaken}"));
                    cameraSamples.AppendLine(FormattableString.Invariant($"{frame},{p.x:F4},{p.y:F4},{p.z:F4},{world.Camera.fieldOfView:F4}"));
                    edge=Mathf.Max(edge,Coverage(world.Camera));
                    if(frame%10==0&&state.Enemy==EnemyPhase.Windup)
                        foreach(var actor in new[]{hero,enemy})foreach(var skin in actor.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {skin.BakeMesh(mesh,true);foreach(var vertex in mesh.vertices){var v=world.Camera.WorldToViewportPoint(skin.transform.TransformPoint(vertex));minX=Mathf.Min(minX,v.x);maxX=Mathf.Max(maxX,v.x);minY=Mathf.Min(minY,v.y);maxY=Mathf.Max(maxY,v.y);if(version=="after"&&world.ThreatFocus>.99f)peakTop=Mathf.Max(peakTop,v.y);}}
                    var center=world.EnemyHome+Vector3.up*2.5f;float size=Mathf.Abs(world.Camera.WorldToViewportPoint(center+Vector3.up*.2f).y-world.Camera.WorldToViewportPoint(center).y);
                    if(frame==0)baseSize=size;if(frame==95)peakSize=size;
                    if(version=="after"&&state.Enemy==EnemyPhase.Windup&&state.EnemyAge>=3.4f&&world.ThreatFocus>.001f)throw new Exception("Warning did not return before attack");
                    if(film&&frame%2==0)CharacterReview.Save(world.Camera,target,$"{folder}/frames/{frame/2:D4}.png");
                    if(frame==0||frame==95||frame==200||frame==335)CharacterReview.Save(world.Camera,target,$"{folder}/stage-{frame}.png");
                }
                string report=$"{id}/{version} blocks={state.Blocks} hurt={state.HitsTaken} health={state.EnemyHealth} viewport=({minX:F3},{minY:F3})-({maxX:F3},{maxY:F3}) backdropEdge={edge:F3} cameraStep={maxStep:F4} peakTop={peakTop:F3} monsterMagnification={peakSize/baseSize:F3}";
                if(state.Blocks!=1||state.HitsTaken!=0||state.EnemyHealth!=50||edge>.49f)throw new Exception(report);
                if(version=="after"&&(peakTop>.87f||minX<.1f||maxX>.9f||peakSize/baseSize<1.15f||maxStep>.09f))throw new Exception("Unsafe or ineffective threat framing: "+report);
                File.WriteAllText(folder+"/sequence.csv",samples.ToString());File.WriteAllText(folder+"/camera.csv",cameraSamples.ToString());
                File.WriteAllText(folder+"/validation.txt",report);Debug.Log("[ThreatCameraReview] "+report);
            }
            finally{world.Camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(mesh);}
        }
    }
}
