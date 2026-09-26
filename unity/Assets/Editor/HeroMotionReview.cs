using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    // Identical input and camera for comparing authored animation, including
    // rapid left/right transitions. The real player review runs separately.
    public static class HeroMotionReview
    {
        public static void Render()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--motion-output");
            if(at<0||at+1>=args.Length)throw new ArgumentException("--motion-output is required");
            string folder=Path.GetFullPath(args[at+1]);Directory.CreateDirectory(folder+"/frames");
            File.Delete(folder+"/validation.txt");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            UnityEngine.Random.InitState(20260921);
            var world=new GameWorld();var battle=new Battle();
            var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);
            var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            battle.Tick(.01f,new PlayerInput{Tracking=true,Transform=true});
            for(int i=0;i<140;i++)battle.Tick(1/60f,new PlayerInput{Tracking=true});
            while(battle.TryCue(out _)){}
            hero.Update(battle,world.Camera,0,0);enemy.Update(battle,world.Camera,0,0);world.Tick(battle,1,0);
            var hashes=new StringBuilder("UTC: "+DateTime.UtcNow.ToString("O")+"\n");
            using(var sha=System.Security.Cryptography.SHA256.Create())
                foreach(string file in new[]{"Resources/Characters/Tiga/Tiga.fbx","Resources/Characters/Tiga/motion.json","Scripts/Runtime/RiggedActor.cs","Scripts/Runtime/AnimatedActor.cs","Scripts/Runtime/GameWorld.cs","Editor/HeroMotionReview.cs"})
                    hashes.AppendLine(file+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Application.dataPath,file)))).Replace("-","").ToLowerInvariant());
            File.WriteAllText(folder+"/sources.txt",hashes.ToString());
            var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32){antiAliasing=4};target.Create();
            world.Camera.targetTexture=target;world.Camera.aspect=16f/9;
            var motion=new StringBuilder("frame,time,action,age,left_x,left_y,left_z,right_x,right_y,right_z,rear_foot_drift\n");
            float maxHandStep=0,maxFootDrift=0;Vector3 lastLeft=hero.StrikeOrigin(HeroAction.LeftPunch),lastRight=hero.HandPosition,rear=default;
            HeroAction lastAction=HeroAction.None;
            int punches=0;float contactGap=0;
            try
            {
                for(int frame=0;frame<240;frame++)
                {
                    const float dt=1/60f;float time=frame*dt;
                    battle.Tick(dt,new PlayerInput{Tracking=true,LeftPunch=frame==30||frame==84,RightPunch=frame==60||frame==108});
                    while(battle.TryCue(out _)){}
                    hero.Update(battle,world.Camera,dt,time);enemy.Update(battle,world.Camera,dt,time);
                    if(battle.Punches>punches)
                    {
                        punches=battle.Punches;
                        float gap=SurfaceGap(enemy.Root,hero.StrikeOrigin(battle.Action)+world.BattleAxis*.12f);
                        contactGap=Mathf.Max(contactGap,gap);
                        Debug.Log($"[PunchContact] side={battle.Action} gap={gap:F4}m");
                    }
                    // Omit hit VFX and impact zoom: the silhouette must work on its own.
                    world.Tick(battle,dt,time);
                    var left=hero.StrikeOrigin(HeroAction.LeftPunch);var right=hero.HandPosition;
                    maxHandStep=Mathf.Max(maxHandStep,Vector3.Distance(left,lastLeft),Vector3.Distance(right,lastRight));
                    lastLeft=left;lastRight=right;
                    bool punching=battle.Action==HeroAction.LeftPunch||battle.Action==HeroAction.RightPunch;
                    var foot=hero.FootPosition(battle.Action==HeroAction.RightPunch);
                    if(battle.Action!=lastAction)rear=foot;
                    float drift=punching?Vector3.ProjectOnPlane(foot-rear,Vector3.up).magnitude:0;
                    maxFootDrift=Mathf.Max(maxFootDrift,drift);lastAction=battle.Action;
                    motion.AppendLine(FormattableString.Invariant($"{frame},{time:F4},{battle.Action},{battle.ActionAge:F4},{left.x:F5},{left.y:F5},{left.z:F5},{right.x:F5},{right.y:F5},{right.z:F5},{drift:F5}"));
                    if(frame%2==0)CharacterReview.Save(world.Camera,target,$"{folder}/frames/frame-{frame/2:D4}.png");
                }
                File.WriteAllText(folder+"/motion.csv",motion.ToString());
                if(battle.Punches!=4||battle.HitsTaken!=0)throw new Exception("Motion input did not produce four uninterrupted punches");
                if(maxHandStep>1||maxFootDrift>.05f)throw new Exception($"Animation discontinuity: hand={maxHandStep:F3} supportFoot={maxFootDrift:F3}");
                if(contactGap>.3f)throw new Exception($"Fist misses monster surface: gap={contactGap:F4}m");
                string result=$"[HeroMotionReview] punches=4 frames=120 maxHandStep={maxHandStep:F4}m supportFootDrift={maxFootDrift:F4}m contactGap={contactGap:F4}m";
                File.WriteAllText(folder+"/validation.txt",result);Debug.Log(result);
            }
            finally {world.Camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);}
        }
        static float SurfaceGap(Transform root,Vector3 point)
        {
            float distance=float.PositiveInfinity;
            foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh=new Mesh();skin.BakeMesh(mesh,true);
                foreach(var vertex in mesh.vertices)distance=Mathf.Min(distance,Vector3.Distance(point,skin.transform.TransformPoint(vertex)));
                UnityEngine.Object.DestroyImmediate(mesh);
            }
            return distance;
        }
    }
}
