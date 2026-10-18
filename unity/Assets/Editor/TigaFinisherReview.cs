using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class TigaFinisherReview
    {
        static string Folder=>Path.GetFullPath(Application.dataPath+"/../../artifacts/tiga-finisher-20261012");
        public static void Run()
        {
            var report=new StringBuilder();Directory.CreateDirectory(Folder+"/inspection");
            foreach(int hz in new[]{15,30,60})Pose(hz,report);
            foreach(bool pause in new[]{false,true})FinishBoundary(pause,report);
            Pixels(report);
            File.WriteAllText(Folder+"/inspection/validation.txt",report.ToString());Debug.Log("[TigaFinisherReview] PASS\n"+report);
        }
        static object Field(object value,string name)=>value.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(value);
        static void Pose(int hz,StringBuilder report)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var world=new GameWorld();var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);
            var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            var rig=Field(hero,"rigged");Transform Bone(string name)=>(Transform)Field(rig,name);
            var wrist=Bone("leftHand");var elbow=Bone("forearm");var leftElbow=Bone("leftForearm");var shoulder=Bone("leftUpperArm");
            var b=new Battle(50);b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});
            for(int hit=0;hit<15;hit++){b.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});for(int f=0;f<22;f++)b.Tick(.02f,new PlayerInput{Tracking=true});}
            b.GiveInstructionTime(20);while(b.TryCue(out _)){}
            float dt=1f/hz,minGap=100,maxGap=0,minAngle=180,maxFreeze=0;int held=0;
            var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();
            for(int f=0;f<hz*4;f++)
            {
                b.Tick(world.BattleDelta(dt,b),new PlayerInput{Tracking=true,Beam=f==0});while(b.TryCue(out var cue))world.Cue(cue,b);
                hero.Update(b,world.Camera,dt,f*dt);enemy.Update(b,world.Camera,dt,f*dt);world.Tick(b,dt,f*dt);
                if(b.Action==HeroAction.Beam&&!world.Closeup.Active&&b.ActionAge>.30f)
                {
                    float gap=Vector3.Distance(wrist.position,elbow.position);minGap=Mathf.Min(minGap,gap);maxGap=Mathf.Max(maxGap,gap);
                    minAngle=Mathf.Min(minAngle,Vector3.Angle(shoulder.position-leftElbow.position,wrist.position-leftElbow.position));
                    Vector3 hand=wrist.position,origin=hero.BeamOrigin;hero.Update(b,world.Camera,0,f*dt);
                    maxFreeze=Mathf.Max(maxFreeze,Vector3.Distance(hand,wrist.position),Vector3.Distance(origin,hero.BeamOrigin));
                    if(held++==0&&hz==60)
                    {
                        world.Camera.targetTexture=rt;
                        foreach(int angle in new[]{-35,0,35})
                        {
                            var focus=(elbow.position+shoulder.position)*.5f;
                            world.Camera.transform.position=focus+Quaternion.Euler(0,angle,0)*new Vector3(0,.45f,-3.5f);
                            world.Camera.transform.LookAt(focus);CharacterReview.Save(world.Camera,rt,Folder+"/inspection/support-"+angle+".png");
                        }
                        world.Camera.targetTexture=null;
                    }
                }
            }
            if(held==0||minGap<.08f||maxGap>.23f||minAngle<65||maxFreeze>.0001f)
                throw new Exception($"Support invalid hz={hz} gap={minGap}/{maxGap} elbowAngle={minAngle} zeroTime={maxFreeze}");
            report.AppendLine($"pose hz={hz} held={held} wrist/elbowGap={minGap:F4}..{maxGap:F4} elbowAngleMin={minAngle:F2} zeroTime={maxFreeze:F6}");
            rt.Release();UnityEngine.Object.DestroyImmediate(rt);
        }
        static void FinishBoundary(bool pause,StringBuilder report)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var world=new GameWorld();var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);
            var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            var rig=Field(hero,"rigged");var wrist=(Transform)Field(rig,"leftHand");var elbow=(Transform)Field(rig,"forearm");
            var b=new Battle(16);b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});
            for(int hit=0;hit<15;hit++){b.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});for(int f=0;f<22;f++)b.Tick(.02f,new PlayerInput{Tracking=true});}
            b.GiveInstructionTime(20);while(b.TryCue(out _)){}bool paused=false;float gap=0,maxPauseMotion=0;Vector3 frozen=Vector3.zero;
            for(int f=0;f<300;f++)
            {
                const float dt=1/60f;
                b.Tick(world.BattleDelta(dt,b),new PlayerInput{Tracking=!paused,Beam=f==0});while(b.TryCue(out var cue))world.Cue(cue,b);
                if(pause&&!paused&&b.EnemyHealth==0){frozen=wrist.position;b.Pause();paused=true;}
                hero.Update(b,world.Camera,dt,f*dt);enemy.Update(b,world.Camera,dt,f*dt);world.Tick(b,dt,f*dt);
                gap=Vector3.Distance(wrist.position,elbow.position);
                if(paused){maxPauseMotion=Mathf.Max(maxPauseMotion,Vector3.Distance(frozen,wrist.position));if(world.BeamVisible)throw new Exception("Paused final beam remained visible");}
            }
            var expected=pause?GamePhase.Paused:GamePhase.Victory;
            if(b.Phase!=expected||b.EnemyHealth!=0||(!pause&&gap<.24f)||maxPauseMotion>.0001f)throw new Exception($"Final beam boundary {b.Phase}, gap={gap}, pauseMotion={maxPauseMotion}");
            if(pause)
            {
                for(int f=0;f<300;f++)
                {
                    const float dt=1/60f;b.Tick(world.BattleDelta(dt,b),new PlayerInput{Tracking=true});while(b.TryCue(out var cue))world.Cue(cue,b);
                    hero.Update(b,world.Camera,dt,5+f*dt);enemy.Update(b,world.Camera,dt,5+f*dt);world.Tick(b,dt,5+f*dt);
                }
                gap=Vector3.Distance(wrist.position,elbow.position);
                if(b.Phase!=GamePhase.Victory||gap<.24f)throw new Exception("Final beam failed to resume to victory");
            }
            report.AppendLine($"finishing beam => {expected} health=0 pauseMotion={maxPauseMotion:F6} finalVictoryGap={gap:F4}");
        }
        static void Pixels(StringBuilder report)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var shader=Resources.Load<Shader>("BeamStream");if(!shader||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new Exception("Invalid beam shader");
            var camera=new GameObject("Beam inspection camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1.4f;camera.aspect=1;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            var rt=new RenderTexture(256,256,24);rt.Create();camera.targetTexture=rt;
            var image=new Texture2D(256,256,TextureFormat.RGB24,false);
            var root=new GameObject("Beam review").transform;var beam=new BeamStream(root);
            var line=root.Find("Zeperion traveling stream").GetComponent<LineRenderer>();
            var tint=new Color(.25f,.72f,1);var accent=new Color(.90f,.98f,1);beam.SetProfile(tint,accent,false,true);
            Color32[] Read(string name){camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,256,256),0,0);image.Apply();File.WriteAllBytes(Folder+"/inspection/"+name+".png",image.EncodeToPNG());return image.GetPixels32();}
            int Changed(Color32[] a,Color32[] c,int threshold=0){int n=0;for(int j=0;j<a.Length;j++)if(Math.Abs(a[j].r-c[j].r)+Math.Abs(a[j].g-c[j].g)+Math.Abs(a[j].b-c[j].b)>threshold)n++;return n;}
            void Tick(float time)=>beam.Tick(camera,Vector3.left,Vector3.right,.6f,true,time);
            foreach(int angle in new[]{-45,-20,0,20,45})
            {
                camera.transform.position=Quaternion.Euler(0,angle,0)*new Vector3(0,0,-4);camera.transform.LookAt(Vector3.zero);
                beam.Clear();var empty=Read("empty-"+angle);Tick(1);var a=Read("flow-"+angle);Tick(1);int freeze=Changed(a,Read("repeat-"+angle));
                Tick(1.09f);int flow=Changed(a,Read("later-"+angle),6),pixels=Changed(empty,a,12);
                if(pixels<100||flow<100||freeze!=0)throw new Exception($"Beam invalid {angle} pixels={pixels} flow={flow} freeze={freeze}");
                beam.Clear();if(Changed(empty,Read("clear-"+angle))!=0)throw new Exception("Beam did not clear");
                report.AppendLine($"GPU angle={angle} pixels={pixels} flowPixels={flow} zeroTime={freeze} clear=pass");
            }
            camera.transform.position=new Vector3(0,0,-4);camera.transform.LookAt(Vector3.zero);
            var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.transform.position=new Vector3(0,0,-2);cube.transform.localScale=new Vector3(4,4,.1f);
            var opaque=new Material(Shader.Find("Unlit/Color")){color=new Color(.2f,.3f,.4f)};cube.GetComponent<Renderer>().sharedMaterial=opaque;
            beam.Clear();var blocked=Read("blocker");Tick(1);if(Changed(blocked,Read("occluded"))!=0)throw new Exception("Beam leaks through opaque geometry");
            UnityEngine.Object.DestroyImmediate(cube);UnityEngine.Object.DestroyImmediate(opaque);
            beam.SetProfile(tint,accent,true);Tick(1);
            if(line.sharedMaterial.GetFloat("_Axial")!=0||!root.Find("Twin finisher traveling stream").GetComponent<LineRenderer>().enabled)throw new Exception("Other hero profile changed");
            beam.SetProfile(tint,accent,false,true);Tick(1);
            if(line.sharedMaterial.GetFloat("_Axial")!=1||root.Find("Twin finisher traveling stream").GetComponent<LineRenderer>().enabled)throw new Exception("Tiga profile did not restore");
            report.AppendLine("depth=pass otherProfiles/twin=retained TigaRestore=pass");
            camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
