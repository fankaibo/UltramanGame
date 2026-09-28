using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class ComboStrikeReview
    {
        static string Output
        {get{var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--combo-output");return Path.GetFullPath(at>=0&&at+1<args.Length?args[at+1]:Path.Combine(Application.dataPath,"../../artifacts/combo-strike"));}}
        public static void Before()=>Run("before");
        public static void After()=>Run("after");
        static Transform Bone(Transform root,params string[] names)
        {foreach(var bone in root.GetComponentsInChildren<Transform>())foreach(string name in names)if(bone.name==name)return bone;throw new Exception("Missing bone "+names[0]);}
        static Battle Ready()
        {var state=new Battle(50);state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});for(int i=0;i<140;i++)state.Tick(.02f,new PlayerInput{Tracking=true});state.GiveInstructionTime(20);while(state.TryCue(out _)){}return state;}
        public static void Release(){After();Validate();Interruptions();StaggerReview.CheckRecovery();SurfaceImpactReview.CheckPunchRecovery();}
        public static void Interruptions()
        {
            string folder=Path.Combine(Output,"validation");Directory.CreateDirectory(folder);File.Delete(folder+"/interruptions.txt");
            var report=new StringBuilder();
            foreach(string id in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})foreach(bool shield in new[]{true,false})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var world=new GameWorld();var state=Ready();var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);
                var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                float time=0;const float dt=1/60f;
                void Step(PlayerInput input)
                {state.Tick(dt,input);hero.Update(state,world.Camera,dt,time);enemy.Update(state,world.Camera,dt,time);world.Tick(state,dt,time);time+=dt;}
                for(int n=0;n<4;n++)for(int f=0;f<40;f++)Step(new PlayerInput{Tracking=true,LeftPunch=f==0});
                for(int f=0;f<10;f++)Step(new PlayerInput{Tracking=true,RightPunch=f==0});
                if(state.Punches!=5||!ComboStrikeMotion.Active(state))throw new Exception("Interruption did not start from accent contact");
                var oldLeft=hero.Root.InverseTransformPoint(hero.StrikeOrigin(HeroAction.LeftPunch));var oldRight=hero.Root.InverseTransformPoint(hero.HandPosition);float maxStep=0;
                if(!shield)state.Pause();
                for(int f=0;f<120;f++)
                {
                    Step(new PlayerInput{Tracking=true,Shield=shield&&f<30});
                    if(ComboStrikeMotion.Active(state))throw new Exception("Accent survived interruption");
                    var l=hero.Root.InverseTransformPoint(hero.StrikeOrigin(HeroAction.LeftPunch));var r=hero.Root.InverseTransformPoint(hero.HandPosition);
                    if(shield&&f<30)maxStep=Mathf.Max(maxStep,Vector3.Distance(l,oldLeft),Vector3.Distance(r,oldRight));oldLeft=l;oldRight=r;
                }
                if(state.Phase!=GamePhase.Battle||state.Punches!=5||maxStep>.5f)throw new Exception($"Accent interruption jump: {id} shield={shield} step={maxStep}");
                state=new Battle();var fresh=new AnimatedActor(id,world.HeroHome,world.EnemyHome);fresh.SetOpponent(enemy);
                for(int f=0;f<60;f++){hero.Update(state,world.Camera,dt,time);fresh.Update(state,world.Camera,dt,time);time+=dt;}
                if(Vector3.Distance(hero.HandPosition,fresh.HandPosition)>.001f||Vector3.Distance(hero.StrikeOrigin(HeroAction.LeftPunch),fresh.StrikeOrigin(HeroAction.LeftPunch))>.001f)
                    throw new Exception("New round retained accent pose");
                report.AppendLine($"{id} shield={shield} guardHandStep={maxStep:F4} interrupted=passed resume=passed newRound=passed");
            }
            File.WriteAllText(folder+"/interruptions.txt",report.ToString());Debug.Log("[ComboInterruptions]\n"+report);
        }
        public static void Validate()
        {
            string folder=Path.Combine(Output,"validation");Directory.CreateDirectory(folder);
            File.Delete(folder+"/validation.txt");
            var report=new StringBuilder();
            foreach(string id in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})foreach(int rate in id=="Tiga"?new[]{15,30,60}:new[]{60})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var world=new GameWorld();var state=Ready();var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);
                var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,1,0);
                var leftShoulder=Bone(hero.Root,"armBase_L","bip_upperArm_L");var rightShoulder=Bone(hero.Root,"armBase_R","bip_upperArm_R");
                var target=new RenderTexture(1280,720,24);target.Create();world.Camera.targetTexture=target;world.Camera.aspect=16f/9;
                float maxStep=0,maxFootDrift=0,maxGap=0,maxOffhand=0,minSeparation=100;
                Vector3 lastLeft=hero.StrikeOrigin(HeroAction.LeftPunch),lastRight=hero.HandPosition;
                int contacts=0,accentContacts=0;float dt=1f/rate;var mesh=new Mesh();
                try
                {
                    for(int strike=1;strike<=10;strike++)
                    {
                        bool left=strike%2!=0;Vector3 support=hero.FootPosition(!left);float health=state.EnemyHealth;
                        for(int f=0;f<rate;f++)
                        {
                            float time=strike+f*dt;
                            state.Tick(dt,new PlayerInput{Tracking=true,LeftPunch=f==0&&left,RightPunch=f==0&&!left});while(state.TryCue(out _)){}
                            hero.Update(state,world.Camera,dt,time);enemy.Update(state,world.Camera,dt,time);world.Tick(state,dt,time);
                            bool punch=state.Action==HeroAction.LeftPunch||state.Action==HeroAction.RightPunch;
                            if(punch&&ComboStrikeMotion.Active(state)!=(strike%5==0))throw new Exception("Accent changed at contact or wrong punch ordinal");
                            var l=hero.StrikeOrigin(HeroAction.LeftPunch);var r=hero.HandPosition;
                            maxStep=Mathf.Max(maxStep,Vector3.Distance(l,lastLeft),Vector3.Distance(r,lastRight));lastLeft=l;lastRight=r;
                            if(punch)maxFootDrift=Mathf.Max(maxFootDrift,Vector3.ProjectOnPlane(hero.FootPosition(!left)-support,Vector3.up).magnitude);
                            if(state.EnemyHealth<health)
                            {
                                contacts++;if(strike%5==0)
                                {
                                    accentContacts++;Vector3 active=left?l:r,off=left?r:l,shoulder=left?rightShoulder.position:leftShoulder.position;
                                    maxOffhand=Mathf.Max(maxOffhand,Vector3.Dot(off-shoulder,world.BattleAxis));minSeparation=Mathf.Min(minSeparation,Vector3.Dot(active-off,world.BattleAxis));
                                    float gap=100;foreach(var skin in enemy.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                                    {
                                        skin.BakeMesh(mesh,true);var vertices=mesh.vertices;var weights=skin.sharedMesh.boneWeights;
                                        for(int v=0;v<vertices.Length;v++)
                                        {
                                            var w=weights[v];float Chest(int bone,float weight)=>skin.bones[bone].name.StartsWith("bip_spine",StringComparison.Ordinal)?weight:0;
                                            if(Chest(w.boneIndex0,w.weight0)+Chest(w.boneIndex1,w.weight1)+Chest(w.boneIndex2,w.weight2)+Chest(w.boneIndex3,w.weight3)<.75f)continue;
                                            gap=Mathf.Min(gap,Vector3.Distance(active+world.BattleAxis*.12f,skin.transform.TransformPoint(vertices[v])));
                                        }
                                    }
                                    maxGap=Mathf.Max(maxGap,gap);
                                    CharacterReview.Save(world.Camera,target,$"{folder}/{id}-{rate}-contact-{strike}.png");
                                }
                            }
                            health=state.EnemyHealth;
                            // Re-sampling the real accent pose must not accumulate
                            // torso/arm IK or feedback into the underlying clip.
                            if(punch&&strike%5==0&&state.ActionAge>=.10f)
                            {
                                // The opponent moved after the hero was sampled.
                                // Settle that new target once before comparing two
                                // identical samples; movement is not IK feedback.
                                hero.Update(state,world.Camera,0,time);
                                var stableLeft=hero.StrikeOrigin(HeroAction.LeftPunch);var stableRight=hero.HandPosition;
                                hero.Update(state,world.Camera,0,time);
                                float repeated=Mathf.Max(Vector3.Distance(stableLeft,hero.StrikeOrigin(HeroAction.LeftPunch)),Vector3.Distance(stableRight,hero.HandPosition));
                                if(repeated>.015f)throw new Exception($"Repeated combo sampling changed pose: {id}/{rate} {repeated}");
                            }
                        }
                    }
                    string result=$"{id}/{rate}Hz contacts={contacts} accents={accentContacts} gap={maxGap:F4} supportDrift={maxFootDrift:F4} handStep={maxStep:F4} offhandReach={maxOffhand:F4} separation={minSeparation:F4}";
                    Debug.Log("[ComboStrikeValidation] "+result);report.AppendLine(result);
                    if(contacts!=10||accentContacts!=2||state.EnemyHealth!=40||state.Energy!=10||maxGap>.35f||maxFootDrift>.09f||maxStep>1.7f||maxOffhand>.55f||minSeparation<.25f)
                        throw new Exception("Combo contact/support/hand validation failed: "+result);
                }
                finally{world.Camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(mesh);}
            }
            File.WriteAllText(folder+"/validation.txt",report.ToString());
        }
        static void Run(string version)
        {
            string folder=Path.Combine(Output,version);Directory.CreateDirectory(folder+"/frames");
            var sources=new StringBuilder();using(var sha=System.Security.Cryptography.SHA256.Create())
                foreach(string file in new[]{"Scripts/Runtime/RiggedActor.cs","Scripts/Runtime/AnimatedActor.cs","Scripts/Runtime/GameWorld.cs","Scripts/Runtime/GameAudio.cs","Scripts/Runtime/ArenaController.cs","Scripts/Runtime/CombatVfx.cs","Scripts/Runtime/StrikeContactBurst.cs","Resources/StrikeContact.shader","Scripts/Core/Battle.cs","Scripts/Core/ComboStrikeMotion.cs","Scripts/Core/MonsterStaggerMotion.cs","Resources/Characters/Golza/Golza.fbx","Resources/Characters/Tiga/Tiga.fbx","Editor/ComboStrikeReview.cs","Editor/MonsterBackstepReview.cs","Editor/StrikeContactReview.cs","Editor/TigaGuardReview.cs","Editor/SurfaceImpactReview.cs","Editor/RosterPunchReview.cs","Scripts/Core/ComboCameraMotion.cs","Editor/ComboCameraReview.cs"})
                {string path=Path.Combine(Application.dataPath,file);if(File.Exists(path))sources.AppendLine(file+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant());}
            File.WriteAllText(folder+"/sources.txt",sources.ToString());
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(928);
            var world=new GameWorld();var state=new Battle(50);var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);
            var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});for(int i=0;i<140;i++)state.Tick(.02f,new PlayerInput{Tracking=true});
            state.GiveInstructionTime(10);while(state.TryCue(out _)){}
            hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,1,0);
            var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
            var report=new StringBuilder("frame,time,action,age,punches,health,energy\n");int hits=0;float health=state.EnemyHealth;
            try
            {
                for(int frame=0;frame<420;frame++)
                {
                    const float dt=1/60f;float time=frame*dt;int count=(frame-30)/34;
                    bool punch=frame>=30&&count<10&&(frame-30)%34==0;
                    state.Tick(world.BattleDelta(dt,state),new PlayerInput{Tracking=true,LeftPunch=punch&&count%2==0,RightPunch=punch&&count%2==1});
                    while(state.TryCue(out var cue))world.Cue(cue,state);
                    hero.Update(state,world.Camera,dt,time);enemy.Update(state,world.Camera,dt,time);
                    if(state.EnemyHealth<health){hits++;world.Hit(false,state);}health=state.EnemyHealth;world.Tick(state,dt,time);
                    if(hits==5||hits==10)
                    {
                        if(state.ActionAge>=.12f&&state.ActionAge<.12f+dt)CharacterReview.Save(world.Camera,rt,folder+"/contact-"+hits+".png");
                        if(state.ActionAge>=.24f&&state.ActionAge<.24f+dt)CharacterReview.Save(world.Camera,rt,folder+"/follow-"+hits+".png");
                    }
                    report.AppendLine(FormattableString.Invariant($"{frame},{time:F4},{state.Action},{state.ActionAge:F4},{state.Punches},{state.EnemyHealth},{state.Energy}"));
                    if(frame%2==0)CharacterReview.Save(world.Camera,rt,folder+"/frames/"+(frame/2).ToString("D4")+".png");
                }
                if(hits!=10||state.Punches!=10||state.EnemyHealth!=40||state.Energy!=10||state.HitsTaken!=0)throw new Exception("Combo review changed punch rules or inputs");
                File.WriteAllText(folder+"/sequence.csv",report.ToString());File.WriteAllText(folder+"/validation.txt","10 alternating punches, health=40, energy=10, hurt=0, frames=210 passed");
                Debug.Log("[ComboStrikeReview] "+version+" frames=210 hits=10 health=40 energy=10");
            }
            finally{world.Camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
        }
    }
}
