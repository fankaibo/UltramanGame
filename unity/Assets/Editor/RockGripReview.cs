using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class RockGripReview
    {
        static string Folder=>Path.GetFullPath(Environment.GetEnvironmentVariable("ULTRAMAN_ROCK_GRIP_OUTPUT")??"../artifacts/a155-rock-grip");
        public static void Before()=>Run("before",60,true,true);
        public static void After()
        {
            foreach(int hz in new[]{15,30,60})foreach(bool block in new[]{true,false})Run("after",hz,block,hz==60&&block);
            ValidateLifecycle();
        }
        public static void ValidateLifecycle(){Lifecycle();ClawPoseReview.Stability();}
        static void Lifecycle()
        {
            var report=new StringBuilder();
            foreach(int hz in new[]{15,30,60})foreach(bool flight in new[]{false,true})foreach(bool pause in new[]{false,true})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var root=new GameObject("Grip lifecycle").transform;var camera=new GameObject("Camera").AddComponent<Camera>();
                var rock=new MonsterRock(root);var b=new Battle();var grip=new Pose(new Vector3(0,3,0),Quaternion.Euler(13,42,29));
                var target=new Vector3(3,2,0);float dt=1f/hz;
                b.Tick(dt,new PlayerInput{Tracking=true,Transform=true});bool reached=false;
                for(int n=0;n<hz*40;n++)
                {
                    b.Tick(dt,new PlayerInput{Tracking=true,Shield=true});rock.Tick(b,grip,target,false,camera);
                    reached=flight?rock.Flying:rock.Holding&&MonsterRockMotion.Prepare(b)>.95f;
                    if(reached)break;
                }
                if(!reached)throw new Exception("Grip lifecycle phase not reached");
                var stone=(Transform)typeof(MonsterRock).GetField("stone",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(rock);
                Vector3 position=rock.Position;Quaternion rotation=stone.rotation;int launches=rock.Launches;
                if(pause)b.Pause();rock.Tick(b,grip,target,!pause,camera);
                if(stone.gameObject.activeSelf||rock.Holding||rock.Flying)throw new Exception("Suppression left stone visible");
                if(pause)for(int n=0;n<hz*2&&b.Phase==GamePhase.Paused;n++)b.Tick(dt,new PlayerInput{Tracking=true});
                rock.Tick(b,grip,target,false,camera);
                // Tracking pause intentionally cancels an unfinished warning
                // or attack and gives the child a fresh instruction window.
                // A presentation-only suppression retains the attack clock.
                if(pause)
                {
                    if(b.Phase!=GamePhase.Battle||b.Enemy!=EnemyPhase.Rest||stone.gameObject.activeSelf||rock.Launches!=launches)
                        throw new Exception("Pause restored a cancelled throw");
                }
                else if(!stone.gameObject.activeSelf||Vector3.Distance(position,rock.Position)>.0001f||Quaternion.Angle(rotation,stone.rotation)>.05f||rock.Launches!=launches)
                    throw new Exception("Presentation resume moved or relaunched rock");
                rock.Clear();if(stone.gameObject.activeSelf)throw new Exception("Reset retained rock");
                rock.Tick(new Battle(),grip,target,false,camera);
                if(stone.gameObject.activeSelf||rock.Launches!=0)throw new Exception("New round retained throw state");
                report.AppendLine($"{hz}Hz flight={flight} pause={pause} suppression/resume/reset/newRound=passed");
            }
            File.WriteAllText(Path.Combine(Folder,"lifecycle.txt"),report.ToString());Debug.Log("[RockGripLifecycle] PASS\n"+report);
        }
        static Transform Bone(Transform root,string name)
        {foreach(var t in root.GetComponentsInChildren<Transform>())if(t.name==name)return t;throw new Exception(name);}
        static void Run(string version,int hz,bool block,bool movie)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(155);
            var w=new GameWorld();var b=new Battle();var h=new AnimatedActor("Tiga",w.HeroHome,w.EnemyHome);
            var e=new AnimatedActor("Golza",w.EnemyHome,w.HeroHome,true);w.BindActors(h,e);
            b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
            for(int n=0;n<4000;n++)
            {
                if(b.EnemyAttackCount==1&&b.Enemy==EnemyPhase.Windup&&b.WarningDuration-b.EnemyAge<1.7f)break;
                b.Tick(.02f,new PlayerInput{Tracking=true,Shield=true});
            }
            if(b.EnemyAttackCount!=1||b.Enemy!=EnemyPhase.Windup)throw new Exception("Rock scenario not reached");
            while(b.TryCue(out _)){}
            var wrist=Bone(e.Root,"bip_hand_R");
            var stone=(Transform)typeof(MonsterRock).GetField("stone",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(w.Rock);
            string folder=Path.Combine(Folder,version,(block?"block":"hurt")+"-"+hz);Directory.CreateDirectory(folder);
            if(movie)Directory.CreateDirectory(folder+"/frames");
            var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();w.Camera.targetTexture=rt;w.Camera.aspect=16f/9;
            var trace=new StringBuilder("frame,enemy,age,attack,action,actionAge,health,blocks,hits,holding,flying,x,y,z,rotationStep,gripLocalX,gripLocalY,gripLocalZ\n");
            int blocks=b.Blocks,hits=b.HitsTaken,holdFrames=0;float dt=1f/hz,time=0,maxHoldDrift=0,releaseTurn=0,releaseStep=0,repeat=0;
            bool wasHolding=false,wasFlying=false;Vector3 previous=Vector3.zero,local=Vector3.zero;Quaternion rotation=Quaternion.identity;bool localSet=false;
            var saved=new System.Collections.Generic.HashSet<string>();
            try
            {
                for(int f=0;f<hz*4;f++)
                {
                    b.Tick(dt,new PlayerInput{Tracking=true,Shield=block});while(b.TryCue(out var cue))w.Cue(cue,b);
                    h.Update(b,w.Camera,dt,time);e.Update(b,w.Camera,dt,time);w.Tick(b,dt,time);time+=dt;
                    Vector3 p=w.Rock.Position,g=wrist.InverseTransformPoint(p);float turn=Quaternion.Angle(rotation,stone.rotation);
                    if(w.Rock.Holding&&MonsterRockMotion.Prepare(b)>.9f)
                    {if(localSet)maxHoldDrift=Mathf.Max(maxHoldDrift,Vector3.Distance(local,g));else {local=g;localSet=true;}holdFrames++;}
                    if(wasHolding&&w.Rock.Flying){releaseTurn=turn;releaseStep=Vector3.Distance(previous,p);}
                    if(w.Rock.Holding||w.Rock.Flying)
                    {
                        Quaternion currentRotation=stone.rotation;
                        w.Tick(b,0,time-dt);repeat=Mathf.Max(repeat,Vector3.Distance(p,w.Rock.Position));
                        if(Quaternion.Angle(stone.rotation,currentRotation)>.05f)throw new Exception("Repeated clock rotated stone");
                    }
                    trace.AppendLine(FormattableString.Invariant($"{f},{b.Enemy},{b.EnemyAge:F6},{b.EnemyAttackCount},{b.Action},{b.ActionAge:F6},{b.EnemyHealth},{b.Blocks},{b.HitsTaken},{w.Rock.Holding},{w.Rock.Flying},{p.x:F6},{p.y:F6},{p.z:F6},{turn:F4},{g.x:F6},{g.y:F6},{g.z:F6}"));
                    string key=w.Rock.Holding&&b.Enemy==EnemyPhase.Windup&&b.WarningDuration-b.EnemyAge<.25f?"hold":
                        w.Rock.Holding&&b.Enemy==EnemyPhase.Attack&&b.EnemyAge>.16f?"pre-release":
                        w.Rock.Flying&&!wasFlying?"release":w.Rock.Fragments?"impact":null;
                    if(movie&&key!=null&&saved.Add(key))
                    {
                        CharacterReview.Save(w.Camera,rt,folder+"/"+key+".png");
                        if(key=="hold"||key=="pre-release")
                        {
                            Vector3 cameraPosition=w.Camera.transform.position;Quaternion cameraRotation=w.Camera.transform.rotation;
                            w.Camera.transform.position=wrist.position+(cameraPosition-wrist.position).normalized*2.1f;
                            w.Camera.transform.LookAt(wrist.position+Vector3.up*.12f);
                            CharacterReview.Save(w.Camera,rt,folder+"/"+key+"-close.png");
                            w.Camera.transform.SetPositionAndRotation(cameraPosition,cameraRotation);
                        }
                    }
                    if(movie&&f%2==0)RangedContactReview.Save(w.Camera,rt,$"{folder}/frames/{f/2:D4}.jpg");
                    previous=p;rotation=stone.rotation;wasHolding=w.Rock.Holding;wasFlying=w.Rock.Flying;
                }
                string result=$"{hz}Hz block={block} launches={w.Rock.Launches} blocks={b.Blocks-blocks} hits={b.HitsTaken-hits} holdFrames={holdFrames} holdLocalDrift={maxHoldDrift:F6} releaseTurn={releaseTurn:F4} releaseStep={releaseStep:F4} repeat={repeat:F6} hidden={!stone.gameObject.activeSelf}";
                File.WriteAllText(folder+"/trace.csv",trace.ToString());File.WriteAllText(folder+"/metrics.txt",result);Debug.Log("[RockGrip] "+result);
                if(w.Rock.Launches!=1||b.Blocks-blocks!=(block?1:0)||b.HitsTaken-hits!=(block?0:1)||holdFrames<2||stone.gameObject.activeSelf||repeat>.0001f)throw new Exception(result);
                if(version=="after"&&(maxHoldDrift>.01f||releaseTurn>1000f/hz||releaseStep>20f/hz))throw new Exception("Grip or release discontinuity: "+result);
                File.WriteAllText(folder+"/validation.txt","passed\n"+result);
            }
            finally{w.Camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
        }
    }
}
