using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class RangedLifecycleReview
    {
        static Battle BattleStart()
        {var b=new Battle(200);b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});b.GiveInstructionTime(20);return b;}
        public static void Validate()
        {
            var report=new StringBuilder();
            foreach(string hero in new[]{"Tiga","Zero"})foreach(int hz in new[]{15,30,60})foreach(float speed in new[]{.65f,1.7f})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var root=new GameObject("Lifecycle test").transform;var camera=new GameObject("Test camera").AddComponent<Camera>();camera.transform.position=new Vector3(0,2,-6);
                var fx=new HeroProjectile(root);fx.SetHero(hero);var b=BattleStart();float dt=1f/hz;int hits=0;
                Vector3 hand=new Vector3(-1,2,0),target=new Vector3(2,2,0),head=new Vector3(-1,3,0);
                void Draw(float step=0){fx.Tick(b,camera,hand,target,false,head,step);}
                void Step(PlayerInput input)
                {b.Tick(dt,input);if(b.LastDamageRanged){fx.Impact(target,Vector3.right);hits++;}Draw(dt);}
                bool Cleared()=>!fx.Visible&&!fx.LaunchVisible&&!fx.ImpactVisible;
                // Acquiring defence after release must not erase a travelling shot.
                Draw();Step(new PlayerInput{Tracking=true,LeftPunch=true,RangedAttack=true,AttackSpeed=speed});
                while(!b.Shot.Active)Step(new PlayerInput{Tracking=true});
                Step(new PlayerInput{Tracking=true,Shield=true});
                int expectedHits=b.Shot.Age>=AttackTempo.RangedHitSeconds?1:0;
                if(!b.Shield||!fx.Visible||hits!=expectedHits)throw new Exception("Guard lost in-flight shot");
                while(hits==0)Step(new PlayerInput{Tracking=true,Shield=true});
                if(fx.Impacts!=1||!fx.ImpactVisible)throw new Exception("Missing true contact");
                var tip=fx.Tip;
                for(int i=0;i<3;i++)Draw();
                if(Vector3.Distance(tip,fx.Tip)>.0001f||fx.Impacts!=1)throw new Exception("Zero-time render advanced flight or repeated impact");
                if(hero=="Zero"&&fx.Visible)
                {
                    Vector3 left=fx.BladePosition(0),right=fx.BladePosition(1);camera.transform.rotation=Quaternion.Euler(0,50,0);Draw();
                    if(Vector3.Distance(left,fx.BladePosition(0))>.0001f||Vector3.Distance(right,fx.BladePosition(1))>.0001f)throw new Exception("Camera moved blade trajectory");
                }
                b.Pause();Draw();if(!Cleared())throw new Exception("Impact or launch persisted after pause");
                b=BattleStart();Draw();if(!Cleared()||fx.Impacts!=0||fx.Launches!=0)throw new Exception("New round retained effects");
                hits=0;Step(new PlayerInput{Tracking=true,RightPunch=true,RangedAttack=true,AttackSpeed=speed});while(!b.Shot.Active)Step(new PlayerInput{Tracking=true});
                b.Pause();Draw();for(int i=0;i<hz*2;i++)Step(new PlayerInput{Tracking=true});
                if(hits!=0||!Cleared())throw new Exception("Cancelled flight became a late hit");
                b=BattleStart();Draw();for(int n=0;n<15;n++){Step(new PlayerInput{Tracking=true,LeftPunch=true});for(int i=0;i<hz;i++)Step(new PlayerInput{Tracking=true});}
                Step(new PlayerInput{Tracking=true,RightPunch=true,RangedAttack=true,AttackSpeed=speed});while(!b.Shot.Active)Step(new PlayerInput{Tracking=true});
                Step(new PlayerInput{Tracking=true,Beam=true,BeamIntent=true});
                for(int i=0;i<hz&&b.Action!=HeroAction.Beam;i++)Step(new PlayerInput{Tracking=true,Beam=true,BeamIntent=true});
                fx.Tick(b,camera,hand,target,true,head,dt);
                if(b.Action!=HeroAction.Beam||!Cleared())throw new Exception($"Beam closeup mismatch action={b.Action} energy={b.Energy} punches={b.Punches} clear={Cleared()}");
                fx.SetHero(hero=="Tiga"?"Zero":"Tiga");if(!Cleared())throw new Exception("Hero selection retained remote effects");
                string row=$"{hero}-{hz} speed={speed:F2} guardFlight=pass impactOnce=pass zeroTime=pass cameraPath=pass pause=pass newRound=pass cancelled=pass beam=pass heroSwitch=pass";
                report.AppendLine(row);Debug.Log("[RangedLifecycle] "+row);
            }
            string folder=Path.GetFullPath("../artifacts/ranged-presentation-20261008");Directory.CreateDirectory(folder);File.WriteAllText(folder+"/lifecycle.txt",report.ToString());
        }
    }
}
