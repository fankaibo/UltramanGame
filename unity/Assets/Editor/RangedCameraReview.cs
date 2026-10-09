using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class RangedCameraReview
    {
        public static void Before()=>Run(false);
        public static void After()=>Run(true);
        public static void WaitingCoverage()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var world=new GameWorld();world.Camera.aspect=16f/9;var b=new Battle();
            for(int f=0;f<180;f++)world.Tick(b,1/60f,f/60f);
            Debug.Log($"[RangedCameraWaitingCoverage] untouchedWaitingEdge={Coverage(world.Camera):F5} rangedFocus={Focus(world):F5}");
        }
        public static void Lifecycle()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/ranged-camera-20261010/lifecycle"));Directory.CreateDirectory(folder);
            var report=new StringBuilder();
            foreach(int hz in new[]{15,30,60})foreach(string mode in new[]{"guard","melee","combo","warning","pause","showcase","reset","new-round","beam"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var world=new GameWorld();var b=new Battle(200);
                b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});if(mode!="warning")b.GiveInstructionTime(20);
                if(mode=="beam"||mode=="combo")for(int n=0;n<(mode=="beam"?15:2);n++){b.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});for(int f=0;f<30;f++)b.Tick(.02f,new PlayerInput{Tracking=true});b.GiveInstructionTime(20);}
                var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);while(b.TryCue(out _)){}
                world.Camera.aspect=16f/9;float dt=1f/hz,time=0;
                void Step(PlayerInput input)
                {b.Tick(world.BattleDelta(dt,b),input);while(b.TryCue(out var cue))world.Cue(cue,b);hero.Update(b,world.Camera,dt,time);enemy.Update(b,world.Camera,dt,time);world.Tick(b,dt,time);time+=dt;}
                for(int f=0;f<hz*2;f++)Step(new PlayerInput{Tracking=true,RangedAttack=true,LeftPunch=f==0,RightPunch=f==hz});
                if(Focus(world)<.9f)throw new Exception("Lifecycle never entered volley");
                float maximumSpeed=0,edge=0;int eventFrame=mode=="warning"?-1:0;bool beam=false;
                if(mode=="pause")b.Pause();if(mode=="showcase")world.Showcase=true;if(mode=="reset")world.ResetPresentation();if(mode=="new-round")b=new Battle();
                for(int f=0;f<hz*8;f++)
                {
                    var input=new PlayerInput{Tracking=true,Shield=mode=="guard",LeftPunch=(mode=="melee"||mode=="combo")&&f==0,Beam=mode=="beam"&&f==0};
                    if(mode=="warning"&&b.Enemy==EnemyPhase.Rest){input.RangedAttack=true;input.RightPunch=f%Math.Max(1,hz/2)==0;}
                    var old=world.Camera.transform.position;Step(input);beam|=b.Action==HeroAction.Beam;
                    if(mode=="warning"&&eventFrame<0&&b.Enemy==EnemyPhase.Windup)eventFrame=f;
                    maximumSpeed=Mathf.Max(maximumSpeed,Vector3.Distance(old,world.Camera.transform.position)/dt);edge=Mathf.Max(edge,Coverage(world.Camera));
                    if((mode=="pause"||mode=="showcase"||mode=="reset"||mode=="new-round"||mode=="beam")&&Focus(world)!=0)throw new Exception("Exclusive lens retained volley: "+mode);
                    if(eventFrame>=0&&f-eventFrame>=hz*2)break;
                }
                string line=$"{hz}Hz {mode} focus={Focus(world):F5} returnSpeed={maximumSpeed:F3} backdrop={edge:F3} beam={beam}";report.AppendLine(line);
                // The untouched 37-degree waiting lens reaches .491 in the
                // background quad. Check real coverage (<.5), retaining a
                // small margin, rather than a battle-only .49 composition.
                if(eventFrame<0||Focus(world)>.001f||edge>.499f||mode=="beam"&&!beam||(mode=="guard"||mode=="melee"||mode=="combo"||mode=="warning")&&maximumSpeed>18)throw new Exception("Camera lifecycle failed: "+line);
            }
            File.WriteAllText(folder+"/validation.txt",report.ToString());Debug.Log("[RangedCameraLifecycle] PASS\n"+report);
        }
        static float Focus(GameWorld world)=>(float)(typeof(GameWorld).GetProperty("RangedFocus")?.GetValue(world)??0f);
        static float Coverage(Camera camera)
        {
            var backdrop=GameObject.Find("Realistic Mount Fuji night backdrop").transform;var plane=new Plane(backdrop.forward,backdrop.position);float max=0;
            for(int x=0;x<2;x++)for(int y=0;y<2;y++)
            {var ray=camera.ViewportPointToRay(new Vector3(x,y,0));if(!plane.Raycast(ray,out float distance))throw new Exception("Backdrop behind camera");var point=backdrop.InverseTransformPoint(ray.GetPoint(distance));max=Mathf.Max(max,Mathf.Abs(point.x),Mathf.Abs(point.y));}
            return max;
        }
        static void Run(bool check)
        {
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/ranged-camera-20261010"));
            string folder=root+(check?"/after":"/before");Directory.CreateDirectory(folder);
            var report=new StringBuilder();var gameplay=new StringBuilder();
            foreach(string id in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})foreach(int hz in id=="Tiga"?new[]{15,30,60}:new[]{60})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(1070);
                var world=new GameWorld();var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);
                world.BindActors(hero,enemy);world.SetHeroProfile(id);var b=new Battle();
                b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});b.GiveInstructionTime(30);while(b.TryCue(out _)){}
                var target=new RenderTexture(1280,720,24){antiAliasing=4};target.Create();world.Camera.targetTexture=target;world.Camera.aspect=16f/9;
                var mesh=new Mesh();bool movie=id=="Tiga"&&hz==60;string path=folder+"/"+id+"-"+hz;Directory.CreateDirectory(path);if(movie)Directory.CreateDirectory(path+"/frames");
                float dt=1f/hz,time=0;void Step(PlayerInput input){b.Tick(dt,input);while(b.TryCue(out var cue))world.Cue(cue,b);hero.Update(b,world.Camera,dt,time);enemy.Update(b,world.Camera,dt,time);world.Tick(b,dt,time);time+=dt;}
                for(int f=0;f<hz*2;f++)Step(new PlayerInput{Tracking=true});
                float Size()=>Mathf.Abs(world.Camera.WorldToViewportPoint(world.HeroHome+Vector3.up*3.3f).y-world.Camera.WorldToViewportPoint(world.HeroHome+Vector3.up*.2f).y);
                float size=Size(),magnification=1,minX=1,maxX=0,minY=1,maxY=0,upperMinY=1,handMinY=1,edge=0,maxSpeed=0,minHeld=1;int overlaps=0;
                float lateralMin=100,lateralMax=-100;var cameraTrace=new StringBuilder("frame,focus,x,y,z,fov\n");
                try
                {
                    for(int frame=0;frame<hz*7;frame++)
                    {
                        var input=new PlayerInput{Tracking=true};for(int n=0;n<5;n++)if(frame==Mathf.RoundToInt((.6f+n*.8f)*hz))
                        {input.RangedAttack=true;input.LeftPunch=n%2==0;input.RightPunch=n%2==1;input.AttackSpeed=n==0?.65f:n==4?1.7f:1;}
                        var old=world.Camera.transform.position;Step(input);
                        maxSpeed=Mathf.Max(maxSpeed,Vector3.Distance(old,world.Camera.transform.position)/dt);
                        float focus=Focus(world);magnification=Mathf.Max(magnification,Size()/size);edge=Mathf.Max(edge,Coverage(world.Camera));
                        if(frame>=hz*2&&frame<hz*4){minHeld=Mathf.Min(minHeld,focus);lateralMin=Mathf.Min(lateralMin,world.Camera.transform.position.x);lateralMax=Mathf.Max(lateralMax,world.Camera.transform.position.x);}
                        if(frame%Mathf.Max(1,hz/15)==0)foreach(var actor in new[]{hero,enemy})foreach(var skin in actor.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {
                            skin.BakeMesh(mesh,true);foreach(var vertex in mesh.vertices)
                            {
                                var position=skin.transform.TransformPoint(vertex);var p=world.Camera.WorldToViewportPoint(position);minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);minY=Mathf.Min(minY,p.y);maxY=Mathf.Max(maxY,p.y);
                                if(position.y>actor.Root.position.y+1.25f)upperMinY=Mathf.Min(upperMinY,p.y);
                                var screen=new Vector2(p.x*1280,(1-p.y)*720);if(BattleHudLayout.HeroPlate.Contains(screen)||BattleHudLayout.EnemyPlate.Contains(screen))overlaps++;
                            }
                        }
                        handMinY=Mathf.Min(handMinY,world.Camera.WorldToViewportPoint(hero.HandPosition).y,world.Camera.WorldToViewportPoint(hero.StrikeOrigin(HeroAction.LeftPunch)).y);
                        var pos=world.Camera.transform.position;var rot=world.Camera.transform.rotation;float fov=world.Camera.fieldOfView;
                        world.Tick(b,0,time-dt);if(Vector3.Distance(pos,world.Camera.transform.position)>.0001f||Quaternion.Angle(rot,world.Camera.transform.rotation)>.05f||Mathf.Abs(fov-world.Camera.fieldOfView)>.001f)throw new Exception("Repeated camera sample drifts");
                        gameplay.AppendLine(FormattableString.Invariant($"{id},{hz},{frame},{b.Action},{b.ActionAge:F5},{b.Shot.Age:F5},{b.Punches},{b.EnemyHealth},{b.Energy},{b.Shot.Sequence}"));
                        cameraTrace.AppendLine(FormattableString.Invariant($"{frame},{focus:F5},{pos.x:F5},{pos.y:F5},{pos.z:F5},{fov:F5}"));
                        if(movie&&frame%2==0)CharacterReview.Save(world.Camera,target,path+"/frames/"+(frame/2).ToString("D4")+".png");
                        if(frame==hz*3)CharacterReview.Save(world.Camera,target,path+"/volley.png");
                    }
                    string line=$"{id}/{hz} hits={b.Punches} scale={magnification:F3} held={minHeld:F3} rangeX={minX:F3}..{maxX:F3} rangeY={minY:F3}..{maxY:F3} upperMinY={upperMinY:F3} handsMinY={handMinY:F3} hudOverlap={overlaps} backdrop={edge:F3} cameraSpeed={maxSpeed:F3} lateralSwing={lateralMax-lateralMin:F3} residual={Focus(world):F5}";
                    report.AppendLine(line);Debug.Log("[RangedCameraReview] "+line);
                    if(b.Punches!=5||b.EnemyHealth!=45||b.Energy!=5)throw new Exception("Missing ranged shots");
                    if(check&&(magnification<1.20f||minHeld<.95f||minX<.04f||maxX>.96f||upperMinY<.10f||handMinY<.16f||maxY>.98f||overlaps>0||edge>.49f||maxSpeed>14||Focus(world)>.001f))throw new Exception("Volley framing or continuity failed: "+line);
                    File.WriteAllText(path+"/camera.csv",cameraTrace.ToString());
                }
                finally{world.Camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(mesh);}
            }
            File.WriteAllText(folder+"/validation.txt",report.ToString());File.WriteAllText(folder+"/gameplay.csv",gameplay.ToString());
            if(check&&File.ReadAllText(root+"/before/gameplay.csv")!=gameplay.ToString())throw new Exception("Camera changed gameplay");
            Debug.Log("[RangedCameraReview] PASS "+folder);
        }
    }
}
