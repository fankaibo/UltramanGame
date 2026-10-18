using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class MebiumBladeReview
    {
        public static void Before()=>Run("before",60,true);
        public static void After(){foreach(int hz in new[]{15,30,60})Run("after",hz,hz==60);}
        public static void Release(){After();MebiumBladeLifecycleReview.Validate();}
        static Transform Bone(Transform root,string name)
        {foreach(var b in root.GetComponentsInChildren<Transform>())if(b.name==name)return b;throw new Exception(name);}
        static void Run(string version,int hz,bool movie)
        {
            string folder=Path.GetFullPath("../artifacts/mebium-blade-20261009/"+version+"/"+hz);Directory.CreateDirectory(folder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(1058);
            var world=new GameWorld();var hero=new AnimatedActor("Mebius",world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);
            world.BindActors(hero,enemy);world.SetHeroProfile("Mebius");
            var state=new Battle();state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(state.Phase!=GamePhase.Battle)state.Tick(.02f,new PlayerInput{Tracking=true});state.GiveInstructionTime(20);while(state.TryCue(out _)){}
            float time=0,dt=1f/hz;for(int n=0;n<40;n++){time+=1/60f;hero.Update(state,world.Camera,1/60f,time);enemy.Update(state,world.Camera,1/60f,time);world.Tick(state,1/60f,time);}
            var wrist=Bone(hero.Root,"bip_hand_L");var elbow=Bone(hero.Root,"bip_lowerArm_L");var shoulder=Bone(hero.Root,"bip_upperArm_L");
            float maxAngle=0,gap=0,sweptGap=0,step=0,minY=99;Vector3 priorTip=world.Blade.Tip;int hits=0,visible=0;float health=50;Vector3 last=wrist.position;var baked=new Mesh();
            var rt=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
            if(movie)Directory.CreateDirectory(folder+"/frames");
            var trace=new StringBuilder("frame,action,age,health,energy,hits,sequence\n");var pose=new StringBuilder("frame,age,visible,wristX,wristY,wristZ,tipX,tipY,tipZ,forearmAngle,handStep,shoulderGap,armLength,forearmLength\n");
            var marks=new System.Collections.Generic.HashSet<string>();
            try
            {
                for(int f=0;f<hz*5;f++)
                {
                    time+=dt;float at=f*dt;
                    bool left=f==Mathf.RoundToInt(.4f*hz)||f==Mathf.RoundToInt(2.2f*hz),right=f==Mathf.RoundToInt(3.5f*hz);
                    state.Tick(dt,new PlayerInput{Tracking=true,LeftPunch=left,RightPunch=right,AttackSpeed=at<1?.65f:at<3?1.7f:1});
                    while(state.TryCue(out var cue))world.Cue(cue,state);hero.Update(state,world.Camera,dt,time);enemy.Update(state,world.Camera,dt,time);
                    bool hit=state.EnemyHealth<health;if(hit){world.Hit(false,state);hits++;}health=state.EnemyHealth;world.Tick(state,dt,time);
                    float angle=world.Blade.Visible?Vector3.Angle(world.Blade.Tip-(version=="after"?world.Blade.Origin:wrist.position),wrist.position-elbow.position):0;
                    if(world.Blade.Visible){visible++;maxAngle=Mathf.Max(maxAngle,angle);}
                    if(hit&&state.Action==HeroAction.LeftPunch)
                    {
                        gap=Mathf.Max(gap,Vector3.Distance(world.Blade.Tip,world.BeamTarget));
                        var segment=world.Blade.Tip-priorTip;float along=segment.sqrMagnitude>0?Mathf.Clamp01(Vector3.Dot(world.BeamTarget-priorTip,segment)/segment.sqrMagnitude):0;
                        sweptGap=Mathf.Max(sweptGap,Vector3.Distance(priorTip+segment*along,world.BeamTarget));
                    }
                    priorTip=world.Blade.Tip;
                    float delta=Vector3.Distance(wrist.position,last);step=Mathf.Max(step,delta);last=wrist.position;
                    if(f%Mathf.Max(1,hz/15)==0)foreach(var skin in hero.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {skin.BakeMesh(baked,true);foreach(var v in baked.vertices)minY=Mathf.Min(minY,skin.transform.TransformPoint(v).y);}
                    trace.AppendLine(FormattableString.Invariant($"{f},{state.Action},{state.ActionAge:F5},{state.EnemyHealth:F2},{state.Energy:F2},{hits},{state.AttackSequence}"));
                    var tip=world.Blade.Tip;var w=wrist.position;pose.AppendLine(FormattableString.Invariant($"{f},{state.ActionAge:F5},{world.Blade.Visible},{w.x:F5},{w.y:F5},{w.z:F5},{tip.x:F5},{tip.y:F5},{tip.z:F5},{angle:F4},{delta:F5},{Vector3.Distance(shoulder.position,world.BeamTarget):F5},{Vector3.Distance(shoulder.position,elbow.position):F5},{Vector3.Distance(elbow.position,wrist.position):F5}"));
                    string key=state.Action==HeroAction.LeftPunch?(state.ActionAge<.07f?"load":state.ActionAge<.16f?"contact":state.ActionAge<.26f?"follow":"recover"):null;
                    if(movie&&key!=null&&marks.Add(key))
                    {
                        CharacterReview.Save(world.Camera,rt,folder+"/"+key+".png");
                        var position=world.Camera.transform.position;var rotation=world.Camera.transform.rotation;float fov=world.Camera.fieldOfView;
                        world.Camera.transform.position=hero.Root.position+world.BattleAxis*3.4f-Vector3.Cross(Vector3.up,world.BattleAxis)*1.5f+Vector3.up*2.8f;
                        world.Camera.transform.LookAt(hero.Root.position+Vector3.up*2.5f);world.Camera.fieldOfView=42;
                        CharacterReview.Save(world.Camera,rt,folder+"/detail-"+key+".png");world.Camera.transform.SetPositionAndRotation(position,rotation);world.Camera.fieldOfView=fov;
                    }
                    if(movie&&f%2==0)CharacterReview.Save(world.Camera,rt,folder+"/frames/"+(f/2).ToString("D4")+".png");
                }
            }
            finally{world.Camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(baked);}
            File.WriteAllText(folder+"/trace.csv",trace.ToString());File.WriteAllText(folder+"/pose.csv",pose.ToString());
            var sources=new StringBuilder();foreach(string dir in new[]{"Scripts","Resources"})foreach(string path in Directory.GetFiles(Application.dataPath+"/"+dir,"*",SearchOption.AllDirectories))
            {if(!path.EndsWith(".cs")&&!path.EndsWith(".shader")&&!path.EndsWith("Mebius.fbx"))continue;using(var sha=System.Security.Cryptography.SHA256.Create())sources.AppendLine(path.Substring(Application.dataPath.Length+1)+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant());}
            File.WriteAllText(folder+"/sources.txt",sources.ToString());
            string report=$"{version}-{hz} hits={hits} visible={visible} forearmAngle={maxAngle:F3} contactGap={gap:F4} sweptGap={sweptGap:F4} maxHandStep={step:F4} minY={minY:F4}";Debug.Log("[MebiumBlade] "+report);
            if(hits!=3||minY<-.07f||visible<3)throw new Exception(report);
            if(version=="after"&&(maxAngle>.10f||sweptGap>.40f))throw new Exception("Wrist alignment or contact failed: "+report);
            File.WriteAllText(folder+"/validation.txt",report+" passed\n");
            if(version=="before")
            {
                var info=new StringBuilder();foreach(var b in hero.Root.GetComponentsInChildren<Transform>())info.AppendLine("bone "+b.name);
                foreach(var r in hero.Root.GetComponentsInChildren<SkinnedMeshRenderer>())foreach(var m in r.sharedMaterials)info.AppendLine("material "+m.name);
                File.WriteAllText(folder+"/model.txt",info.ToString());
            }
        }
    }
}
