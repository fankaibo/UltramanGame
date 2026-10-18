using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class MebiumBladeLifecycleReview
    {
        static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
        static Battle Start()
        {var b=new Battle(200);b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});b.GiveInstructionTime(60);return b;}
        static Transform Bone(Transform root,string name)
        {foreach(var t in root.GetComponentsInChildren<Transform>())if(t.name==name)return t;throw new Exception(name);}
        public static void Validate()
        {
            var report=new StringBuilder();
            foreach(int hz in new[]{15,30,60})foreach(float speed in new[]{.65f,1,1.7f})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var world=new GameWorld();var hero=new AnimatedActor("Mebius",world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);
                world.BindActors(hero,enemy);world.SetHeroProfile("Mebius");var b=Start();float time=0,dt=1f/hz;
                var wrist=Bone(hero.Root,"bip_hand_L");var elbow=Bone(hero.Root,"bip_lowerArm_L");var shoulder=Bone(hero.Root,"bip_upperArm_L");
                float a=Vector3.Distance(shoulder.position,elbow.position),c=Vector3.Distance(elbow.position,wrist.position),lengthError=0,anchorError=0;
                void Draw(float step=0)
                {
                    time+=step;hero.Update(b,world.Camera,step,time);enemy.Update(b,world.Camera,step,time);world.Tick(b,step,time);
                    lengthError=Mathf.Max(lengthError,Mathf.Abs(a-Vector3.Distance(shoulder.position,elbow.position)),Mathf.Abs(c-Vector3.Distance(elbow.position,wrist.position)));
                    if(world.Blade.Visible)
                    {
                        anchorError=Mathf.Max(anchorError,Vector3.Distance(world.Blade.Origin,hero.BladeOrigin));
                        Require(Vector3.Angle(world.Blade.Tip-world.Blade.Origin,wrist.position-elbow.position)<.10f,"Blade rotated away from forearm");
                    }
                }
                void Step(PlayerInput input){b.Tick(dt,input);Draw(dt);}
                void Clear(){Require(!world.Blade.Visible&&!world.Blade.SweepVisible,"Sword or slash wake survived suppression");}
                void Launch(){Step(new PlayerInput{Tracking=true,LeftPunch=true,AttackSpeed=speed});Require(world.Blade.Visible,"Left melee did not emit blade");}
                Draw(dt);
                foreach(float guardAt in new[]{.045f,.145f,.27f})
                {
                    b=Start();Draw(dt);Launch();while(b.ActionAge<guardAt&&b.IsPunch)Step(new PlayerInput{Tracking=true});
                    int expected=b.Punches;Step(new PlayerInput{Tracking=true,Shield=true,GuardIntent=true});Clear();Require(b.Shield,"Sword blocked guard entry");
                    for(int i=0;i<hz;i++)Step(new PlayerInput{Tracking=true,Shield=true,GuardIntent=true});
                    Require(b.Punches==expected,"Cancelled sword added a late hit");
                }
                b=Start();Draw(dt);Launch();int bladeSamples=0;
                for(int frame=0;frame<hz*4;frame++)
                {
                    bool queue=b.IsPunch&&b.ActionAge>=.23f&&b.AttackSequence<5;
                    Step(new PlayerInput{Tracking=true,LeftPunch=queue&&b.Action==HeroAction.RightPunch,RightPunch=queue&&b.Action==HeroAction.LeftPunch,AttackSpeed=speed});
                    if(world.Blade.Visible)bladeSamples++;
                }
                Require(b.Punches==5&&b.AttackSequence==5&&bladeSamples>1,"Linked sword/punch sequence failed");Clear();
                b=Start();Draw(dt);Launch();for(int n=0;n<4;n++)
                {
                    var tip=world.Blade.Tip;var origin=world.Blade.Origin;var hand=wrist.position;Draw();
                    Require(Vector3.Distance(tip,world.Blade.Tip)<.0001f&&Vector3.Distance(origin,world.Blade.Origin)<.0001f&&Vector3.Distance(hand,wrist.position)<.0001f,"Zero-time sample drifted");
                }
                var point=world.Blade.Tip;world.Camera.transform.rotation=Quaternion.Euler(0,83,0);
                world.Blade.Tick(b,hero.StrikeOrigin(HeroAction.LeftPunch),world.BattleAxis,world.BeamTarget,false);
                Require(Vector3.Distance(point,world.Blade.Tip)<.0001f,"Camera rotated sword");
                b.Pause();Draw();Clear();b=Start();Draw(dt);Clear();
                Launch();world.Blade.Tick(b,hero.StrikeOrigin(HeroAction.LeftPunch),world.BattleAxis,world.BeamTarget,true);Clear();Draw();Require(world.Blade.Visible,"Unsuppressed sword missing");
                world.SetHeroProfile("Tiga");Clear();world.SetHeroProfile("Mebius");Draw();Require(world.Blade.Visible,"Profile restore missing sword");
                var other=new AnimatedActor("Zero",world.HeroHome,world.EnemyHome);world.BindActors(other,enemy);world.SetHeroProfile("Zero");Clear();
                world.BindActors(hero,enemy);world.SetHeroProfile("Mebius");b=Start();Draw(dt);
                Step(new PlayerInput{Tracking=true,LeftPunch=true,RangedAttack=true,AttackSpeed=speed});for(int i=0;i<hz;i++){Clear();Step(new PlayerInput{Tracking=true});}
                b=Start();Draw(dt);
                // Fifth-hit uppercut/kick must keep its own animation, and the
                // actual charged beam must take the sword and wake offscreen.
                for(int n=0;n<15;n++)
                {
                    Step(new PlayerInput{Tracking=true,LeftPunch=n==4,RightPunch=n!=4,AttackSpeed=1});
                    for(int i=0;i<hz;i++){Clear();Step(new PlayerInput{Tracking=true});}
                }
                Require(b.Punches==15&&b.Energy==Battle.MaxEnergy,"Beam setup incomplete");Launch();
                Step(new PlayerInput{Tracking=true,Beam=true,BeamIntent=true});
                for(int i=0;i<hz&&b.Action!=HeroAction.Beam;i++)Step(new PlayerInput{Tracking=true,Beam=true,BeamIntent=true});
                Require(b.Action==HeroAction.Beam,"Sword prevented charged beam");Clear();
                Require(lengthError<.0001f&&anchorError<.0001f,"Arm length or socket changed");
                string line=$"{hz}Hz speed={speed:F2} guards=3 linkedFiveHits=pass zeroTime=pass camera=pass pause=pass newRound=pass closeup=pass switch=pass ranged=pass combo=pass beam=pass lengthError={lengthError:F6} anchorError={anchorError:F6} passed";
                Debug.Log("[MebiumBladeLifecycle] "+line);report.AppendLine(line);
            }
            var folder=Path.GetFullPath("../artifacts/mebium-blade-20261009");Directory.CreateDirectory(folder);File.WriteAllText(folder+"/lifecycle.txt",report.ToString());
        }
    }
}
