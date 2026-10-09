using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class RangedReactionReview
    {
        public static void Before()=>Run(false);
        public static void After()=>Run(true);
        public static void Final(){Run(true);Lifecycle();}
        public static void Lifecycle()
        {
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/ranged-reaction-20261010/lifecycle"));Directory.CreateDirectory(root);var report=new StringBuilder();
            foreach(int hz in new[]{15,30,60})foreach(string mode in new[]{"pause","tracking","showcase","new-round","beam","melee"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var world=new GameWorld();var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                var b=new Battle(200);b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});
                if(mode=="beam")for(int n=0;n<15;n++){b.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});for(int f=0;f<30;f++)b.Tick(.02f,new PlayerInput{Tracking=true});b.GiveInstructionTime(20);}
                b.GiveInstructionTime(20);while(b.TryCue(out _)){}float dt=1f/hz,time=0;int preview=-1;
                void Tick(PlayerInput input)
                {b.Tick(dt,input);while(b.TryCue(out var cue))world.Cue(cue,b);hero.Update(b,world.Camera,dt,time,preview);enemy.Update(b,world.Camera,dt,time,preview);world.Tick(b,dt,time);time+=dt;}
                for(int f=0;f<hz*4;f++)
                {
                    Tick(new PlayerInput{Tracking=true,RangedAttack=true,LeftPunch=f==0||f==hz*2,RightPunch=f==hz});
                    if(enemy.RangedStepActive&&enemy.StaggerAge>=.13f)break;
                }
                if(!enemy.RangedStepActive)throw new Exception("Lifecycle did not enter step "+mode);
                if(mode=="pause")b.Pause();if(mode=="new-round")b=new Battle();if(mode=="showcase"){preview=0;world.Showcase=true;}
                bool beam=false,melee=false;float drift=0;
                for(int f=0;f<hz*4;f++)
                {
                    Tick(new PlayerInput{Tracking=mode!="tracking",Beam=mode=="beam"&&f<hz,LeftPunch=mode=="melee"&&f<hz});
                    beam|=b.Action==HeroAction.Beam;melee|=b.IsPunch&&!b.IsRangedPunch;
                    var nodes=enemy.Root.GetComponentsInChildren<Transform>();var pos=new Vector3[nodes.Length];var rot=new Quaternion[nodes.Length];for(int i=0;i<nodes.Length;i++){pos[i]=nodes[i].position;rot[i]=nodes[i].rotation;}
                    enemy.Update(b,world.Camera,0,time-dt,preview);
                    for(int i=0;i<nodes.Length;i++){drift=Mathf.Max(drift,Vector3.Distance(pos[i],nodes[i].position));if(Quaternion.Angle(rot[i],nodes[i].rotation)>.12f)throw new Exception("Repeated bone rotation drift "+mode);}
                    if((mode=="showcase"||mode=="new-round"||mode=="pause"||mode=="tracking")&&enemy.RangedStepActive)throw new Exception("Exclusive phase retained step "+mode);
                }
                string line=$"{hz}Hz {mode} active={enemy.RangedStepActive} drift={drift:F6} beam={beam} melee={melee}";report.AppendLine(line);
                if(enemy.RangedStepActive||drift>.0001f||mode=="beam"&&!beam||mode=="melee"&&!melee)throw new Exception("Lifecycle failed: "+line);
            }
            File.WriteAllText(root+"/validation.txt",report.ToString());Debug.Log("[RangedReactionLifecycle] PASS\n"+report);
        }
        static void Run(bool check)
        {
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/ranged-reaction-20261010"));
            string folder=root+(check?"/after":"/before");Directory.CreateDirectory(folder);
            var gameplay=new StringBuilder();var report=new StringBuilder();
            foreach(int hz in new[]{15,30,60})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(1071);
                var world=new GameWorld();var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                var rig=typeof(AnimatedActor).GetField("rigged",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(enemy);
                var sideField=typeof(RiggedActor).GetField("contactSide",BindingFlags.Instance|BindingFlags.NonPublic);
                var b=new Battle();b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});b.GiveInstructionTime(20);while(b.TryCue(out _)){}
                var target=new RenderTexture(1280,720,24){antiAliasing=4};target.Create();world.Camera.targetTexture=target;world.Camera.aspect=16f/9;
                string path=folder+"/"+hz;Directory.CreateDirectory(path);if(hz==60)Directory.CreateDirectory(path+"/frames");
                float dt=1f/hz,time=0,maxHandStep=0,maxFootStep=0,minFootY=100;int hits=0,wrongSides=0,guardContacts=0;Vector3 lastLeft=Vector3.zero,lastRight=Vector3.zero,lastFoot=Vector3.zero;
                var trace=new StringBuilder("frame,punches,action,shield,side,stepAge,stepLeft,leftY,rightY\n");
                try
                {
                    for(int f=-hz;f<hz*8;f++)
                    {
                        var input=new PlayerInput{Tracking=true};
                        for(int n=0;n<6;n++)
                        {
                            int start=Mathf.RoundToInt((.5f+n*.9f)*hz);
                            if(f==start){input.LeftPunch=n%2==0;input.RightPunch=n%2==1;input.RangedAttack=true;input.AttackSpeed=1;}
                            // Both hands: release a shot, then guard before it arrives.
                            if((n==0||n==3)&&f>=start+Mathf.CeilToInt(.16f*hz)&&f<start+Mathf.RoundToInt(.65f*hz)){input.Shield=true;input.GuardIntent=true;}
                        }
                        float health=b.EnemyHealth;b.Tick(dt,input);while(b.TryCue(out var cue))world.Cue(cue,b);
                        hero.Update(b,world.Camera,dt,time);enemy.Update(b,world.Camera,dt,time);if(b.EnemyHealth<health)world.Hit(false,b);world.Tick(b,dt,time);time+=dt;
                        int side=(int)(float)sideField.GetValue(rig);
                        if(b.Punches>hits)
                        {int expected=hits%2==0?-1:1;if(side!=expected)wrongSides++;if(b.Shield)guardContacts++;hits=b.Punches;CharacterReview.Save(world.Camera,target,path+"/hit-"+hits+".png");}
                        var left=enemy.StrikeOrigin(HeroAction.LeftPunch);var right=enemy.HandPosition;var foot=enemy.FootPosition(true);
                        if(f>0){maxHandStep=Mathf.Max(maxHandStep,Vector3.Distance(left,lastLeft),Vector3.Distance(right,lastRight));maxFootStep=Mathf.Max(maxFootStep,Vector3.Distance(foot,lastFoot));}
                        lastLeft=left;lastRight=right;lastFoot=foot;minFootY=Mathf.Min(minFootY,foot.y,enemy.FootPosition(false).y);
                        if(f>=0)
                        {
                            gameplay.AppendLine(FormattableString.Invariant($"{hz},{f},{b.Action},{b.ActionAge:F5},{b.Punches},{b.EnemyHealth},{b.Energy},{b.Shield},{b.Enemy},{b.EnemyAge:F5}"));
                            trace.AppendLine(FormattableString.Invariant($"{f},{b.Punches},{b.Action},{b.Shield},{side},{enemy.StaggerAge:F5},{enemy.StaggerLeft},{foot.y:F5},{enemy.FootPosition(false).y:F5}"));
                            if(hz==60&&f%2==0)CharacterReview.Save(world.Camera,target,path+"/frames/"+(f/2).ToString("D4")+".png");
                            if(enemy.StaggerAge>.17f&&enemy.StaggerAge<.17f+dt)CharacterReview.Save(world.Camera,target,path+"/step-"+hits+".png");
                        }
                        var before=enemy.Root.position;var handBefore=enemy.HandPosition;enemy.Update(b,world.Camera,0,time-dt);
                        if(Vector3.Distance(before,enemy.Root.position)>.0001f||Vector3.Distance(handBefore,enemy.HandPosition)>.0001f)throw new Exception("Repeated actor sample drift");
                    }
                    string line=$"{hz}Hz hits={hits} guardContacts={guardContacts} wrongSides={wrongSides} stepLandings={enemy.StaggerLandings} maxHandStep={maxHandStep:F4} maxFootStep={maxFootStep:F4} minFootY={minFootY:F4} residual={enemy.StaggerAge:F3}";
                    report.AppendLine(line);File.WriteAllText(path+"/motion.csv",trace.ToString());Debug.Log("[RangedReactionReview] "+line);
                    if(hits!=6||guardContacts!=2||b.EnemyHealth!=44||b.Energy!=6||check&&(wrongSides!=0||enemy.StaggerLandings!=2||minFootY<-.02f||enemy.StaggerAge<1))throw new Exception("Ranged reaction failed: "+line);
                }
                finally{world.Camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);}
            }
            File.WriteAllText(folder+"/validation.txt",report.ToString());File.WriteAllText(folder+"/gameplay.csv",gameplay.ToString());
            if(check&&File.ReadAllText(root+"/before/gameplay.csv")!=gameplay.ToString())throw new Exception("Ranged reaction changed gameplay");
            Debug.Log("[RangedReactionReview] PASS "+folder);
        }
    }
}
