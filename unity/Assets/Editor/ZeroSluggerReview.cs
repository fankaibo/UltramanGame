using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class ZeroSluggerReview
    {
        public static void Before()=>Run("before",60,true);
        public static void After(){foreach(int hz in new[]{15,30,60})Run("after",hz,hz==60);}
        static Transform Bone(Transform root,string name)
        {foreach(var b in root.GetComponentsInChildren<Transform>())if(b.name==name)return b;throw new Exception(name);}
        static void Run(string version,int hz,bool movie)
        {
            string folder=Path.GetFullPath("../artifacts/zero-slugger-20261009/"+version+"/"+hz);Directory.CreateDirectory(folder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(1057);
            var world=new GameWorld();var hero=new AnimatedActor("Zero",world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);
            if(!hero.IsRigged)throw new Exception("Actual Zero required");world.BindActors(hero,enemy);world.SetHeroProfile("Zero");
            var state=new Battle();state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(state.Phase!=GamePhase.Battle)state.Tick(.02f,new PlayerInput{Tracking=true});state.GiveInstructionTime(20);while(state.TryCue(out _)){}
            float time=0,dt=1f/hz;for(int n=0;n<40;n++){time+=1/60f;hero.Update(state,world.Camera,1/60f,time);enemy.Update(state,world.Camera,1/60f,time);world.Tick(state,1/60f,time);}
            var bones=new[]{Bone(hero.Root,"slugger_L"),Bone(hero.Root,"slugger_R")};var local=new[]{bones[0].localPosition,bones[1].localPosition};
            var head=Bone(hero.Root,"bip_head");float detached=0,restError=0;int originalFlightFrames=0,proceduralFlightFrames=0,hits=0;float health=50;
            var rt=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
            if(movie)Directory.CreateDirectory(folder+"/frames");
            var trace=new StringBuilder("frame,action,age,shotAge,health,energy,hits,sequence\n");var pose=new StringBuilder("frame,flight,age,leftDetach,rightDetach,procedural\n");
            var marks=new System.Collections.Generic.HashSet<string>();
            try
            {
                for(int f=0;f<hz*5;f++)
                {
                    time+=dt;float at=f*dt;
                    bool shoot=f==Mathf.RoundToInt(.4f*hz)||f==2*hz||f==Mathf.RoundToInt(3.4f*hz);
                    state.Tick(dt,new PlayerInput{Tracking=true,LeftPunch=shoot&&at>1&&at<3,RightPunch=shoot&&(at<1||at>3),RangedAttack=true,AttackSpeed=at<1?.65f:at<3?1:1.7f});
                    while(state.TryCue(out var cue))world.Cue(cue,state);hero.Update(state,world.Camera,dt,time);enemy.Update(state,world.Camera,dt,time);
                    if(state.EnemyHealth<health){world.Hit(false,state);hits++;}health=state.EnemyHealth;world.Tick(state,dt,time);
                    float l=Vector3.Distance(bones[0].position,bones[0].parent.TransformPoint(local[0]));float r=Vector3.Distance(bones[1].position,bones[1].parent.TransformPoint(local[1]));
                    int copies=0;foreach(var render in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))if(render.name.StartsWith("Zero Slugger ")&&render.enabled&&render.gameObject.activeInHierarchy)copies++;
                    if(world.Projectile.Visible){detached=Mathf.Max(detached,l,r);if(l>.20f&&r>.20f)originalFlightFrames++;if(copies>0)proceduralFlightFrames++;}
                    else restError=Mathf.Max(restError,l,r);
                    trace.AppendLine(FormattableString.Invariant($"{f},{state.Action},{state.ActionAge:F5},{state.Shot.Age:F5},{state.EnemyHealth:F2},{state.Energy:F2},{hits},{state.AttackSequence}"));
                    pose.AppendLine(FormattableString.Invariant($"{f},{world.Projectile.Visible},{state.Shot.Age:F5},{l:F5},{r:F5},{copies}"));
                    string key=!state.Shot.Active&&at<.3f?"mounted":state.Shot.Active&&state.Shot.Age>.20f&&state.Shot.Age<.28f?"outbound":state.Shot.Active&&state.Shot.Age>.35f?"return":!state.Shot.Active&&at>1.25f&&at<1.8f?"reattached":null;
                    if(movie&&key!=null&&marks.Add(key))
                    {
                        CharacterReview.Save(world.Camera,rt,folder+"/"+key+".png");
                        var position=world.Camera.transform.position;var rotation=world.Camera.transform.rotation;float fov=world.Camera.fieldOfView;
                        world.Camera.transform.position=head.position-world.BattleAxis*2.4f+Vector3.Cross(Vector3.up,world.BattleAxis)*1.25f+Vector3.up*.4f;
                        world.Camera.transform.LookAt(head.position+Vector3.up*.1f);world.Camera.fieldOfView=26;
                        CharacterReview.Save(world.Camera,rt,folder+"/head-"+key+".png");world.Camera.transform.SetPositionAndRotation(position,rotation);world.Camera.fieldOfView=fov;
                    }
                    if(movie&&f%2==0)CharacterReview.Save(world.Camera,rt,folder+"/frames/"+(f/2).ToString("D4")+".png");
                }
            }
            finally{world.Camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
            File.WriteAllText(folder+"/trace.csv",trace.ToString());File.WriteAllText(folder+"/pose.csv",pose.ToString());
            var sources=new StringBuilder();foreach(string dir in new[]{"Scripts","Resources"})foreach(string path in Directory.GetFiles(Application.dataPath+"/"+dir,"*",SearchOption.AllDirectories))
            {if(!path.EndsWith(".cs")&&!path.EndsWith(".shader")&&!path.EndsWith("Zero.fbx")&&!path.EndsWith("Zero_Sluggers.png"))continue;using(var sha=System.Security.Cryptography.SHA256.Create())sources.AppendLine(path.Substring(Application.dataPath.Length+1)+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant());}
            File.WriteAllText(folder+"/sources.txt",sources.ToString());
            string report=$"{version}-{hz} hits={hits} launches={world.Projectile.Launches} impacts={world.Projectile.Impacts} originalFlightFrames={originalFlightFrames} proceduralFlightFrames={proceduralFlightFrames} detached={detached:F4} restError={restError:F6}";
            Debug.Log("[ZeroSlugger] "+report);
            if(hits!=3||world.Projectile.Launches!=3||world.Projectile.Impacts!=3||restError>.001f)throw new Exception(report);
            if(version=="after"&&(originalFlightFrames<3||proceduralFlightFrames!=0||detached<1))throw new Exception("Original sluggers did not leave the head: "+report);
            File.WriteAllText(folder+"/validation.txt",report+" passed\n");
        }
    }
}
