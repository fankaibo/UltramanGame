using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class RosterPunchReview
    {
        public static void Before(){for(int i=0;i<HeroRoster.Count;i++)Run(HeroRoster.At(i).Id,"before");}
        public static void After(){for(int i=0;i<HeroRoster.Count;i++)Run(HeroRoster.At(i).Id,"after");}
        public static void Release(){After();SurfaceImpactReview.CheckPunchRecovery();CheckGuardTransitions();}
        public static void CheckGuardTransitions()
        {
            for(int h=1;h<HeroRoster.Count;h++)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                string id=HeroRoster.At(h).Id;var world=new GameWorld();var state=new Battle();
                var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});for(int i=0;i<120;i++)state.Tick(.02f,new PlayerInput{Tracking=true});state.GiveInstructionTime(20);
                hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);
                var previousLeft=hero.Root.InverseTransformPoint(hero.StrikeOrigin(HeroAction.LeftPunch));var previousRight=hero.Root.InverseTransformPoint(hero.HandPosition);
                float maxStep=0;int maxFrame=0;
                for(int f=0;f<120;f++)
                {
                    const float dt=1/60f;
                    state.Tick(dt,new PlayerInput{Tracking=true,Shield=(f>=20&&f<40)||(f>=70&&f<90),LeftPunch=f==60});
                    hero.Update(state,world.Camera,dt,f*dt);enemy.Update(state,world.Camera,dt,f*dt);
                    var left=hero.Root.InverseTransformPoint(hero.StrikeOrigin(HeroAction.LeftPunch));var right=hero.Root.InverseTransformPoint(hero.HandPosition);
                    if((f>=20&&f<45)||(f>=70&&f<95))
                    {float step=Mathf.Max(Vector3.Distance(left,previousLeft),Vector3.Distance(right,previousRight));if(step>maxStep){maxStep=step;maxFrame=f;}}
                    previousLeft=left;previousRight=right;
                }
                Debug.Log($"[RosterGuardTransitions] {id} maxHandStep={maxStep:F4} frame={maxFrame} punches={state.Punches} health={state.EnemyHealth}");
                if(maxStep>.45f||state.Punches!=1||state.EnemyHealth!=49)throw new Exception("Guard transition jumps: "+id);
            }
        }
        static Transform Bone(Transform root,string name,string alternative)
        {foreach(var b in root.GetComponentsInChildren<Transform>())if(b.name==name||b.name==alternative)return b;throw new Exception("Missing "+name);}
        static float Gap(AnimatedActor enemy,Vector3 point)
        {
            float gap=100;var mesh=new Mesh();
            foreach(var skin in enemy.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {skin.BakeMesh(mesh,true);foreach(var v in mesh.vertices)gap=Mathf.Min(gap,Vector3.Distance(point,skin.transform.TransformPoint(v)));}
            UnityEngine.Object.DestroyImmediate(mesh);return gap;
        }
        static void Run(string id,string version)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(270927);
            var world=new GameWorld();var state=new Battle();var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);
            var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});for(int i=0;i<120;i++)state.Tick(.02f,new PlayerInput{Tracking=true});state.GiveInstructionTime(20);while(state.TryCue(out _)){}
            hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,1,0);
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--roster-output");
            string output=at>=0&&at+1<args.Length?args[at+1]:Path.Combine(Application.dataPath,"../../artifacts/roster-contact");
            string folder=Path.GetFullPath(Path.Combine(output,version,id));Directory.CreateDirectory(folder+"/frames");File.Delete(folder+"/validation.txt");
            var source=new StringBuilder("UTC: "+DateTime.UtcNow.ToString("O")+"\n");
            using(var sha=System.Security.Cryptography.SHA256.Create())foreach(string file in new[]{"Scripts/Runtime/RiggedActor.cs","Scripts/Runtime/AnimatedActor.cs","Scripts/Runtime/GameWorld.cs","Resources/Characters/"+id+"/"+id+".fbx","Editor/RosterPunchReview.cs"})
                source.AppendLine(file+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Application.dataPath,file)))).Replace("-","").ToLowerInvariant());
            File.WriteAllText(folder+"/sources.txt",source.ToString());
            var target=new RenderTexture(1280,720,24){antiAliasing=4};target.Create();world.Camera.targetTexture=target;world.Camera.aspect=16f/9;
            var csv=new StringBuilder("frame,action,age,health,leftX,leftY,leftZ,rightX,rightY,rightZ\n");float health=state.EnemyHealth,maxStep=0;var gaps=new float[2];var oldLeft=hero.StrikeOrigin(HeroAction.LeftPunch);var oldRight=hero.HandPosition;int contacts=0;
            var leftShoulder=Bone(hero.Root,"bip_upperArm_L","armBase_L");var rightShoulder=Bone(hero.Root,"bip_upperArm_R","armBase_R");
            float maxOffhandReach=0,minHandSeparation=100;
            try
            {
                for(int f=0;f<150;f++)
                {
                    const float dt=1/60f;float t=f*dt;state.Tick(dt,new PlayerInput{Tracking=true,LeftPunch=f==12,RightPunch=f==72});while(state.TryCue(out _)){}
                    hero.Update(state,world.Camera,dt,t);enemy.Update(state,world.Camera,dt,t);world.Tick(state,dt,t);
                    var left=hero.StrikeOrigin(HeroAction.LeftPunch);var right=hero.HandPosition;
                    maxStep=Mathf.Max(maxStep,Vector3.Distance(left,oldLeft),Vector3.Distance(right,oldRight));oldLeft=left;oldRight=right;
                    if(state.EnemyHealth<health)
                    {
                        int side=state.Action==HeroAction.LeftPunch?0:1;gaps[side]=Gap(enemy,hero.StrikeOrigin(state.Action)+world.BattleAxis*.12f);
                        Vector3 active=side==0?left:right,off=side==0?right:left,shoulder=side==0?rightShoulder.position:leftShoulder.position;
                        maxOffhandReach=Mathf.Max(maxOffhandReach,Vector3.Dot(off-shoulder,world.BattleAxis));
                        minHandSeparation=Mathf.Min(minHandSeparation,Vector3.Dot(active-off,world.BattleAxis));
                        contacts++;CharacterReview.Save(world.Camera,target,folder+"/contact-"+side+".png");
                    }
                    health=state.EnemyHealth;
                    csv.AppendLine(FormattableString.Invariant($"{f},{state.Action},{state.ActionAge:F5},{health},{left.x:F5},{left.y:F5},{left.z:F5},{right.x:F5},{right.y:F5},{right.z:F5}"));
                    if(id=="Mebius"&&f%2==0)CharacterReview.Save(world.Camera,target,$"{folder}/frames/{f/2:D4}.png");
                }
                string result=$"{version}/{id}: contacts={contacts} leftGap={gaps[0]:F4} rightGap={gaps[1]:F4} maxHandStep={maxStep:F4} offhandReach={maxOffhandReach:F4} handSeparation={minHandSeparation:F4}";
                File.WriteAllText(folder+"/motion.csv",csv.ToString());Debug.Log("[RosterPunchReview] "+result);
                if(contacts!=2||state.EnemyHealth!=48)throw new Exception("Invalid punch sequence");
                if(version=="after"&&(gaps[0]>.30f||gaps[1]>.30f||maxStep>.70f))throw new Exception("Roster punch does not reach skin: "+result);
                if(version=="after"&&at>=0&&id!="Tiga"&&(maxOffhandReach>.34f||minHandSeparation<.40f||maxStep>.45f))throw new Exception("Guard separation or punch continuity failed: "+result);
                File.WriteAllText(folder+"/validation.txt",result);
            }
            finally{world.Camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);}
        }
    }
}
