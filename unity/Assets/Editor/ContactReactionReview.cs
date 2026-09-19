using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    // Contact, maximum recoil and return must be reviewed together. The ordinary
    // guided screenshot is taken at damage time, before the body has reacted.
    public static class ContactReactionReview
    {
        public static void Before() => Render("before");
        public static void After() {Render("after");ValidateSampling();}
        static Transform Bone(Transform root,string name)
        {foreach(var joint in root.GetComponentsInChildren<Transform>())if(joint.name==name)return joint;throw new Exception("Missing "+name);}
        static void ValidateSampling()
        {
            foreach(int fps in new[]{15,30,60})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var camera=new GameObject("Sampling camera").AddComponent<Camera>();var state=new Battle();
                var enemy=new AnimatedActor("Golza",Vector3.zero,Vector3.forward,true);
                state.Tick(.01f,new PlayerInput{Tracking=true,Transform=true});
                for(int i=0;i<130;i++)state.Tick(.02f,new PlayerInput{Tracking=true});
                float dt=1f/fps;enemy.Update(state,camera,0,0);
                var foot=Bone(enemy.Root,"bip_foot_L");var head=Bone(enemy.Root,"bip_head");var spine=Bone(enemy.Root,"bip_spine_2");float ground=foot.position.y;
                bool sampled=false;
                for(int i=0;i<fps;i++)
                {
                    state.Tick(dt,new PlayerInput{Tracking=true,LeftPunch=i==0});enemy.Update(state,camera,dt,i*dt);
                    if(Mathf.Abs(foot.position.y-ground)>.035f)throw new Exception("Foot drift at "+fps+" FPS");
                    if(!sampled&&state.ActionAge>.23f)
                    {
                        sampled=true;enemy.Update(state,camera,0,i*dt);var h=head.rotation;var s=spine.rotation;
                        for(int repeat=0;repeat<5;repeat++)enemy.Update(state,camera,0,i*dt);
                        if(Quaternion.Angle(h,head.rotation)>.01f||Quaternion.Angle(s,spine.rotation)>.01f)
                            throw new Exception("Impact feeds back into zero-time sampling at "+fps+" FPS");
                    }
                }
                if(!sampled)throw new Exception("Repeated sample was not exercised");
                var reference=new AnimatedActor("Golza",Vector3.zero,Vector3.forward,true);reference.Update(state,camera,0,(fps-1)*dt);
                if(Quaternion.Angle(head.rotation,Bone(reference.Root,"bip_head").rotation)>.01f||Quaternion.Angle(spine.rotation,Bone(reference.Root,"bip_spine_2").rotation)>.01f)
                    throw new Exception("Impact pose persists after recovery at "+fps+" FPS");
                Debug.Log($"[ContactSampling] fps={fps} plantedFeet=passed repeatedSample=passed recoveredPose=passed");
            }
        }
        static void Render(string version)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            UnityEngine.Random.InitState(180926);
            var world=new GameWorld();var state=new Battle(50);
            var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);
            var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            state.Tick(.01f,new PlayerInput{Tracking=true,Transform=true});
            for(int i=0;i<120;i++)state.Tick(.02f,new PlayerInput{Tracking=true});
            for(int hit=0;hit<13;hit++)
            {
                state.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});
                for(int i=0;i<22;i++)state.Tick(.02f,new PlayerInput{Tracking=true});
            }
            state.GiveInstructionTime(10);while(state.TryCue(out _)){}
            hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,1,0);
            var left=Bone(enemy.Root,"bip_foot_L");var right=Bone(enemy.Root,"bip_foot_R");var head=Bone(enemy.Root,"bip_head");
            Vector3 lf=left.position,rf=right.position,lastHead=head.position;float health=state.EnemyHealth,maxFootHeight=0,maxHeadStep=0;
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/contact-reaction",version));Directory.CreateDirectory(folder+"/frames");File.Delete(folder+"/validation.txt");
            var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32){antiAliasing=4};target.Create();world.Camera.targetTexture=target;world.Camera.aspect=16f/9;
            var csv=new StringBuilder("frame,time,health,leftFootY,rightFootY,headX,headY,headZ\n");
            int contacts=0;float contactAge=10;string contact="";var markers=new bool[4];float[] moments={.04f,.12f,.23f,.38f};
            try
            {
                for(int frame=0;frame<420;frame++)
                {
                    float dt=1/60f,t=frame*dt;contactAge+=dt;
                    state.Tick(world.BattleDelta(dt,state),new PlayerInput{Tracking=true,LeftPunch=frame==12,RightPunch=frame==90,Beam=frame==168});
                    while(state.TryCue(out var cue))world.Cue(cue);
                    hero.Update(state,world.Camera,dt,t);enemy.Update(state,world.Camera,dt,t);
                    if(state.EnemyHealth<health)
                    {
                        contacts++;contactAge=0;contact=state.Action.ToString();markers=new bool[4];world.Hit(health-state.EnemyHealth>1,state);
                    }
                    health=state.EnemyHealth;world.Tick(state,dt,t);enemy.SetPresentationOpacity(world.EnemyOpacity);
                    maxFootHeight=Mathf.Max(maxFootHeight,Mathf.Abs(left.position.y-lf.y),Mathf.Abs(right.position.y-rf.y));
                    maxHeadStep=Mathf.Max(maxHeadStep,Vector3.Distance(head.position,lastHead));lastHead=head.position;
                    csv.AppendLine(FormattableString.Invariant($"{frame},{t:F4},{health},{left.position.y:F5},{right.position.y:F5},{head.position.x:F5},{head.position.y:F5},{head.position.z:F5}"));
                    if(frame%2==0)CharacterReview.Save(world.Camera,target,$"{folder}/frames/frame-{frame/2:D4}.png");
                    for(int i=0;i<moments.Length;i++)if(contactAge>=moments[i]&&!markers[i]&&contacts>0)
                    {markers[i]=true;CharacterReview.Save(world.Camera,target,$"{folder}/{contact}-{i}.png");}
                }
                if(contacts!=3||state.Punches!=15||state.EnemyHealth!=26)throw new Exception("Expected two punches and one beam contact");
                if(version=="after"&&maxFootHeight>.035f)throw new Exception("Recoil rotates the planted feet: "+maxFootHeight);
                if(maxHeadStep>.45f)throw new Exception("Head motion jumps: "+maxHeadStep);
                string result=FormattableString.Invariant($"[ContactReactionReview] {version} passed contacts={contacts} duration=7 frames=210 maxFootVerticalDrift={maxFootHeight:F4} maxHeadStep={maxHeadStep:F4}");
                File.WriteAllText(folder+"/motion.csv",csv.ToString());File.WriteAllText(folder+"/validation.txt",result);Debug.Log(result);
            }
            finally {world.Camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);}
        }
    }
}
