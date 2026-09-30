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
    public static class PunchLinkReview
    {
        static readonly FieldInfo Pending=typeof(Battle).GetField("queuedPunch",BindingFlags.Instance|BindingFlags.NonPublic);
        static Transform Bone(Transform root,string first,string second)
        {foreach(var bone in root.GetComponentsInChildren<Transform>())if(bone.name==first||bone.name==second)return bone;throw new Exception("Missing "+first);}
        public static void Before()=>Run("before","Tiga",60,true);
        public static void After()
        {
            Run("after","Tiga",60,true);
            foreach(string hero in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})
            foreach(int rate in new[]{15,30,60})if(hero!="Tiga"||rate!=60)Run("after",hero,rate,false);
        }
        public static void CheckSupport()
        {
            SurfaceImpactReview.CheckPunchRecovery();
            RosterPunchReview.CheckGuardTransitions();
            ComboStrikeReview.Validate();
            ComboStrikeReview.Interruptions();
        }
        public static void Release(){After();CheckSupport();}
        static void Run(string version,string id,int rate,bool movie)
        {
            var args=Environment.GetCommandLineArgs();int outputAt=Array.IndexOf(args,"--punch-link-output");
            string output=outputAt>=0&&outputAt+1<args.Length?args[outputAt+1]:Path.Combine(Application.dataPath,"../../artifacts/punch-link");
            string folder=Path.GetFullPath(Path.Combine(output,version));Directory.CreateDirectory(folder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(929);
            var world=new GameWorld();var state=new Battle(50);
            var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});for(int i=0;i<120;i++)state.Tick(.02f,new PlayerInput{Tracking=true});
            state.GiveInstructionTime(12);while(state.TryCue(out _)){}
            hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,1,0);
            var linkProperty=typeof(AnimatedActor).GetProperty("LinkedPunchWeight");
            var hip=Bone(hero.Root,"hip","bip_pelvis");
            var rt=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
            if(movie)Directory.CreateDirectory(folder+"/frames");
            string name=id+"-"+rate;
            var states=new StringBuilder("frame,action,age,pending,punches,health,energy\n");
            var motion=new StringBuilder("frame,link,leftX,leftY,leftZ,rightX,rightY,rightZ\n");
            var body=new StringBuilder("frame,hipX,hipY,hipZ,hipQX,hipQY,hipQZ,hipQW,leftFootX,leftFootY,leftFootZ,rightFootX,rightFootY,rightFootZ\n");
            float dt=1f/rate,health=state.EnemyHealth,maxStep=0,maxLink=0,maxGap=0;
            Vector3 left=hero.StrikeOrigin(HeroAction.LeftPunch),right=hero.HandPosition;
            int queuedFor=0,linked=0;bool started=false;HeroAction lastAction=HeroAction.None;float lastAge=0;
            try
            {
                for(int frame=0;frame<rate*4;frame++)
                {
                    float time=frame*dt;var input=new PlayerInput{Tracking=true};
                    if(!started&&time>=.3f){input.LeftPunch=true;started=true;}
                    bool punching=state.Action==HeroAction.LeftPunch||state.Action==HeroAction.RightPunch;
                    if(punching&&state.ActionAge>=.24f&&state.Punches<6&&queuedFor!=state.Punches)
                    {queuedFor=state.Punches;input.LeftPunch=state.Action==HeroAction.RightPunch;input.RightPunch=!input.LeftPunch;}
                    state.Tick(world.BattleDelta(dt,state),input);while(state.TryCue(out var cue))world.Cue(cue,state);
                    hero.Update(state,world.Camera,dt,time);enemy.Update(state,world.Camera,dt,time);
                    bool hit=state.EnemyHealth<health;
                    if(hit)
                    {
                        maxGap=Mathf.Max(maxGap,Vector3.Distance(hero.StrikeOrigin(state.Action)+world.BattleAxis*.12f,enemy.BeamSurfaceContact));
                        world.Hit(false,state);
                    }
                    health=state.EnemyHealth;world.Tick(state,dt,time);
                    float weight=linkProperty==null?0:(float)linkProperty.GetValue(hero);
                    if(version=="after"&&hit&&weight>.001f)throw new Exception("Preparation altered a contact pose");
                    bool fresh=(state.Action==HeroAction.LeftPunch||state.Action==HeroAction.RightPunch)&&(state.Action!=lastAction||state.ActionAge<lastAge);
                    if(fresh&&weight>.05f)linked++;
                    maxLink=Mathf.Max(maxLink,weight);
                    Vector3 l=hero.StrikeOrigin(HeroAction.LeftPunch),r=hero.HandPosition;
                    maxStep=Mathf.Max(maxStep,Vector3.Distance(l,left),Vector3.Distance(r,right));left=l;right=r;
                    states.AppendLine(FormattableString.Invariant($"{frame},{state.Action},{state.ActionAge:F4},{Pending.GetValue(state)},{state.Punches},{state.EnemyHealth},{state.Energy}"));
                    motion.AppendLine(FormattableString.Invariant($"{frame},{weight:F5},{l.x:F5},{l.y:F5},{l.z:F5},{r.x:F5},{r.y:F5},{r.z:F5}"));
                    var hp=hip.position;var hq=hip.rotation;var lf=hero.FootPosition(true);var rf=hero.FootPosition(false);
                    body.AppendLine(FormattableString.Invariant($"{frame},{hp.x:F5},{hp.y:F5},{hp.z:F5},{hq.x:F5},{hq.y:F5},{hq.z:F5},{hq.w:F5},{lf.x:F5},{lf.y:F5},{lf.z:F5},{rf.x:F5},{rf.y:F5},{rf.z:F5}"));
                    if(movie&&frame%2==0)CharacterReview.Save(world.Camera,rt,$"{folder}/frames/{frame/2:D4}.png");
                    if(state.Punches==1&&state.ActionAge>.31f&&state.ActionAge<.37f)
                        CharacterReview.Save(world.Camera,rt,folder+"/"+name+"-prepare.png");
                    if(fresh&&state.Punches==1)CharacterReview.Save(world.Camera,rt,folder+"/"+name+"-handoff.png");
                    lastAction=state.Action;lastAge=state.ActionAge;
                }
                File.WriteAllText(folder+"/"+name+"-states.csv",states.ToString());File.WriteAllText(folder+"/"+name+"-motion.csv",motion.ToString());
                File.WriteAllText(folder+"/"+name+"-body.csv",body.ToString());
                if(state.Punches!=6||state.EnemyHealth!=44)throw new Exception("Six-punch input sequence changed");
                if(version=="after"&&(linked<4||maxLink<.5f))throw new Exception("Next accepted punch did not preload or carry");
                if(version=="after"&&maxStep>(rate==15?1.8f:rate==30?1.15f:.8f))
                    throw new Exception($"Linked punch detached: step={maxStep:F4} gap={maxGap:F4}");
                string report=$"{name} punches=6 health=44 linked={linked} peak={maxLink:F4} maxHandStep={maxStep:F4} contactGap={maxGap:F4}";
                File.WriteAllText(folder+"/"+name+"-validation.txt",report);Debug.Log("[PunchLinkReview] "+version+" "+report);
                state.Pause();hero.Update(state,world.Camera,dt,4);if(linkProperty!=null&&(float)linkProperty.GetValue(hero)!=0)throw new Exception("Pause retained punch anticipation");
                hero.Update(new Battle(),world.Camera,0,0);if(linkProperty!=null&&(float)linkProperty.GetValue(hero)!=0)throw new Exception("New round retained punch anticipation");
            }
            finally{world.Camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
            if(movie)
            {
                var hashes=new StringBuilder();using(var sha=System.Security.Cryptography.SHA256.Create())
                    foreach(string path in new[]{"Scripts/Runtime/RiggedActor.cs","Scripts/Runtime/AnimatedActor.cs","Scripts/Runtime/GameWorld.cs","Scripts/Core/Battle.cs","Scripts/Core/HeroPunchLink.cs"})
                    {string file=Path.Combine(Application.dataPath,path);if(File.Exists(file))hashes.AppendLine(path+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file))).Replace("-","").ToLowerInvariant());}
                File.WriteAllText(folder+"/sources.txt",hashes.ToString());
            }
        }
    }
}
