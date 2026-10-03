using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class ComboCameraReview
    {
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/combo-camera"));
        static Battle Ready(int punches=0,bool hold=true)
        {
            var state=new Battle();state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});for(int n=0;n<(Battle.TransformationSeconds+0.2f)/(.02f);n++)state.Tick(.02f,new PlayerInput{Tracking=true});
            for(int n=0;n<punches;n++){state.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});for(int f=0;f<25;f++)state.Tick(.02f,new PlayerInput{Tracking=true});}
            if(hold)state.GiveInstructionTime(20);while(state.TryCue(out _)){}return state;
        }
        public static void Release(){Framing();Flow();ComboStrikeReview.After();}
        static float Coverage(Camera camera)
        {
            var backdrop=GameObject.Find("Realistic Mount Fuji night backdrop").transform;var plane=new Plane(backdrop.forward,backdrop.position);float max=0;
            for(int x=0;x<2;x++)for(int y=0;y<2;y++)
            {var ray=camera.ViewportPointToRay(new Vector3(x,y,0));if(!plane.Raycast(ray,out float distance))throw new Exception("Backdrop behind camera");var point=backdrop.InverseTransformPoint(ray.GetPoint(distance));max=Mathf.Max(max,Mathf.Abs(point.x),Mathf.Abs(point.y));}
            return max;
        }
        public static void Framing()
        {
            Directory.CreateDirectory(Folder+"/poses");File.Delete(Folder+"/camera-validation.txt");var report=new StringBuilder();
            foreach(string id in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})foreach(int rate in id=="Tiga"?new[]{15,30,60}:new[]{60})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(933);
                var world=new GameWorld();var state=Ready();var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                float dt=1f/rate,time=0;void Step(PlayerInput input){float health=state.EnemyHealth;state.Tick(world.BattleDelta(dt,state),input);while(state.TryCue(out var cue))world.Cue(cue,state);hero.Update(state,world.Camera,dt,time);enemy.Update(state,world.Camera,dt,time);if(state.EnemyHealth<health)world.Hit(false,state);world.Tick(state,dt,time);time+=dt;}
                for(int n=0;n<rate;n++)Step(new PlayerInput{Tracking=true});
                Vector3 center=world.EnemyHome+Vector3.up*2.5f;float Size()=>Mathf.Abs(world.Camera.WorldToViewportPoint(center+Vector3.up*.2f).y-world.Camera.WorldToViewportPoint(center).y);
                float baseSize=Size(),magnification=0,minX=1,maxX=0,top=0,edge=0,maxSpeed=0,handLow=1,handHigh=0;int shots=0,headerOverlap=0;bool peak=false;
                var target=new RenderTexture(1280,720,24){antiAliasing=4};target.Create();world.Camera.targetTexture=target;world.Camera.aspect=16f/9;var mesh=new Mesh();
                try
                {
                    for(int n=1;n<=10;n++)for(int frame=0;frame<rate;frame++)
                    {
                        Vector3 old=world.Camera.transform.position;Step(new PlayerInput{Tracking=true,LeftPunch=frame==0&&n%2==1,RightPunch=frame==0&&n%2==0});
                        maxSpeed=Mathf.Max(maxSpeed,Vector3.Distance(old,world.Camera.transform.position)/dt);edge=Mathf.Max(edge,Coverage(world.Camera));
                        if(world.ComboFocus>.95f)
                        {
                            if(!peak){shots++;peak=true;CharacterReview.Save(world.Camera,target,$"{Folder}/poses/{id}-{rate}-peak-{n}.png");}
                            magnification=Mathf.Max(magnification,Size()/baseSize);
                            foreach(var actor in new[]{hero,enemy})foreach(var skin in actor.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                            {skin.BakeMesh(mesh,true);foreach(var vertex in mesh.vertices){Vector3 p=skin.transform.TransformPoint(vertex);if(p.y<actor.Root.position.y+1.8f)continue;var v=world.Camera.WorldToViewportPoint(p);minX=Mathf.Min(minX,v.x);maxX=Mathf.Max(maxX,v.x);top=Mathf.Max(top,v.y);var screen=new Vector2(v.x*1280,(1-v.y)*720);if(BattleHudLayout.HeroPlate.Contains(screen)||BattleHudLayout.EnemyPlate.Contains(screen))headerOverlap++;}}
                            foreach(var hand in new[]{hero.HandPosition,hero.StrikeOrigin(HeroAction.LeftPunch),enemy.HandPosition,enemy.StrikeOrigin(HeroAction.LeftPunch)})
                            {var p=world.Camera.WorldToViewportPoint(hand);handLow=Mathf.Min(handLow,p.y);handHigh=Mathf.Max(handHigh,p.y);}
                        }
                        else peak=false;
                        Vector3 position=world.Camera.transform.position;Quaternion rotation=world.Camera.transform.rotation;float fov=world.Camera.fieldOfView;
                        world.Tick(state,0,time-dt);
                        if(Vector3.Distance(position,world.Camera.transform.position)>.0001f||Quaternion.Angle(rotation,world.Camera.transform.rotation)>.05f||Mathf.Abs(fov-world.Camera.fieldOfView)>.001f)
                            throw new Exception($"Camera feeds back on repeated rendering {id}/{rate} focus={world.ComboFocus} age={state.ActionAge}");
                    }
                    string line=$"{id}/{rate}Hz peaks={shots} upperBodyX={minX:F3}..{maxX:F3} top={top:F3} headerOverlap={headerOverlap} handsY={handLow:F3}..{handHigh:F3} backdropEdge={edge:F3} magnification={magnification:F3} maxCameraSpeed={maxSpeed:F3}";Debug.Log("[ComboCameraFraming] "+line);report.AppendLine(line);
                    // Full-body shots use a subtler push-in. Check the actual
                    // side plates rather than forbidding the empty top centre.
                    // PlayfieldReview also checks every vertex down to the feet.
                    if(shots!=2||state.EnemyHealth!=40||state.Energy!=10||world.ComboFocus>.001f||minX<.08f||maxX>.92f||headerOverlap>0||top>1||handLow<.12f||handHigh>.87f||edge>.49f||magnification<1.05f||maxSpeed>14)
                        throw new Exception("Combo camera framing/motion failed: "+line);
                }
                finally{world.Camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(mesh);}
            }
            File.WriteAllText(Folder+"/camera-validation.txt",report.ToString());
        }
        public static void Flow()
        {
            var report=new StringBuilder();File.Delete(Folder+"/flow-validation.txt");
            foreach(int rate in new[]{15,30,60})foreach(string mode in new[]{"guard","beam","pause","new-round","reset","showcase","rapid","warning"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var world=new GameWorld();var state=Ready(mode=="beam"?14:4,mode!="warning");
                float dt=1f/rate,time=0;void Step(PlayerInput input){state.Tick(world.BattleDelta(dt,state),input);while(state.TryCue(out var cue))world.Cue(cue,state);world.Tick(state,dt,time);time+=dt;}
                if(mode=="warning")
                {for(int f=0;f<rate*20&&state.Enemy!=EnemyPhase.Windup;f++)Step(new PlayerInput{Tracking=true});if(state.Enemy!=EnemyPhase.Windup)throw new Exception("No warning exercised");}
                for(int f=0;f<Mathf.CeilToInt(rate*.24f);f++)Step(new PlayerInput{Tracking=true,LeftPunch=f==0});
                if(mode=="warning") {if(world.ComboFocus!=0)throw new Exception("Combo camera overrides guard warning");report.AppendLine(rate+"Hz warning priority=passed");continue;}
                if(world.ComboFocus<.95f)throw new Exception("No peak exercised");
                if(mode=="pause")state.Pause();if(mode=="new-round")state=new Battle();if(mode=="reset"){world.ResetPresentation();if(world.ComboFocus!=0)throw new Exception("Reset retained shot");}if(mode=="showcase")world.Showcase=true;
                float maximumReturn=0;bool beam=false;
                for(int f=0;f<rate*2;f++)
                {
                    var old=world.Camera.transform.position;Step(new PlayerInput{Tracking=true,Shield=mode=="guard",Beam=mode=="beam",RightPunch=mode=="rapid"&&f%Mathf.Max(1,rate/2)==0});beam|=state.Action==HeroAction.Beam;
                    if(mode=="guard")maximumReturn=Mathf.Max(maximumReturn,Vector3.Distance(old,world.Camera.transform.position)/dt);
                    if(((mode=="pause"||mode=="new-round"||mode=="reset"||mode=="showcase")&&f==0||mode=="beam"&&state.Action==HeroAction.Beam)&&world.ComboFocus!=0)
                        throw new Exception($"Exclusive presentation kept combo shot: {mode} phase={state.Phase} action={state.Action}");
                }
                // Rapid inputs can legitimately begin the next fifth-punch shot
                // on the final frame. Test its return after inputs actually stop.
                if(mode=="rapid")for(int f=0;f<rate;f++)Step(new PlayerInput{Tracking=true});
                if(world.ComboFocus>.001f||maximumReturn>14||mode=="beam"&&!beam)
                    throw new Exception($"Interrupted shot persisted or jumped: {rate}Hz {mode} focus={world.ComboFocus:F3} age={world.ComboCameraAge:F3} speed={maximumReturn:F3} beam={beam} punches={state.Punches}");
                report.AppendLine($"{rate}Hz {mode} passed returnSpeed={maximumReturn:F3} punches={state.Punches} beam={beam}");
            }
            File.WriteAllText(Folder+"/flow-validation.txt",report.ToString());Debug.Log("[ComboCameraFlow]\n"+report);
        }
    }
}
