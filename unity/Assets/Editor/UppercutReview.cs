using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class UppercutReview
    {
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/uppercut"));
        public static void Rapid(){var report=new StringBuilder();foreach(string id in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})foreach(int rate in id=="Tiga"?new[]{15,30,60}:new[]{30})foreach(bool left in new[]{true,false})Run(id,rate,left,"rapid",report);}
        public static void Release()
        {
            Validate();ComboStrikeReview.After();ComboStrikeReview.Validate();
            ComboStrikeReview.Interruptions();MonsterBackstepReview.Validate();
        }
        public static void Validate()
        {
            Directory.CreateDirectory(Folder+"/validation");File.Delete(Folder+"/launch-validation.txt");var report=new StringBuilder();
            foreach(string id in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})foreach(int rate in id=="Tiga"?new[]{15,30,60}:new[]{30})
                foreach(bool left in new[]{true,false})foreach(string mode in new[]{"normal","rapid"})Run(id,rate,left,mode,report);
            foreach(int rate in new[]{15,30,60})foreach(string mode in new[]{"pause","new-round","beam","late-warning"})
                Run("Tiga",rate,true,mode,report);
            File.WriteAllText(Folder+"/launch-validation.txt",report.ToString());
            var sources=new StringBuilder();using(var sha=System.Security.Cryptography.SHA256.Create())
                foreach(string file in new[]{"Scripts/Core/MonsterLaunchMotion.cs","Scripts/Runtime/RiggedActor.cs","Scripts/Runtime/AnimatedActor.cs","Scripts/Runtime/GameWorld.cs","Editor/UppercutReview.cs"})
                    sources.AppendLine(file+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Application.dataPath,file)))).Replace("-","").ToLowerInvariant());
            File.WriteAllText(Folder+"/validation-sources.txt",sources.ToString());
        }
        static void Run(string id,int rate,bool left,string mode,StringBuilder report)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(940);
            var world=new GameWorld();var state=new Battle(80);state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
            for(int f=0;f<140;f++)state.Tick(.02f,new PlayerInput{Tracking=true});state.GiveInstructionTime(30);
            for(int n=0;n<(mode=="beam"?19:9);n++){state.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});for(int f=0;f<25;f++)state.Tick(.02f,new PlayerInput{Tracking=true});}
            if(mode=="late-warning")for(int f=0;f<6000;f++){state.Tick(.01f,new PlayerInput{Tracking=true});if(state.Enemy==EnemyPhase.Windup&&state.WarningDuration-state.EnemyAge<.80f)break;}
            while(state.TryCue(out _)){}
            var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,1,0);
            var target=new RenderTexture(1280,720,24){antiAliasing=4};target.Create();world.Camera.targetTexture=target;world.Camera.aspect=16f/9;
            var mesh=new Mesh();Vector3 restL=enemy.FootPosition(true),restR=enemy.FootPosition(false),landL=default,landR=default;
            float lift=0,ground=100,slide=0,low=1,high=0,dt=1f/rate,health=state.EnemyHealth;
            int contacts=0,landingFx=0,header=0;bool stopped=false,airShot=false,landShot=false,recoverShot=false,beamed=false,landing=false,airCounter=false;
            float counterGap=0,heroGround=100;
            int punches=state.Punches;string prefix=$"{Folder}/validation/{id}-{rate}-{left}-{mode}";
            bool film=id=="Tiga"&&rate==30&&mode=="normal";
            string frames=$"{Folder}/shots/{(left?"left":"right")}/frames";if(film)Directory.CreateDirectory(frames);
            try
            {
                for(int f=0;f<rate*4;f++)
                {
                    if(!stopped&&enemy.LaunchAge>.26f&&enemy.LaunchAge<.5f)
                    {stopped=true;if(mode=="pause")state.Pause();if(mode=="new-round"){state=new Battle();health=state.EnemyHealth;world.ResetPresentation();}}
                    bool punch=f==0||mode=="rapid"&&f==Mathf.CeilToInt(rate*.45f);
                    var input=new PlayerInput{Tracking=!(mode=="pause"&&stopped),LeftPunch=punch&&left,RightPunch=punch&&!left,
                        Beam=mode=="beam"&&stopped,Shield=mode=="late-warning"&&f>rate/3};
                    state.Tick(world.BattleDelta(dt,state),input);while(state.TryCue(out var cue))world.Cue(cue,state);
                    hero.Update(state,world.Camera,dt,f*dt);enemy.Update(state,world.Camera,dt,f*dt);
                    var root=enemy.Root.position;var l=enemy.FootPosition(true);var r=enemy.FootPosition(false);var hand=enemy.HandPosition;
                    enemy.Update(state,world.Camera,0,f*dt);
                    if(Vector3.Distance(root,enemy.Root.position)>.001f||Vector3.Distance(l,enemy.FootPosition(true))>.001f||Vector3.Distance(r,enemy.FootPosition(false))>.001f||enemy.LaunchAge<10&&Vector3.Distance(hand,enemy.HandPosition)>.015f)
                        throw new Exception($"Launch accumulates on repeated sampling: {id}/{rate}/{left}/{mode} age={enemy.LaunchAge} root={Vector3.Distance(root,enemy.Root.position)} left={Vector3.Distance(l,enemy.FootPosition(true))} right={Vector3.Distance(r,enemy.FootPosition(false))} hand={Vector3.Distance(hand,enemy.HandPosition)}");
                    if(state.EnemyHealth<health)
                    {
                        contacts++;world.Hit(state.Action==HeroAction.Beam,state);
                        if(mode=="rapid"&&contacts==2)
                        {
                            airCounter=enemy.LaunchAge<MonsterLaunchMotion.Landing;
                            counterGap=100;Vector3 fist=hero.StrikeOrigin(state.Action)+world.BattleAxis*.12f;
                            foreach(var skin in enemy.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                            {skin.BakeMesh(mesh,true);foreach(var v in mesh.vertices)counterGap=Mathf.Min(counterGap,Vector3.Distance(fist,skin.transform.TransformPoint(v)));}
                            CharacterReview.Save(world.Camera,target,prefix+"-counter.png");
                            Debug.Log($"[UppercutCounter] {id}/{rate} fist={fist} chest={enemy.BeamSurfaceContact} age={enemy.LaunchAge:F3} gap={counterGap:F4}");
                        }
                    }
                    health=state.EnemyHealth;
                    int oldGround=world.GroundContactCount;world.Tick(state,dt,f*dt);
                    if(world.GroundContactCount>oldGround&&world.GroundContactCause=="uppercut-land")landingFx++;
                    beamed|=state.Action==HeroAction.Beam;
                    lift=Mathf.Max(lift,Mathf.Min(l.y-restL.y,r.y-restR.y));
                    if(mode=="late-warning"&&enemy.LaunchAge<10)throw new Exception("Launch stole incoming claw/guard");
                    if(enemy.LaunchAge>=MonsterLaunchMotion.Landing&&enemy.LaunchAge<MonsterLaunchMotion.Recovery)
                    {
                        if(!landing){landL=l;landR=r;landing=true;}
                        slide=Mathf.Max(slide,Vector3.Distance(l,landL),Vector3.Distance(r,landR));
                    }
                    if(mode=="normal")foreach(var skin in enemy.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        skin.BakeMesh(mesh,true);foreach(var v in mesh.vertices)
                        {
                            var point=skin.transform.TransformPoint(v);ground=Mathf.Min(ground,point.y);
                            var screen=world.Camera.WorldToViewportPoint(point);low=Mathf.Min(low,screen.y);high=Mathf.Max(high,screen.y);
                            float x=screen.x*1280,y=(1-screen.y)*720;
                            if(y<100&&(x>=24&&x<=448||x>=832&&x<=1256))header++;
                        }
                    }
                    if(mode=="rapid")foreach(var skin in hero.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {skin.BakeMesh(mesh,true);foreach(var v in mesh.vertices)heroGround=Mathf.Min(heroGround,skin.transform.TransformPoint(v).y);}
                    if(!airShot&&enemy.LaunchAge>=.28f&&enemy.LaunchAge<.5f){CharacterReview.Save(world.Camera,target,prefix+"-air.png");airShot=true;}
                    if(!landShot&&enemy.LaunchAge>=MonsterLaunchMotion.Landing&&enemy.LaunchAge<MonsterLaunchMotion.Recovery){CharacterReview.Save(world.Camera,target,prefix+"-land.png");landShot=true;}
                    if(!recoverShot&&enemy.LaunchAge>=MonsterLaunchMotion.Recovery+.13f&&enemy.LaunchAge<MonsterLaunchMotion.Duration){CharacterReview.Save(world.Camera,target,prefix+"-recover.png");recoverShot=true;}
                    if(film)CharacterReview.Save(world.Camera,target,$"{frames}/{f:D4}.png");
                }
                string line=$"{id}/{rate}/{left}/{mode} lift={lift:F4} ground={ground:F4} landingSlide={slide:F4} viewportY={low:F4}..{high:F4} header={header} contacts={contacts} landings={enemy.LaunchLandings} landingFx={landingFx} airCounter={airCounter} counterGap={counterGap:F4} heroGround={heroGround:F4}";
                Debug.Log("[UppercutReview] "+line);
                if(mode=="normal"&&(lift<.55f||ground<-.04f||slide>.025f||low<.045f||high>.94f||header>0||contacts!=1||enemy.LaunchLandings!=1||landingFx!=1||!recoverShot||state.Punches!=punches+1))
                    throw new Exception("Airborne contact/landing/framing failed: "+line);
                if(mode=="rapid"&&(contacts!=2||enemy.LaunchLandings!=1||landingFx!=1||!airCounter||counterGap>.38f||heroGround<-.04f))throw new Exception("Airborne follow-up failed: "+line);
                if((mode=="pause"||mode=="new-round"||mode=="beam")&&(!stopped||enemy.LaunchAge<10||enemy.LaunchLandings!=0||landingFx!=0))throw new Exception("Cancelled launch left residual landing");
                if(mode=="beam"&&!beamed)throw new Exception("Beam failed to supersede launch");
                if(mode=="late-warning"&&(state.Blocks!=1||state.HitsTaken!=0||landingFx!=0))throw new Exception("Incoming guard changed");
                report.AppendLine(line+" zeroTime=passed passed");
            }
            finally{world.Camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(mesh);}
        }
    }
}
