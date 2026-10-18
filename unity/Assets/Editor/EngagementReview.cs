using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class EngagementReview
    {
        public static void Before()=>Run("before","Tiga",60,true);
        public static void TigaAfter()
        {foreach(int hz in new[]{15,30,60})Run("after","Tiga",hz,hz==60);}
        public static void After()
        {
            foreach(string name in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})foreach(int hz in new[]{15,30,60})Run("after",name,hz,name=="Tiga"&&hz==60);
        }
        static void Run(string version,string name,int hz,bool movie)
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--engagement-output");
            string output=at>=0&&at+1<args.Length?args[at+1]:"../artifacts/engagement-20261008";
            string extension=Array.IndexOf(args,"--review-jpeg")>=0?"jpg":"png";
            string folder=Path.GetFullPath(Path.Combine(output,version));Directory.CreateDirectory(folder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(1009);
            var world=new GameWorld();var state=new Battle();var hero=new AnimatedActor(name,world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);
            if(!hero.IsRigged||!enemy.IsRigged)throw new Exception("Actual model missing");world.BindActors(hero,enemy);world.SetHeroProfile(name);
            state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(state.Phase!=GamePhase.Battle)state.Tick(.02f,new PlayerInput{Tracking=true});state.GiveInstructionTime(20);while(state.TryCue(out _)){}
            var rt=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
            for(int n=0;n<40;n++){hero.Update(state,world.Camera,1/60f,n/60f);enemy.Update(state,world.Camera,1/60f,n/60f);world.Tick(state,1/60f,n/60f);}
            if(movie)Directory.CreateDirectory(folder+"/frames");
            var csv=new StringBuilder("frame,action,age,punches,health,energy,rootTravel,footLeftY,footRightY\n");
            float dt=1f/hz,health=state.EnemyHealth,path=0,handStep=0,ground=99,gap=0;Vector3 previous=hero.Root.position,oldHand=hero.HandPosition;var mesh=new Mesh();
            float next=.3f;bool left=true;int queued=0,entries=0;float handoff=0;int oldSequence=0;
            try
            {
                for(int f=0;f<hz*8;f++)
                {
                    float t=f*dt;var input=new PlayerInput{Tracking=true};
                    if(t>=next&&state.Action==HeroAction.None&&state.Punches<6){input.LeftPunch=left;input.RightPunch=!left;left=!left;next=t+.7f;}
                    if(state.IsPunch&&state.ActionAge>=.24f&&state.Punches<3&&queued!=state.Punches)
                    {queued=state.Punches;input.LeftPunch=state.Action==HeroAction.RightPunch;input.RightPunch=!input.LeftPunch;next=t+.7f;}
                    state.Tick(world.BattleDelta(dt,state),input);while(state.TryCue(out var cue))world.Cue(cue,state);
                    hero.Update(state,world.Camera,dt,t);enemy.Update(state,world.Camera,dt,t);
                    if(state.EnemyHealth<health)
                    {float g=Vector3.Distance(hero.StrikeContact(state),world.BeamTarget-(HeroKickMotion.Active(state)?Vector3.up*.64f:Vector3.zero));if(name!="Mebius")gap=Mathf.Max(gap,g);world.Hit(false,state);}
                    health=state.EnemyHealth;world.Tick(state,dt,t);
                    if(state.AttackSequence!=oldSequence&&state.Punches>=1){handoff+=Vector3.Dot(hero.Root.position-world.HeroHome,world.BattleAxis);entries++;}oldSequence=state.AttackSequence;
                    path+=Vector3.Distance(hero.Root.position,previous);previous=hero.Root.position;
                    handStep=Mathf.Max(handStep,Vector3.Distance(hero.HandPosition,oldHand));oldHand=hero.HandPosition;
                    if(f%5==0)foreach(var skin in hero.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {skin.BakeMesh(mesh,true);foreach(var v in mesh.vertices)ground=Mathf.Min(ground,skin.transform.TransformPoint(v).y);}
                    csv.AppendLine(FormattableString.Invariant($"{f},{state.Action},{state.ActionAge:F5},{state.Punches},{state.EnemyHealth},{state.Energy},{Vector3.Dot(hero.Root.position-world.HeroHome,world.BattleAxis):F5},{hero.FootPosition(true).y:F5},{hero.FootPosition(false).y:F5}"));
                    if(movie&&f%2==0)CharacterReview.Save(world.Camera,rt,folder+"/frames/"+(f/2).ToString("D4")+"."+extension);
                    if(state.Punches==2&&state.ActionAge>.32f&&state.ActionAge<.37f)CharacterReview.Save(world.Camera,rt,folder+"/"+name+"-"+hz+"-chain.png");
                }
                string result=$"{name}-{hz} hits={state.Punches} path={path:F4} handoff={handoff/Mathf.Max(1,entries):F4} gap={gap:F4} ground={ground:F4} handStep={handStep:F4} finalDistance={Vector3.Distance(hero.Root.position,world.HeroHome):F4}";
                File.WriteAllText(folder+"/"+name+"-"+hz+".csv",csv.ToString());File.WriteAllText(folder+"/"+name+"-"+hz+".txt",result);Debug.Log("[EngagementReview] "+result);
                if(state.Punches!=6||ground<-.055f||gap>.70f)throw new Exception(result);
                if(version=="after"&&handoff/Mathf.Max(1,entries)<.7f)throw new Exception("No close-range continuation: "+result);
                if(Vector3.Distance(hero.Root.position,world.HeroHome)>.15f)throw new Exception("Hero failed to return after input stopped");
            }
            finally{world.Camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(mesh);}
            if(movie)
            {
                var source=new StringBuilder();foreach(string p in Directory.GetFiles(Application.dataPath+"/Scripts","*.cs",SearchOption.AllDirectories))
                    using(var sha=System.Security.Cryptography.SHA256.Create())source.AppendLine(p.Substring(Application.dataPath.Length+1)+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant());
                File.WriteAllText(folder+"/sources.txt",source.ToString());
            }
        }
    }
}
