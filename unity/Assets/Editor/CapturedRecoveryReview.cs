using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class CapturedRecoveryReview
    {
        static string Folder=>Path.GetFullPath(Environment.GetEnvironmentVariable("ULTRAMAN_CAPTURED_REVIEW")??Path.Combine(Application.dataPath,"../../artifacts/mocap-recovery-20261010/runtime-ready"));
        public static void Preview()=>Run("Tiga",60,true);
        public static void After()
        {
            foreach(string hero in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})
                foreach(int hz in new[]{15,30,60})Run(hero,hz,hero=="Tiga"&&hz==60);
            KnockdownReview.CheckInterruptions();
        }
        static void Run(string id,int hz,bool movie)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(1073);
            var world=new GameWorld();var b=new Battle();
            var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            if(!hero.IsRigged)throw new Exception("Missing skeleton "+id);
            float dt=1f/hz,time=0;int landings=0;
            void Step(PlayerInput input)
            {
                time+=dt;b.Tick(world.BattleDelta(dt,b),input);
                while(b.TryCue(out var cue)){world.Cue(cue,b);if(cue==GameCue.HeroLanded)landings++;}
                hero.Update(b,world.Camera,dt,time);enemy.Update(b,world.Camera,dt,time);world.Tick(b,dt,time);
            }
            Step(new PlayerInput{Tracking=true,Transform=true});int waited=0;
            while(b.Action!=HeroAction.Hurt&&waited++<hz*40)Step(new PlayerInput{Tracking=true});
            if(b.Action!=HeroAction.Hurt)throw new Exception("No hit setup");
            string output=Folder+"/"+id+"-"+hz;Directory.CreateDirectory(output);if(movie)Directory.CreateDirectory(output+"/frames");
            var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
            var mesh=new Mesh();var skins=hero.Root.GetComponentsInChildren<SkinnedMeshRenderer>();
            var tracked=hero.Root.GetComponentsInChildren<Transform>().Where(t=>new[]{"hip","bip_pelvis","HandBase_L","HandBase_R","bip_hand_L","bip_hand_R","Foot_L","Foot_R","bip_foot_L","bip_foot_R"}.Contains(t.name)).ToArray();
            Vector3[] previous=tracked.Select(t=>t.position).ToArray();float lowest=100,maxStep=0,repeat=0;int captured=0;string lowestDetail="";
            var csv=new StringBuilder("frame,action,age,captured,minimumY,jointStep,rootX,rootY,rootZ,leftX,leftY,leftZ,rightX,rightY,rightZ\n");
            float[] moments={.2f,.45f,.85f,1.3f,1.8f,2.35f};bool[] saved=new bool[moments.Length];
            try
            {
                for(int frame=0;frame<hz*4;frame++)
                {
                    Step(new PlayerInput{Tracking=true});
                    var current=tracked.Select(t=>t.position).ToArray();float movement=0;
                    for(int i=0;i<current.Length;i++)movement=Mathf.Max(movement,Vector3.Distance(previous[i],current[i]));previous=current;
                    float ground=100;string detail="";foreach(var skin in skins)
                    {
                        skin.BakeMesh(mesh,true);var vertices=mesh.vertices;int at=-1;
                        for(int v=0;v<vertices.Length;v++){float y=skin.transform.TransformPoint(vertices[v]).y;if(y<ground){ground=y;at=v;}}
                        if(at>=0)detail=$"age={b.ActionAge:F5} bone={skin.bones[skin.sharedMesh.boneWeights[at].boneIndex0].name}";
                    }
                    if(b.Action==HeroAction.Hurt)
                    {
                        if(ground<lowest){lowest=ground;lowestDetail=detail;}maxStep=Mathf.Max(maxStep,movement);
                        if(hero.CapturedRecoveryActive)captured++;
                        if(b.ActionAge>.8f&&b.ActionAge<2&& !hero.CapturedRecoveryActive)throw new Exception("Capture was not loaded/applied "+id);
                        for(int m=0;m<moments.Length;m++)if(!saved[m]&&b.ActionAge>=moments[m])
                        {CharacterReview.Save(world.Camera,rt,$"{output}/pose-{m}.png");saved[m]=true;}
                    }
                    var beforeRoot=hero.Root.position;hero.Update(b,world.Camera,0,time);
                    repeat=Mathf.Max(repeat,Vector3.Distance(hero.Root.position,beforeRoot));
                    for(int i=0;i<current.Length;i++)repeat=Mathf.Max(repeat,Vector3.Distance(current[i],tracked[i].position));
                    var root=hero.Root.position;var left=hero.FootPosition(true);var right=hero.FootPosition(false);
                    csv.AppendLine(FormattableString.Invariant($"{frame},{b.Action},{b.ActionAge:F6},{hero.CapturedRecoveryActive},{ground:F6},{movement:F6},{root.x:F6},{root.y:F6},{root.z:F6},{left.x:F6},{left.y:F6},{left.z:F6},{right.x:F6},{right.y:F6},{right.z:F6}"));
                    if(movie&&frame%2==0)CharacterReview.Save(world.Camera,rt,$"{output}/frames/{frame/2:D4}.png");
                }
                bool recovered=b.Action==HeroAction.None;Step(new PlayerInput{Tracking=true,Shield=true});
                string result=$"{id}-{hz} capturedSamples={captured} minimumY={lowest:F6} maximumJointStep={maxStep:F6} repeatError={repeat:F6} hits={b.HitsTaken} landings={landings} recovered={recovered} shield={b.Shield} lowest=({lowestDetail})";
                File.WriteAllText(output+"/trace.csv",csv.ToString());File.WriteAllText(output+"/metrics.txt",result);Debug.Log("[CapturedRecoveryReview] "+result);
                if(captured<10||lowest<-.05f||repeat>.001f||landings!=1||b.HitsTaken!=1||b.Action!=HeroAction.None||!b.Shield||!saved.All(x=>x))throw new Exception("Captured recovery failed: "+result);
            }
            finally{world.Camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(mesh);}
        }
    }
}
