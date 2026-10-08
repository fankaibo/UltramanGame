using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class HeroGuardHandoffReview
    {
        static void Require(bool value,string message){if(!value)throw new Exception(message);}
        static Battle Start()
        {var b=new Battle(200);b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});b.GiveInstructionTime(20);return b;}
        static Vector3[] Points(Transform[] bones){var p=new Vector3[bones.Length];for(int i=0;i<p.Length;i++)p[i]=bones[i].position;return p;}
        public static void Validate()=>Run(false);
        public static void Baseline()=>Run(true);
        static void Run(bool baseline)
        {
            var report=new StringBuilder();
            foreach(string name in baseline?new[]{"Tiga"}:new[]{"Tiga","Mebius","Zero","Geed","Grigio"})foreach(int hz in baseline?new[]{15}:new[]{15,30,60})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var world=new GameWorld();var hero=new AnimatedActor(name,world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);
                world.BindActors(hero,enemy);world.SetHeroProfile(name);var b=Start();float dt=1f/hz,time=0,ground=99,pauseError=0,lengthError=0,counterGap=0,counterSweep=0;
                var bones=hero.Root.GetComponentsInChildren<Transform>();var lengths=new float[bones.Length];
                for(int i=0;i<bones.Length;i++)lengths[i]=bones[i].localPosition.magnitude;
                var mesh=new Mesh();bool bake=false;
                void Draw(float delta)
                {
                    time+=delta;hero.Update(b,world.Camera,delta,time);enemy.Update(b,world.Camera,delta,time);world.Tick(b,delta,time);
                    for(int i=0;i<bones.Length;i++)if(bones[i].name.Contains("lowerArm")||bones[i].name.Contains("ForearmBase")||bones[i].name.Contains("hand_")||bones[i].name.Contains("HandBase"))
                        lengthError=Mathf.Max(lengthError,Mathf.Abs(lengths[i]-bones[i].localPosition.magnitude));
                    if(bake)foreach(var skin in hero.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {skin.BakeMesh(mesh,true);foreach(var v in mesh.vertices)ground=Mathf.Min(ground,skin.transform.TransformPoint(v).y);}
                }
                void Step(PlayerInput input){b.Tick(dt,input);while(b.TryCue(out _)){}Draw(dt);}
                void Prepare(bool left,float speed)
                {
                    b=Start();for(int i=0;i<hz/2;i++)Step(new PlayerInput{Tracking=true});
                    Step(new PlayerInput{Tracking=true,LeftPunch=left,RightPunch=!left,AttackSpeed=speed});
                    while(b.IsPunch&&b.ActionAge<.15f)Step(new PlayerInput{Tracking=true});
                    Step(new PlayerInput{Tracking=true,Shield=true,GuardIntent=true});Require(b.Shield,"Delayed shield");
                }
                foreach(bool left in new[]{true,false})foreach(float speed in new[]{.65f,1.7f})
                {
                    Prepare(left,speed);int hits=b.Punches;bake=true;
                    for(int i=0;i<hz/2;i++)Step(new PlayerInput{Tracking=true,Shield=true,GuardIntent=true});
                    Require(b.Punches==hits,"Late cancelled punch hit");bake=false;
                    Prepare(left,speed);var frozen=Points(bones);b.Pause();
                    for(int i=0;i<hz/3;i++)
                    {Draw(dt);for(int j=0;j<bones.Length;j++)pauseError=Mathf.Max(pauseError,Vector3.Distance(frozen[j],bones[j].position));}
                    if(!baseline)Require(pauseError<.0001f,"Handoff advanced during pause "+name);
                    for(int i=0;i<hz*2;i++)Step(new PlayerInput{Tracking=true});Require(b.Phase==GamePhase.Battle,"Pause failed to resume");
                    Prepare(left,speed);hits=b.Punches;Step(new PlayerInput{Tracking=true,LeftPunch=!left,RightPunch=left,AttackSpeed=speed});
                    Require(b.IsPunch,"Handoff blocked counterpunch");bake=true;
                    for(int i=0;i<hz;i++)
                    {
                        var oldHand=hero.StrikeOrigin(b.Action);var oldTarget=enemy.BeamSurfaceContact;
                        int previous=b.Punches;Step(new PlayerInput{Tracking=true});
                        if(b.Punches>previous&&!(name=="Mebius"&&!left))
                        {
                            // Hero aiming runs before enemy recoil in Draw. The
                            // target for this strike is the surface before that
                            // recoil, not the chest after it has been knocked back.
                            var end=hero.StrikeContact(b)-oldTarget;var start=oldHand-oldTarget;var span=end-start;
                            float swept=(start+span*Mathf.Clamp01(-Vector3.Dot(start,span)/Mathf.Max(.000001f,span.sqrMagnitude))).magnitude;
                            counterGap=Mathf.Max(counterGap,end.magnitude);counterSweep=Mathf.Max(counterSweep,swept);
                            Debug.Log($"[HeroGuardCounter] {name}-{hz} left={!left} speed={speed:F2} age={b.ActionAge:F4} aimedSample={end.magnitude:F4} swept={swept:F4} afterRecoil={Vector3.Distance(hero.StrikeContact(b),enemy.BeamSurfaceContact):F4}");
                        }
                    }
                    bake=false;Require(b.Punches==hits+1,"Counterpunch did not hit once");
                    Prepare(left,speed);b=Start();for(int i=0;i<hz;i++)Step(new PlayerInput{Tracking=true});
                    var fresh=new AnimatedActor(name,world.HeroHome,world.EnemyHome);fresh.SetOpponent(enemy);fresh.Update(b,world.Camera,dt,time);
                    Require(Vector3.Distance(hero.Root.position,fresh.Root.position)<.01f,"New round retained old root offset");
                    Require(Vector3.Distance(hero.HandPosition,fresh.HandPosition)<.04f,"New round retained old hand pose");
                    UnityEngine.Object.DestroyImmediate(fresh.Root.gameObject);
                }
                Require(ground>-.065f,"Handoff skin intersects floor "+name+" "+ground);
                Require(lengthError<.002f,"Handoff changed arm length "+name+" "+lengthError);
                // Same scene-distance budget as the existing roster contact
                // review. At 15 Hz the fast recovery is already visible on the
                // damage frame; retain both the sampled and swept distances.
                if(!baseline)Require(counterSweep<.75f&&(hz==15||counterGap<.75f),"Handoff delayed counter contact "+name+" sample="+counterGap+" swept="+counterSweep);
                UnityEngine.Object.DestroyImmediate(mesh);
                string result=$"{name}-{hz} sides=2 speeds=2 ground={ground:F5} lengthError={lengthError:F6} counterGap={counterGap:F5} counterSweep={counterSweep:F5} "+(baseline?"measured":"shield=pass pause=pass resume=pass counter=pass newRound=pass passed");
                report.AppendLine(result);Debug.Log("[HeroGuardHandoff] "+result);
            }
            string folder=Path.GetFullPath("../artifacts/melee-guard-20261009");Directory.CreateDirectory(folder);File.WriteAllText(folder+(baseline?"/baseline-lifecycle.txt":"/lifecycle.txt"),report.ToString());
        }
    }
}
