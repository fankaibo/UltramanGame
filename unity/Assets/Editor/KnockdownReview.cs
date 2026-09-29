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
    public static class KnockdownReview
    {
        public static void Render()=>RenderAt("knockdown-review",false);
        public static void Before()=>RenderAt("grounded-rise/before",false);
        public static void After()=>RenderAt("grounded-rise/after",true);
        public static void Release(){After();Rates();}
        public static void Rates()
        {
            RenderAt("grounded-rise/rates/15",true,15);RenderAt("grounded-rise/rates/30",true,30);
            CheckInterruptions();RosterPunchReview.CheckGuardTransitions();
        }
        static void CheckInterruptions()
        {
            for(int id=0;id<HeroRoster.Count;id++)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                string name=HeroRoster.At(id).Id;var world=new GameWorld();var state=new Battle();
                var hero=new AnimatedActor(name,world.HeroHome,world.EnemyHome);
                state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
                for(int i=0;i<2000;i++){state.Tick(.02f,new PlayerInput{Tracking=true});if(state.Action==HeroAction.Hurt&&state.ActionAge>=.9f)break;}
                if(state.Action!=HeroAction.Hurt)throw new Exception("No interrupted rise");
                hero.Update(state,world.Camera,.02f,1);state.Pause();hero.Update(state,world.Camera,0,1);
                Vector3 root=hero.Root.position,left=hero.FootPosition(true),right=hero.FootPosition(false);
                for(int i=0;i<20;i++)hero.Update(state,world.Camera,0,1);
                if(Vector3.Distance(root,hero.Root.position)>.001f||Vector3.Distance(left,hero.FootPosition(true))>.001f||Vector3.Distance(right,hero.FootPosition(false))>.001f)
                    throw new Exception("Paused rise accumulates: "+name);
                for(int i=0;i<80;i++){state.Tick(.02f,new PlayerInput{Tracking=true});hero.Update(state,world.Camera,.02f,1+i*.02f);}
                state.Tick(.02f,new PlayerInput{Tracking=true,Shield=true});hero.Update(state,world.Camera,.02f,3);
                if(!state.Shield)throw new Exception("No defense after rise interruption");
                state.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});
                for(int i=0;i<30;i++){state.Tick(.02f,new PlayerInput{Tracking=true});hero.Update(state,world.Camera,.02f,3+i*.02f);}
                if(state.Punches!=1)throw new Exception("No counterpunch after rise interruption");
                // Interrupt another fall directly with a new round too.
                for(int i=0;i<2000;i++){state.Tick(.02f,new PlayerInput{Tracking=true});if(state.Action==HeroAction.Hurt&&state.ActionAge>=.9f)break;}
                if(state.Action!=HeroAction.Hurt)throw new Exception("No reset rise");
                hero.Update(state,world.Camera,.02f,5);state=new Battle();world.ResetPresentation();
                for(int i=0;i<15;i++)hero.Update(state,world.Camera,.02f,5+i*.02f);
                if(Vector3.Distance(hero.Root.position,world.HeroHome)>.001f||hero.FootPosition(true).y<0||hero.FootPosition(false).y<0)
                    throw new Exception("New round retains fall offset: "+name);
                Debug.Log("[KnockdownBoundary] "+name+" pause=passed resumeGuard=passed counterpunch=passed newRound=passed");
            }
        }
        static Transform Bone(Transform root,string a,string b)
        {foreach(var joint in root.GetComponentsInChildren<Transform>())if(joint.name==a||joint.name==b)return joint;throw new Exception("Missing "+a);}
        static void RenderAt(string destination,bool grounded,int rate=60)
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/"+destination));
            Directory.CreateDirectory(folder);Directory.CreateDirectory(folder+"/frames");File.Delete(folder+"/validation.txt");
            var sources=new StringBuilder("Rendered UTC: "+DateTime.UtcNow.ToString("O")+"\nUnity: "+Application.unityVersion+"\n");
            using(var sha=System.Security.Cryptography.SHA256.Create())
                foreach(string path in new[]{"Scripts/Core/KnockdownMotion.cs","Scripts/Core/Battle.cs","Scripts/Runtime/RiggedActor.cs","Scripts/Runtime/GameWorld.cs","Scripts/Runtime/CombatVfx.cs","Editor/KnockdownReview.cs"})
                    sources.AppendLine(path+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Application.dataPath,path)))).Replace("-","").ToLowerInvariant());
            File.WriteAllText(folder+"/render-source.txt",sources.ToString());
            var report=new StringBuilder();
            for(int id=0;id<HeroRoster.Count;id++)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(426);
                string name=HeroRoster.At(id).Id;
                var world=new GameWorld();var state=new Battle();
                var hero=new AnimatedActor(name,world.HeroHome,world.EnemyHome);
                var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
                for(int i=0;i<2000;i++)
                {
                    state.Tick(.02f,new PlayerInput{Tracking=true});
                    if(state.Enemy==EnemyPhase.Windup&&state.EnemyAge>state.WarningDuration-.3f)break;
                }
                while(state.TryCue(out _)){}
                var rt=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32){antiAliasing=4};rt.Create();
                world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
                hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,.1f,0);
                bool[] saved=new bool[4];int landings=0;float minGround=100,minX=1,minY=1,maxX=0,maxY=0;
                float leftRest=hero.FootPosition(true).y,rightRest=hero.FootPosition(false).y,unsupported=0,rearGap=0,repeatError=0;
                string repeatDetails="";
                var axis=(world.EnemyHome-world.HeroHome).normalized;
                var palm=Bone(hero.Root,"HandBase_L","bip_hand_L");
                var csv=new StringBuilder("frame,action,age,hits,landings,hipX,hipY,hipZ,leftX,leftY,leftZ,rightX,rightY,rightZ,palmX,palmY,palmZ\n");
                var baked=new Mesh();
                try
                {
                    for(int frame=0;frame<Mathf.CeilToInt(190*rate/60f);frame++)
                    {
                        float dt=1f/rate,time=frame*dt;
                        state.Tick(world.BattleDelta(dt,state),new PlayerInput{Tracking=true});
                        while(state.TryCue(out var cue)){world.Cue(cue,state);if(cue==GameCue.HeroLanded)landings++;}
                        hero.Update(state,world.Camera,dt,time);
                        var hip=hero.GroundContactPosition;var left=hero.FootPosition(true);var right=hero.FootPosition(false);var hand=palm.position;
                        if(grounded)
                        {
                            // The other actor's skin is an arm target. Repeat
                            // before moving it, so the inputs really are equal.
                            Vector3 origin=hero.Root.position;
                            hero.Update(state,world.Camera,0,time);
                            float error=Mathf.Max(Vector3.Distance(origin,hero.Root.position),Vector3.Distance(hip,hero.GroundContactPosition),
                                Vector3.Distance(left,hero.FootPosition(true)),Vector3.Distance(right,hero.FootPosition(false)),Vector3.Distance(hand,palm.position));
                            if(error>repeatError){repeatError=error;repeatDetails=$"frame={frame} action={state.Action} age={state.ActionAge:F5} root={Vector3.Distance(origin,hero.Root.position):F6} hip={Vector3.Distance(hip,hero.GroundContactPosition):F6} left={Vector3.Distance(left,hero.FootPosition(true)):F6} right={Vector3.Distance(right,hero.FootPosition(false)):F6} hand={Vector3.Distance(hand,palm.position):F6}";}
                        }
                        enemy.Update(state,world.Camera,dt,time);world.Tick(state,dt,time);
                        csv.AppendLine(FormattableString.Invariant($"{frame},{state.Action},{state.ActionAge:F5},{state.HitsTaken},{landings},{hip.x:F5},{hip.y:F5},{hip.z:F5},{left.x:F5},{left.y:F5},{left.z:F5},{right.x:F5},{right.y:F5},{right.z:F5},{hand.x:F5},{hand.y:F5},{hand.z:F5}"));
                        if(id==0&&rate==60&&frame%2==0)CharacterReview.Save(world.Camera,rt,folder+"/frames/"+(frame/2).ToString("D4")+".png");
                        if(state.Action!=HeroAction.Hurt)continue;
                        float age=state.ActionAge;
                        if(age>=.32f&&age<=1.60f)unsupported=Mathf.Max(unsupported,Mathf.Min(left.y-leftRest,right.y-rightRest));
                        if(age>=1&&age<=1.60f)rearGap=Mathf.Max(rearGap,Mathf.Min(Vector3.Dot(left,axis),Vector3.Dot(right,axis))-Vector3.Dot(hip,axis));
                        if(frame%3==0&&age>.18f)
                            foreach(var skin in hero.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                            {
                                skin.BakeMesh(baked,true);
                                foreach(var vertex in baked.vertices)
                                {
                                    Vector3 p=skin.transform.TransformPoint(vertex),v=world.Camera.WorldToViewportPoint(p);
                                    minGround=Mathf.Min(minGround,p.y);minX=Mathf.Min(minX,v.x);maxX=Mathf.Max(maxX,v.x);
                                    minY=Mathf.Min(minY,v.y);maxY=Mathf.Max(maxY,v.y);
                                }
                            }
                        float[] moments={.2f,.45f,.9f,1.4f};string[] names={"fall","landed","rising","recovered"};
                        for(int k=0;k<4;k++)if(!saved[k]&&age>=moments[k])
                        {CharacterReview.Save(world.Camera,rt,folder+"/"+name+"-"+names[k]+".png");saved[k]=true;}
                    }
                    string metrics=$"{name}: rate={rate} landings={landings} ground={minGround:F3} unsupported={unsupported:F5} rearGap={rearGap:F5} repeatError={repeatError:F6} restFeet=({leftRest:F4},{rightRest:F4}) viewport=({minX:F3},{minY:F3})-({maxX:F3},{maxY:F3})";
                    Debug.Log("[KnockdownReview] "+metrics);report.AppendLine(metrics);
                    File.WriteAllText(folder+"/"+name+"-sequence.csv",csv.ToString());
                    if(landings!=1||!Array.TrueForAll(saved,v=>v)||state.Action!=HeroAction.None||state.HitsTaken!=1)
                        throw new Exception("Fall/recovery sequence incomplete: "+metrics);
                    if(minGround<-.05f||minX<.02f||minY<.045f||maxX>.98f||maxY>.94f)
                        throw new Exception("Fall penetrates ground or leaves the HUD-safe viewport: "+metrics);
                    if(grounded&&unsupported>.045f)throw new Exception("Both feet float during supported rise: "+metrics);
                    if(grounded&&(rearGap>.20f||repeatError>.001f))throw new Exception("Unsupported or accumulating rise: "+metrics+" "+repeatDetails);
                }
                finally
                {
                    world.Camera.targetTexture=null;RenderTexture.active=null;rt.Release();
                    UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(baked);
                }
            }
            File.WriteAllText(folder+"/validation.txt",report.ToString());Debug.Log("[KnockdownReview] passed all heroes");
        }
    }
}
