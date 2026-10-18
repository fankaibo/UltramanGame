using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class EnemyExchangeReview
    {
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/enemy-exchange"));
        public static void Before(){foreach(int attack in new[]{1,2})foreach(bool guard in new[]{true,false})Run("before","Tiga",30,attack,guard,true);}
        public static void Release(){After();Flow();}
        public static void After()
        {
            foreach(string hero in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})
                foreach(int rate in hero=="Tiga"?new[]{15,30,60}:new[]{60})
                    foreach(int attack in new[]{1,2})foreach(bool guard in new[]{true,false})Run("after",hero,rate,attack,guard,hero=="Tiga"&&rate==30);
        }
        static Battle Ready(int attack,bool charged=false)
        {
            var state=new Battle();state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
            if(charged)
            {
                for(int f=0;f<(Battle.TransformationSeconds+.2f)/.02f;f++)state.Tick(.02f,new PlayerInput{Tracking=true});
                for(int hit=0;hit<15;hit++){state.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});for(int f=0;f<25;f++)state.Tick(.02f,new PlayerInput{Tracking=true});}
                if(state.Energy<Battle.MaxEnergy)throw new Exception("Beam not charged");
            }
            for(int f=0;f<4000;f++)
            {
                if(state.Enemy==EnemyPhase.Windup&&state.EnemyAttackCount==attack-1&&state.WarningDuration-state.EnemyAge<=.60f)break;
                state.Tick(.01f,new PlayerInput{Tracking=true,Shield=true});
            }
            if(state.Enemy!=EnemyPhase.Windup||state.EnemyAttackCount!=attack-1)throw new Exception("No requested attack prepared");
            while(state.TryCue(out _)){}return state;
        }
        public static void Flow()
        {
            Directory.CreateDirectory(Folder);File.Delete(Folder+"/flow-validation.txt");var report=new StringBuilder();
            foreach(int rate in new[]{15,30,60})foreach(string mode in new[]{"instruction","extended","pause","new-round","reset","showcase","beam"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var world=new GameWorld();var state=Ready(1,mode=="beam");
                float dt=1f/rate,time=0;world.Tick(state,1,0);
                void Step(bool beam=false){state.Tick(world.BattleDelta(dt,state),new PlayerInput{Tracking=true,Beam=beam});while(state.TryCue(out var cue))world.Cue(cue,state);world.Tick(state,dt,time);time+=dt;}
                for(int f=0;f<Mathf.CeilToInt(rate*.48f);f++)Step();
                if(world.ExchangeFocus<.85f||state.Enemy!=EnemyPhase.Windup)throw new Exception("No late-warning camera exercised");
                if(mode=="instruction")state.GiveInstructionTime(3);if(mode=="extended")state.GiveInstructionTime(3,true);
                if(mode=="pause")state.Pause();if(mode=="new-round")state=new Battle();
                if(mode=="reset"){world.ResetPresentation();if(world.ExchangeFocus!=0)throw new Exception("Reset retained camera");state=new Battle();}
                if(mode=="showcase")world.Showcase=true;
                float returnSpeed=0;bool beamSeen=false;
                for(int f=0;f<rate*2;f++)
                {
                    var old=world.Camera.transform.position;Step(mode=="beam"&&f==0);beamSeen|=state.Action==HeroAction.Beam;
                    if(mode=="instruction"||mode=="extended")returnSpeed=Mathf.Max(returnSpeed,Vector3.Distance(old,world.Camera.transform.position)/dt);
                    if(f>rate/2&&world.ExchangeFocus>.001f)throw new Exception("Cancelled camera remained: "+mode);
                    if((mode=="pause"||mode=="new-round"||mode=="reset"||mode=="showcase"||state.Action==HeroAction.Beam)&&world.ExchangeFocus!=0)
                        throw new Exception("Exclusive shot retained exchange camera");
                    var p=world.Camera.transform.position;var q=world.Camera.transform.rotation;float lens=world.Camera.fieldOfView;world.Tick(state,0,time-dt);
                    if(Vector3.Distance(p,world.Camera.transform.position)>.0001f||Quaternion.Angle(q,world.Camera.transform.rotation)>.05f||Mathf.Abs(lens-world.Camera.fieldOfView)>.001f)
                        throw new Exception("Repeated sample changed cancellation shot");
                }
                if(returnSpeed>6||mode=="beam"&&!beamSeen)throw new Exception("Camera cancellation failed");
                report.AppendLine($"{rate}Hz {mode} passed returnSpeed={returnSpeed:F3} beam={beamSeen} zeroTime=passed");
            }
            File.WriteAllText(Folder+"/flow-validation.txt",report.ToString());Debug.Log("[EnemyExchangeFlow]\n"+report);
        }
        static float Coverage(Camera camera)
        {
            var backdrop=GameObject.Find("Realistic Mount Fuji night backdrop").transform;var plane=new Plane(backdrop.forward,backdrop.position);float max=0;
            for(int x=0;x<2;x++)for(int y=0;y<2;y++)
            {var ray=camera.ViewportPointToRay(new Vector3(x,y,0));if(!plane.Raycast(ray,out float distance))throw new Exception("Backdrop behind camera");var p=backdrop.InverseTransformPoint(ray.GetPoint(distance));max=Mathf.Max(max,Mathf.Abs(p.x),Mathf.Abs(p.y));}
            return max;
        }
        static void Run(string version,string id,int rate,int attack,bool guard,bool film)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(938);
            var world=new GameWorld();var state=Ready(attack);var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            string folder=$"{Folder}/{version}/{id}-{rate}-{attack}-{(guard?"guard":"hurt")}";Directory.CreateDirectory(folder+"/frames");File.Delete(folder+"/validation.txt");
            var target=new RenderTexture(1280,720,24){antiAliasing=4};target.Create();world.Camera.targetTexture=target;world.Camera.aspect=16f/9;
            hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,1,0);
            float dt=1f/rate,minX=1,maxX=0,minY=1,maxY=0,handLow=1,handHigh=0,edge=0,speed=0,maxAngle=0,attackTop=0,attackBottom=1;
            int headerOverlaps=0,guideOverlaps=0;
            int oldBlocks=state.Blocks,oldHurt=state.HitsTaken;var mesh=new Mesh();var csv=new StringBuilder("frame,enemy,enemyAge,action,actionAge,health,blocks,hurt\n");
            var cameraCsv=new StringBuilder("frame,x,y,z,fov,top,bottom\n");
            try
            {
                for(int f=0;f<rate*4;f++)
                {
                    var old=world.Camera.transform.position;var oldRotation=world.Camera.transform.rotation;
                    state.Tick(world.BattleDelta(dt,state),new PlayerInput{Tracking=true,Shield=guard});
                    while(state.TryCue(out var cue))world.Cue(cue,state);
                    hero.Update(state,world.Camera,dt,f*dt);enemy.Update(state,world.Camera,dt,f*dt);world.Tick(state,dt,f*dt);
                    speed=Mathf.Max(speed,Vector3.Distance(old,world.Camera.transform.position)/dt);maxAngle=Mathf.Max(maxAngle,Quaternion.Angle(oldRotation,world.Camera.transform.rotation)/dt);edge=Mathf.Max(edge,Coverage(world.Camera));
                    float top=0,bottom=1;
                    bool exchange=state.Enemy==EnemyPhase.Attack||state.Action==HeroAction.Hurt;
                    if(exchange&&f%Math.Max(1,rate/30)==0)
                    {
                        foreach(var actor in new[]{hero,enemy})foreach(var skin in actor.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {skin.BakeMesh(mesh,true);foreach(var v in mesh.vertices){var p=world.Camera.WorldToViewportPoint(skin.transform.TransformPoint(v));minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);bottom=Mathf.Min(bottom,p.y);top=Mathf.Max(top,p.y);
                            // Real plate and guide extents from ArcadeHud, in its
                            // 1280x720 canvas. Test mesh vertices, not loose AABBs.
                            float x=p.x*1280,y=(1-p.y)*720;
                            if(y<100&&(x>=24&&x<=448||x>=832&&x<=1256))headerOverlaps++;
                            if(state.Enemy==EnemyPhase.Attack&&x>=370&&x<=910&&y>=638&&y<=703)guideOverlaps++;
                        }}
                        minY=Mathf.Min(minY,bottom);maxY=Mathf.Max(maxY,top);
                        if(state.Enemy==EnemyPhase.Attack){attackTop=Mathf.Max(attackTop,top);attackBottom=Mathf.Min(attackBottom,bottom);}
                        foreach(var hand in new[]{hero.HandPosition,hero.StrikeOrigin(HeroAction.LeftPunch),enemy.HandPosition,enemy.StrikeOrigin(HeroAction.LeftPunch)})
                        {float y=world.Camera.WorldToViewportPoint(hand).y;handLow=Mathf.Min(handLow,y);handHigh=Mathf.Max(handHigh,y);}
                    }
                    csv.AppendLine(FormattableString.Invariant($"{f},{state.Enemy},{state.EnemyAge:F4},{state.Action},{state.ActionAge:F4},{state.EnemyHealth},{state.Blocks},{state.HitsTaken}"));
                    var pos=world.Camera.transform.position;cameraCsv.AppendLine(FormattableString.Invariant($"{f},{pos.x:F5},{pos.y:F5},{pos.z:F5},{world.Camera.fieldOfView:F5},{top:F4},{bottom:F4}"));
                    if(film)CharacterReview.Save(world.Camera,target,$"{folder}/frames/{f:D4}.png");
                    if(f==Mathf.RoundToInt(rate*.95f)||f==Mathf.RoundToInt(rate*1.38f)||f==rate*3)CharacterReview.Save(world.Camera,target,$"{folder}/stage-{f}.png");
                    var rotation=world.Camera.transform.rotation;float fov=world.Camera.fieldOfView;world.Tick(state,0,f*dt);
                    if(Vector3.Distance(pos,world.Camera.transform.position)>.0001f||Quaternion.Angle(rotation,world.Camera.transform.rotation)>.05f||Mathf.Abs(fov-world.Camera.fieldOfView)>.001f)
                        throw new Exception("Repeated render moved camera");
                }
                string report=$"{id}/{rate}/{attack}/{guard} viewport=({minX:F4},{minY:F4})-({maxX:F4},{maxY:F4}) attackY={attackBottom:F4}..{attackTop:F4} headerOverlaps={headerOverlaps} guideAttackOverlaps={guideOverlaps} hands={handLow:F4}..{handHigh:F4} backdrop={edge:F4} speed={speed:F4} angularSpeed={maxAngle:F4} blocks={state.Blocks-oldBlocks} hurt={state.HitsTaken-oldHurt}";
                Debug.Log("[EnemyExchangeReview] "+version+" "+report);
                if(state.Blocks-oldBlocks!=(guard?1:0)||state.HitsTaken-oldHurt!=(guard?0:1)||state.EnemyHealth!=50)throw new Exception("Combat changed: "+report);
                if(version=="after"&&(headerOverlaps>0||guideOverlaps>0||minY<.06f||maxY>.94f||minX<.07f||maxX>.93f||edge>.49f||speed>6))
                    throw new Exception("Attack framing or return failed: "+report);
                File.WriteAllText(folder+"/sequence.csv",csv.ToString());File.WriteAllText(folder+"/camera.csv",cameraCsv.ToString());
                File.WriteAllText(folder+"/validation.txt",report+" passed");
                var sources=new StringBuilder();using(var sha=System.Security.Cryptography.SHA256.Create())
                    foreach(string file in new[]{"Scripts/Runtime/GameWorld.cs","Scripts/Runtime/RiggedActor.cs","Scripts/Runtime/ArcadeHud.cs","Scripts/Core/Battle.cs","Scripts/Core/EnemyExchangeMotion.cs",$"Resources/Characters/{id}/{id}.fbx","Resources/Characters/Golza/Golza.fbx","Resources/Characters/Golza/GolzaBodyHD.png","Editor/EnemyExchangeReview.cs"})
                        if(File.Exists(Path.Combine(Application.dataPath,file)))sources.AppendLine(file+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Application.dataPath,file)))).Replace("-","").ToLowerInvariant());
                File.WriteAllText(folder+"/sources.txt",sources.ToString());
            }
            finally{world.Camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(mesh);}
        }
    }
}
