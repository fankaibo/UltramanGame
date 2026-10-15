using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class BeamSpacingReview
    {
        public static void Before()=>Run("before");
        public static void After()=>Run("after");
        static void Run(string version)
        {
            foreach(int hz in version=="before"?new[]{60}:new[]{15,30,60}) Sample(version,hz,false);
            Sample(version,60,true);
            if(version=="after")Sample(version,30,false,true);
        }
        static void Sample(string version,int hz,bool ranged,bool late=false)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(95);
            var world=new GameWorld();var state=new Battle(200);
            var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);
            var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
            while(state.Phase!=GamePhase.Battle)state.Tick(.02f,new PlayerInput{Tracking=true});
            state.GiveInstructionTime(100);while(state.TryCue(out _)){}
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/beam-spacing-20261011",version));Directory.CreateDirectory(folder+"/frames");
            var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16/9f;
            var csv=new StringBuilder("frame,action,age,health,energy,advance,leftY,rightY,beamLength\n");
            float dt=1f/hz,time=0,health=state.EnemyHealth,beamAt=-1,minLength=99,maxRootStep=0,zeroTime=0,minGround=99,readyAge=0,maxForward=0;int beams=0,images=0;Vector3 previous=hero.Root.position;var mesh=new Mesh();
            try
            {
                for(int frame=0;frame<hz*32;frame++)
                {
                    var input=new PlayerInput{Tracking=true};
                    if(state.Action==HeroAction.None)
                    {
                        if(state.Punches<15){input.LeftPunch=state.Punches%2==0;input.RightPunch=!input.LeftPunch;input.RangedAttack=ranged;}
                        else if(beamAt<0){readyAge+=dt;if(!late||readyAge>.68f){input.Beam=true;beamAt=time;}}
                    }
                    state.Tick(world.BattleDelta(dt,state),input);
                    while(state.TryCue(out var cue)){world.Cue(cue,state);if(cue==GameCue.Beam)beams++;}
                    hero.Update(state,world.Camera,dt,time);enemy.Update(state,world.Camera,dt,time);
                    if(state.EnemyHealth<health)world.Hit(state.LastHitAction==HeroAction.Beam,state);
                    health=state.EnemyHealth;world.Tick(state,dt,time);
                    float length=Vector3.Distance(world.BeamOrigin,world.BeamTarget);
                    if(beamAt>=0)
                    {
                        if(frame%5==0)foreach(var skin in hero.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {skin.BakeMesh(mesh,true);foreach(var v in mesh.vertices)minGround=Mathf.Min(minGround,skin.transform.TransformPoint(v).y);}
                        if(time>beamAt+dt*.5f&&state.Action==HeroAction.Beam)maxForward=Mathf.Max(maxForward,Vector3.Dot(hero.Root.position-previous,world.BattleAxis));
                        maxRootStep=Mathf.Max(maxRootStep,Vector3.Distance(previous,hero.Root.position));
                        if(world.BeamVisible&&state.ActionAge>.40f)minLength=Mathf.Min(minLength,length);
                        if(hz==60&&!ranged&&!late&&frame%2==0)CharacterReview.Save(world.Camera,rt,folder+"/frames/"+(images++).ToString("D4")+".jpg");
                        if(state.Action==HeroAction.Beam&&frame%5==0)
                        {var root=hero.Root.position;hero.Update(state,world.Camera,0,time);zeroTime=Mathf.Max(zeroTime,Vector3.Distance(root,hero.Root.position));}
                        csv.AppendLine(FormattableString.Invariant($"{frame},{state.Action},{state.ActionAge:F5},{state.EnemyHealth},{state.Energy},{Vector3.Dot(hero.Root.position-world.HeroHome,world.BattleAxis):F5},{hero.FootPosition(true).y:F5},{hero.FootPosition(false).y:F5},{length:F5}"));
                    }
                    previous=hero.Root.position;time+=dt;
                    if(beamAt>=0&&time-beamAt>4.8f)break;
                }
                string tag=(late?"delayed-":"")+(ranged?"ranged":"melee")+"-"+hz;
                string report=$"{tag} punches={state.Punches} beams={beams} health={health} minBeamLength={minLength:F4} maxRootStep={maxRootStep:F4} zeroTime={zeroTime:F6} ground={minGround:F4} maxForward={maxForward:F5} frames={images}";
                File.WriteAllText(folder+"/"+tag+".csv",csv.ToString());File.WriteAllText(folder+"/"+tag+".txt",report);Debug.Log("[BeamSpacingReview] "+report);
                if(state.Punches!=15||beams!=1||health!=176||zeroTime>.002f)throw new Exception(report);
                if(version=="after"&&(minLength<2||maxRootStep>.5f||minGround<-.055f||maxForward>.04f))throw new Exception("Beam staging not clear/continuous: "+report);
            }
            finally{world.Camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(mesh);}
        }
    }
}
