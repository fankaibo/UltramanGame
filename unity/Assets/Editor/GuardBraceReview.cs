using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class GuardBraceReview
    {
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/guard-brace"));
        public static void Before(){Run("before","Tiga",30,"hold",true);}
        public static void Release()
        {
            foreach(string id in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})
                foreach(int rate in new[]{15,30,60})Run("after",id,rate,"hold",id=="Tiga"&&rate==30);
            foreach(string id in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})
                foreach(int rate in id=="Tiga"?new[]{15,30,60}:new[]{30})foreach(string mode in new[]{"release","left","right","beam","pause","reset"})
                    Run("after",id,rate,mode,id=="Tiga"&&rate==30&&mode=="left");
            RosterPunchReview.CheckGuardTransitions();
        }
        static Transform Bone(Transform root,string a,string b)
        {foreach(var joint in root.GetComponentsInChildren<Transform>())if(joint.name==a||joint.name==b)return joint;throw new Exception("Missing bone "+a);}
        static Battle Ready(bool charged)
        {
            var state=new Battle();state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
            for(int f=0;f<120;f++)state.Tick(.02f,new PlayerInput{Tracking=true});
            if(charged)
            {
                state.GiveInstructionTime(20);
                for(int h=0;h<15;h++){state.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});for(int f=0;f<30;f++)state.Tick(.02f,new PlayerInput{Tracking=true});}
                if(state.Energy!=Battle.MaxEnergy)throw new Exception("Beam not charged");
            }
            for(int f=0;f<5000;f++)
            {
                state.Tick(.01f,new PlayerInput{Tracking=true,Shield=true});
                if(state.Enemy==EnemyPhase.Windup&&state.WarningDuration-state.EnemyAge<.60f)break;
            }
            if(state.Enemy!=EnemyPhase.Windup)throw new Exception("No warning prepared");
            while(state.TryCue(out _)){}return state;
        }
        static void Run(string version,string id,int rate,string mode,bool film)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(2828);
            var world=new GameWorld();var state=Ready(mode=="beam");var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);
            var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            string folder=$"{Folder}/{version}/{id}-{rate}-{mode}";Directory.CreateDirectory(folder+"/frames");File.Delete(folder+"/validation.txt");
            var target=new RenderTexture(1280,720,24){antiAliasing=4};target.Create();world.Camera.targetTexture=target;world.Camera.aspect=16f/9;
            hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,1,0);
            var thigh=Bone(hero.Root,"ThighBase_L","bip_hip_L");var knee=Bone(hero.Root,"Shin_L","bip_knee_L");
            float Angle()=>Vector3.Angle(thigh.position-knee.position,hero.FootPosition(true)-knee.position);
            Vector3 left=hero.FootPosition(true),right=hero.FootPosition(false);float initialKnee=Angle();
            float dt=1f/rate,age=-1,maxBack=0,maxDrop=0,maxBend=0,feet=0,rootSpeed=0,settled=100;bool switched=false,consumed=false;
            int blocks=state.Blocks,punches=state.Punches;var csv=new StringBuilder("frame,enemy,enemyAge,action,actionAge,health,blocks\n");
            var oldRoot=hero.Root.position;bool captured=false;
            try
            {
                for(int f=0;f<rate*3;f++)
                {
                    bool handoff=age>=.14f&&mode!="hold"&&!switched;
                    if(handoff){switched=true;if(mode=="pause")state.Pause();if(mode=="reset"){state=new Battle();world.ResetPresentation();}}
                    var input=new PlayerInput{Tracking=!(switched&&mode=="pause"),Shield=!switched,
                        LeftPunch=handoff&&mode=="left",RightPunch=handoff&&mode=="right",Beam=handoff&&mode=="beam"};
                    state.Tick(world.BattleDelta(dt,state),input);while(state.TryCue(out var cue))world.Cue(cue,state);
                    hero.Update(state,world.Camera,dt,f*dt);
                    // Repeat before the opponent moves: its skin is a punch IK target.
                    // Identical inputs must not accumulate pelvis travel or IK.
                    var l=hero.FootPosition(true);var r=hero.FootPosition(false);var p=hero.Root.position;
                    var k=knee.position;var hand=hero.HandPosition;
                    hero.Update(state,world.Camera,0,f*dt);
                    if(Vector3.Distance(l,hero.FootPosition(true))>.001f||Vector3.Distance(r,hero.FootPosition(false))>.001f||
                        Vector3.Distance(p,hero.Root.position)>.001f||Vector3.Distance(k,knee.position)>.001f||Vector3.Distance(hand,hero.HandPosition)>.001f)
                        throw new Exception($"Repeated guard sample moved bones: {id}/{rate}/{mode} at {age} left={Vector3.Distance(l,hero.FootPosition(true))} right={Vector3.Distance(r,hero.FootPosition(false))} root={Vector3.Distance(p,hero.Root.position)} knee={Vector3.Distance(k,knee.position)} hand={Vector3.Distance(hand,hero.HandPosition)}");
                    enemy.Update(state,world.Camera,dt,f*dt);world.Tick(state,dt,f*dt);
                    if(handoff)
                    {
                        consumed=mode=="left"?state.Action==HeroAction.LeftPunch:mode=="right"?state.Action==HeroAction.RightPunch:
                            mode=="beam"?state.Action==HeroAction.Beam:mode=="pause"?state.Phase==GamePhase.Paused:mode=="reset"?state.Phase==GamePhase.Waiting:!state.Shield;
                        if(!consumed)throw new Exception("Brace delayed input: "+mode);
                    }
                    if(age<0&&state.Blocks>blocks)age=0;else if(age>=0)age+=dt;
                    if(age>=0&&!switched)
                    {
                        maxBack=Mathf.Max(maxBack,-Vector3.Dot(hero.Root.position-world.HeroHome,world.BattleAxis));
                        maxDrop=Mathf.Max(maxDrop,world.HeroHome.y-hero.Root.position.y);
                        maxBend=Mathf.Max(maxBend,initialKnee-Angle());
                        feet=Mathf.Max(feet,Vector3.Distance(left,hero.FootPosition(true)),Vector3.Distance(right,hero.FootPosition(false)));
                    }
                    if(!handoff)rootSpeed=Mathf.Max(rootSpeed,Vector3.Distance(oldRoot,hero.Root.position)/dt);oldRoot=hero.Root.position;
                    if(mode=="hold"&&age>.85f)settled=Vector3.Distance(world.HeroHome,hero.Root.position);
                    if(!captured&&age>=.15f){CharacterReview.Save(world.Camera,target,folder+"/impact.png");captured=true;}
                    if(film)CharacterReview.Save(world.Camera,target,$"{folder}/frames/{f:D4}.png");
                    csv.AppendLine(FormattableString.Invariant($"{f},{state.Enemy},{state.EnemyAge:F5},{state.Action},{state.ActionAge:F5},{state.EnemyHealth},{state.Blocks}"));
                }
                string report=$"{version}/{id}/{rate}/{mode} back={maxBack:F4} drop={maxDrop:F4} kneeBend={maxBend:F2} footDrift={feet:F5} settled={settled:F5} rootSpeed={rootSpeed:F3} input={consumed} zeroTime=passed";
                Debug.Log("[GuardBraceReview] "+report);
                if(!captured||mode=="hold"&&(state.Blocks!=blocks+1||feet>.025f||settled>.001f))throw new Exception("Guard contact or stance changed: "+report);
                if(version=="after"&&mode=="hold"&&(maxBack<.10f||maxDrop<.10f||maxBend<8||rootSpeed>3.5f))throw new Exception("Missing full-body brace: "+report);
                if(mode=="left"||mode=="right")if(state.Punches!=punches+1)throw new Exception("Counterpunch did not land");
                if(mode!="hold"&&!consumed)throw new Exception("Handoff not exercised");
                File.WriteAllText(folder+"/validation.txt",report+" passed");File.WriteAllText(folder+"/sequence.csv",csv.ToString());
                var sources=new StringBuilder();using(var sha=System.Security.Cryptography.SHA256.Create())
                    foreach(string file in new[]{"Scripts/Runtime/RiggedActor.cs","Scripts/Runtime/GameWorld.cs","Scripts/Core/Battle.cs",$"Resources/Characters/{id}/{id}.fbx","Editor/GuardBraceReview.cs"})
                        sources.AppendLine(file+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Application.dataPath,file)))).Replace("-","").ToLowerInvariant());
                File.WriteAllText(folder+"/sources.txt",sources.ToString());
            }
            finally{world.Camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);}
        }
    }
}
