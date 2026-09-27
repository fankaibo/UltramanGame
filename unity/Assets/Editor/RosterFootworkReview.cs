using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    // Measure the planted ankle and the rendered sole, not just root travel.
    public static class RosterFootworkReview
    {
        public static void Before(){for(int i=1;i<HeroRoster.Count;i++)Run(HeroRoster.At(i).Id,"before");}
        public static void After(){for(int i=1;i<HeroRoster.Count;i++)Run(HeroRoster.At(i).Id,"after");SurfaceImpactReview.CheckPunchRecovery();}
        static Transform Bone(Transform root,string name)
        {foreach(var b in root.GetComponentsInChildren<Transform>())if(b.name==name)return b;throw new Exception("Missing "+name);}
        static void Run(string id,string version)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(270927);
            var world=new GameWorld();var state=new Battle();var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);
            var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});for(int i=0;i<120;i++)state.Tick(.02f,new PlayerInput{Tracking=true});state.GiveInstructionTime(20);while(state.TryCue(out _)){}
            hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,1,0);
            var left=Bone(hero.Root,"bip_foot_L");var right=Bone(hero.Root,"bip_foot_R");
            Vector3 lh=left.position,rh=right.position,lastL=lh,lastR=rh;Quaternion lr=left.rotation,rr=right.rotation;
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/roster-footwork",version,id));Directory.CreateDirectory(folder+"/frames");File.Delete(folder+"/validation.txt");
            var source=new StringBuilder("UTC: "+DateTime.UtcNow.ToString("O")+"\n");
            using(var sha=System.Security.Cryptography.SHA256.Create())foreach(string file in new[]{"Scripts/Runtime/RiggedActor.cs","Scripts/Runtime/AnimatedActor.cs","Scripts/Runtime/GameWorld.cs","Resources/Characters/"+id+"/"+id+".fbx","Editor/RosterFootworkReview.cs"})
                source.AppendLine(file+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Application.dataPath,file)))).Replace("-","").ToLowerInvariant());
            File.WriteAllText(folder+"/sources.txt",source.ToString());
            var target=new RenderTexture(1280,720,24){antiAliasing=4};target.Create();world.Camera.targetTexture=target;world.Camera.aspect=16f/9;
            var csv=new StringBuilder("frame,action,age,leftX,leftY,leftZ,rightX,rightY,rightZ,supportDrift,soleTilt\n");
            float supportDrift=0,soleTilt=0,minGround=100,maxStep=0,leadLift=0,landingDrift=0,health=50,contactGap=0;int contacts=0;
            var mesh=new Mesh();Vector3 planted=default;bool landed=false;HeroAction prior=HeroAction.None;
            try
            {
                for(int f=0;f<150;f++)
                {
                    const float dt=1/60f;float t=f*dt;state.Tick(dt,new PlayerInput{Tracking=true,LeftPunch=f==12,RightPunch=f==72});while(state.TryCue(out _)){}
                    hero.Update(state,world.Camera,dt,t);enemy.Update(state,world.Camera,dt,t);world.Tick(state,dt,t);
                    float drift=0,tilt=0;bool punch=state.Action==HeroAction.LeftPunch||state.Action==HeroAction.RightPunch;
                    if(punch)
                    {
                        bool leadingLeft=state.Action==HeroAction.LeftPunch;var support=leadingLeft?right:left;var lead=leadingLeft?left:right;
                        drift=Vector3.Distance(support.position,leadingLeft?rh:lh);tilt=Quaternion.Angle(support.rotation,leadingLeft?rr:lr);
                        supportDrift=Mathf.Max(supportDrift,drift);soleTilt=Mathf.Max(soleTilt,tilt);
                        leadLift=Mathf.Max(leadLift,lead.position.y-(leadingLeft?lh.y:rh.y));
                        if(state.Action!=prior)landed=false;
                        if(state.ActionAge>=Battle.PunchHitSeconds&&state.ActionAge<=.18f)
                        {if(!landed){planted=lead.position;landed=true;}landingDrift=Mathf.Max(landingDrift,Vector3.Distance(planted,lead.position));}
                    }
                    prior=state.Action;maxStep=Mathf.Max(maxStep,Vector3.Distance(left.position,lastL),Vector3.Distance(right.position,lastR));lastL=left.position;lastR=right.position;
                    if(f%2==0)foreach(var skin in hero.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {skin.BakeMesh(mesh,true);foreach(var v in mesh.vertices)minGround=Mathf.Min(minGround,skin.transform.TransformPoint(v).y);}
                    if(state.EnemyHealth<health)
                    {
                        contacts++;float gap=100;Vector3 point=hero.StrikeOrigin(state.Action)+world.BattleAxis*.12f;
                        foreach(var skin in enemy.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {skin.BakeMesh(mesh,true);foreach(var v in mesh.vertices)gap=Mathf.Min(gap,Vector3.Distance(point,skin.transform.TransformPoint(v)));}
                        contactGap=Mathf.Max(contactGap,gap);CharacterReview.Save(world.Camera,target,folder+"/contact-"+contacts+".png");
                    }
                    health=state.EnemyHealth;
                    csv.AppendLine(FormattableString.Invariant($"{f},{state.Action},{state.ActionAge:F5},{left.position.x:F5},{left.position.y:F5},{left.position.z:F5},{right.position.x:F5},{right.position.y:F5},{right.position.z:F5},{drift:F5},{tilt:F4}"));
                    if(id=="Mebius"&&f%2==0)CharacterReview.Save(world.Camera,target,$"{folder}/frames/{f/2:D4}.png");
                }
                string result=$"{version}/{id}: contacts={contacts} supportDrift={supportDrift:F4} soleTilt={soleTilt:F2} leadLift={leadLift:F4} landingDrift={landingDrift:F4} minGround={minGround:F4} maxFootStep={maxStep:F4} contactGap={contactGap:F4}";
                File.WriteAllText(folder+"/motion.csv",csv.ToString());Debug.Log("[RosterFootworkReview] "+result);
                if(contacts!=2||state.EnemyHealth!=48)throw new Exception("Invalid punch sequence");
                if(version=="after"&&(supportDrift>.035f||soleTilt>2||leadLift<.07f||landingDrift>.035f||minGround<-.035f||maxStep>.30f||contactGap>.30f))throw new Exception("Punch grounding failed: "+result);
                File.WriteAllText(folder+"/validation.txt",result);
            }
            finally{world.Camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(mesh);}
        }
    }
}
