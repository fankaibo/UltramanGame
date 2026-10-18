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
    public static class FinisherPortraitReview
    {
        public static void Before()=>Run(false);
        public static void After()=>Run(true);
        sealed class Surface
        {
            public SkinnedMeshRenderer Skin;public int[] Head,Hands;
        }
        static bool HeadBone(Transform bone)=>bone!=null&&(bone.name=="head"||bone.name=="bip_head"||bone.parent!=null&&HeadBone(bone.parent));
        static float HeadWeight(BoneWeight w,Transform[] bones)=>(HeadBone(bones[w.boneIndex0])?w.weight0:0)+(HeadBone(bones[w.boneIndex1])?w.weight1:0)+(HeadBone(bones[w.boneIndex2])?w.weight2:0)+(HeadBone(bones[w.boneIndex3])?w.weight3:0);
        static bool HandBone(Transform bone)=>bone!=null&&(bone.name.StartsWith("HandBase_")||bone.name.StartsWith("bip_hand_")||bone.parent!=null&&HandBone(bone.parent));
        static float HandWeight(BoneWeight w,Transform[] bones)=>(HandBone(bones[w.boneIndex0])?w.weight0:0)+(HandBone(bones[w.boneIndex1])?w.weight1:0)+(HandBone(bones[w.boneIndex2])?w.weight2:0)+(HandBone(bones[w.boneIndex3])?w.weight3:0);
        static void Run(bool after)
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--portrait-output");
            string folder=Path.GetFullPath((at>=0?args[at+1]:"../artifacts/finisher-portrait")+(after?"/after":"/before"));Directory.CreateDirectory(folder);
            var report=new StringBuilder();
            foreach(string id in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})
            foreach(int rate in id=="Tiga"?new[]{15,30,60}:new[]{60})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(168);
                var world=new GameWorld();var b=new Battle(100);var h=new AnimatedActor(id,world.HeroHome,world.EnemyHome);var e=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);
                world.BindActors(h,e);world.SetHeroProfile(id);var camera=world.Camera;camera.aspect=16f/9;
                b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});b.GiveInstructionTime(100);
                for(int n=0;n<15;n++){b.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});for(int f=0;f<30;f++)b.Tick(.02f,new PlayerInput{Tracking=true});}while(b.TryCue(out _)){}
                var surfaces=h.Root.GetComponentsInChildren<SkinnedMeshRenderer>().Where(s=>s.enabled).Select(s=>new Surface{Skin=s,Head=s.sharedMesh.boneWeights.Select((w,i)=>new{w,i}).Where(v=>HeadWeight(v.w,s.bones)>.5f).Select(v=>v.i).ToArray(),Hands=s.sharedMesh.boneWeights.Select((w,i)=>new{w,i}).Where(v=>HandWeight(v.w,s.bones)>.5f).Select(v=>v.i).ToArray()}).ToArray();
                if(surfaces.Sum(s=>s.Head.Length)==0||surfaces.Sum(s=>s.Hands.Length)==0)throw new Exception("No actual head geometry "+id);
                var trace=new StringBuilder("frame,closeup,health,energy,actionAge,x,y,z,rx,ry,rz,rw,fov,headTop,handLow\n");
                float dt=1f/rate,health=b.EnemyHealth,maxTop=0,minHand=1,maxSpeed=0;int clipped=0,portrait=0;bool lastClose=false,saved=false;Vector3 previous=Vector3.zero;var mesh=new Mesh();
                var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();camera.targetTexture=rt;
                try
                {
                    for(int f=0;f<rate*4;f++)
                    {
                        float time=f*dt;b.Tick(world.BattleDelta(dt,b),new PlayerInput{Tracking=true,Beam=f==rate/2});while(b.TryCue(out var cue))world.Cue(cue,b);
                        h.Update(b,camera,dt,time);e.Update(b,camera,dt,time);if(b.EnemyHealth<health)world.Hit(true,b);health=b.EnemyHealth;world.Tick(b,dt,time);e.SetPresentationOpacity(world.EnemyOpacity);
                        float top=0,low=1;bool outside=false;
                        if(world.HeroShot)
                        {
                            portrait++;float safeTop=1-Mathf.Lerp(18,66,world.Closeup.Focus)/720f-.012f;
                            foreach(var s in surfaces)
                            {
                                s.Skin.BakeMesh(mesh,true);var vertices=mesh.vertices;
                                foreach(int i in s.Head)
                                {var p=camera.WorldToViewportPoint(s.Skin.transform.TransformPoint(vertices[i]));top=Mathf.Max(top,p.y);outside|=p.z<=0||p.x<.08f||p.x>.92f||p.y>safeTop;}
                                foreach(int i in s.Hands)
                                {var p=camera.WorldToViewportPoint(s.Skin.transform.TransformPoint(vertices[i]));low=Mathf.Min(low,p.y);outside|=p.z<=0||p.x<.08f||p.x>.92f||p.y<.16f||p.y>safeTop;}
                            }
                            foreach(var hand in new[]{h.StrikeOrigin(HeroAction.LeftPunch),h.StrikeOrigin(HeroAction.RightPunch)})
                            {var p=camera.WorldToViewportPoint(hand);low=Mathf.Min(low,p.y);outside|=p.z<=0||p.x<.08f||p.x>.92f||p.y<.16f||p.y>safeTop;}
                            maxTop=Mathf.Max(maxTop,top);minHand=Mathf.Min(minHand,low);if(outside)clipped++;
                            if(lastClose)maxSpeed=Mathf.Max(maxSpeed,Vector3.Distance(previous,camera.transform.position)/dt);
                            if(!saved&&world.Closeup.Age>=.65f){CharacterReview.Save(camera,rt,folder+$"/{id}-{rate}-peak.png");saved=true;}
                        }
                        var p0=camera.transform.position;var q=camera.transform.rotation;float lens=camera.fieldOfView;
                        trace.AppendLine(FormattableString.Invariant($"{f},{world.HeroShot},{health},{b.Energy},{b.ActionAge:F5},{p0.x:F6},{p0.y:F6},{p0.z:F6},{q.x:F6},{q.y:F6},{q.z:F6},{q.w:F6},{lens:F5},{top:F5},{low:F5}"));
                        world.Tick(b,0,time);
                        if(Vector3.Distance(camera.transform.position,p0)>.0001f||Quaternion.Angle(camera.transform.rotation,q)>.05f||Mathf.Abs(camera.fieldOfView-lens)>.0001f)throw new Exception("Repaint moved portrait camera");
                        previous=p0;lastClose=world.HeroShot;
                    }
                    string label=$"{id}-{rate}";File.WriteAllText(folder+"/"+label+".csv",trace.ToString());
                    string row=$"{label}: portraitFrames={portrait} clippedFrames={clipped} headTop={maxTop:F4} minHandY={minHand:F4} cameraSpeed={maxSpeed:F4} health={health}";
                    report.AppendLine(row);Debug.Log("[FinisherPortrait] "+row);File.WriteAllText(folder+"/validation.txt",report.ToString());
                    if(portrait==0||!saved||health!=76)throw new Exception("Incomplete portrait sequence "+row);
                    if(after&&id=="Tiga"&&(clipped>0||maxSpeed>2))throw new Exception("Portrait framing failed "+row);
                }
                finally{camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(mesh);}
            }
        }
    }
}
