using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class ZeroSluggerLifecycleReview
    {
        static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
        static Battle Start()
        {var b=new Battle(200);b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});b.GiveInstructionTime(60);return b;}
        static Vector3[] Bake(SkinnedMeshRenderer skin)
        {var mesh=new Mesh();skin.BakeMesh(mesh,true);var vertices=mesh.vertices;UnityEngine.Object.DestroyImmediate(mesh);return vertices;}
        static float Error(Vector3[] a,Vector3[] b)
        {float result=0;for(int i=0;i<a.Length;i++)result=Mathf.Max(result,Vector3.Distance(a[i],b[i]));return result;}
        static string Geometry(AnimatedActor hero)
        {
            var rig=hero.Sluggers;Require(rig!=null,"Original blades not bound");
            var skin=hero.Root.GetComponentInChildren<SkinnedMeshRenderer>();var shared=skin.sharedMesh;
            var before=Bake(skin);var weights=shared.boneWeights;var bones=skin.bones;var materials=skin.sharedMaterials;
            rig.Pose(0,rig.Center(0)+new Vector3(3,2,1),Quaternion.Euler(42,57,63)*rig.MountedRotation(0));
            rig.Pose(1,rig.Center(1)+new Vector3(-2,1,3),Quaternion.Euler(-56,30,93)*rig.MountedRotation(1));
            var after=Bake(skin);float bodyError=0,rigidError=0;int body=0,moved=0;
            int[] first={-1,-1};
            for(int v=0;v<weights.Length;v++)
            {
                var w=weights[v];string bone=bones[w.boneIndex0].name;int side=bone=="slugger_L"?0:bone=="slugger_R"?1:-1;
                if(side<0){bodyError=Mathf.Max(bodyError,Vector3.Distance(before[v],after[v]));body++;continue;}
                Require(w.weight0>.9999f,"Blade has blended skin weights");
                if(first[side]<0)first[side]=v;
                rigidError=Mathf.Max(rigidError,Mathf.Abs(Vector3.Distance(before[v],before[first[side]])-Vector3.Distance(after[v],after[first[side]])));
                if(Vector3.Distance(before[v],after[v])>.1f)moved++;
            }
            Require(body>1000&&moved==rig.VertexCount(0)+rig.VertexCount(1),"Unexpected blade/body ownership");
            rig.Restore();float restoreError=Error(before,Bake(skin));
            Require(bodyError<.0001f&&rigidError<.0001f&&restoreError<.0001f,"Head stretched or original blade distorted");
            Require(skin.sharedMesh==shared&&skin.sharedMaterials.Length==materials.Length,"Mesh or material replaced");
            return $"geometry bodyVertices={body} bladeVertices={moved} bodyError={bodyError:F6} rigidError={rigidError:F6} restoreError={restoreError:F6} passed";
        }
        public static void Validate()
        {
            var report=new StringBuilder();
            foreach(int hz in new[]{15,30,60})foreach(float speed in new[]{.65f,1,1.7f})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var world=new GameWorld();var hero=new AnimatedActor("Zero",world.HeroHome,world.EnemyHome);
                var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);world.SetHeroProfile("Zero");
                var rig=hero.Sluggers;var fx=world.Projectile;var b=Start();float dt=1f/hz,time=0;int hits=0;
                void Draw(float step=0,bool suppress=false)
                {
                    time+=step;hero.Update(b,world.Camera,step,time);enemy.Update(b,world.Camera,step,time);
                    fx.Tick(b,world.Camera,hero.HandPosition,enemy.BeamContact,suppress,hero.RayOrigin,step);
                }
                void Step(PlayerInput input)
                {b.Tick(dt,input);if(b.LastDamageRanged){fx.Impact(enemy.BeamContact,world.BattleAxis);hits++;}Draw(dt);}
                void Restored()
                {Require(!rig.Detached&&!fx.Visible,"Blade remains detached");for(int i=0;i<2;i++)Require(Vector3.Distance(rig.Center(i),rig.MountedCenter(i))<.0001f,"Blade did not dock");}
                void Launch()
                {Step(new PlayerInput{Tracking=true,RightPunch=true,RangedAttack=true,AttackSpeed=speed});for(int i=0;i<hz&&!b.Shot.Active;i++)Step(new PlayerInput{Tracking=true});Require(b.Shot.Active&&rig.Detached,"Missing launch");}
                Draw(dt);if(hz==15&&speed==.65f)report.AppendLine(Geometry(hero));
                // A guard acquired while preparing must not throw the blades.
                Step(new PlayerInput{Tracking=true,LeftPunch=true,RangedAttack=true,AttackSpeed=speed});
                Step(new PlayerInput{Tracking=true,Shield=true,GuardIntent=true});
                for(int i=0;i<hz;i++)Step(new PlayerInput{Tracking=true,Shield=true,GuardIntent=true});
                Restored();Require(hits==0&&fx.Launches==0,"Pre-release defence launched a blade");
                b=Start();hits=0;Draw(dt);Launch();
                Step(new PlayerInput{Tracking=true,Shield=true,GuardIntent=true});Require(b.Shield&&rig.Detached,"Guard erased travelling original blades");
                var positions=new[]{rig.Center(0),rig.Center(1)};var bounds=hero.Root.GetComponentInChildren<SkinnedMeshRenderer>().localBounds;
                for(int n=0;n<4;n++)Draw();
                for(int i=0;i<2;i++)Require(Vector3.Distance(positions[i],rig.Center(i))<.0001f,"Repeat render advanced blade");
                Require(bounds==hero.Root.GetComponentInChildren<SkinnedMeshRenderer>().localBounds,"Repeat render grew skin bounds");
                world.Camera.transform.rotation=Quaternion.Euler(0,63,0);Draw();
                for(int i=0;i<2;i++)Require(Vector3.Distance(positions[i],rig.Center(i))<.0001f,"Camera changed trajectory");
                for(int i=0;i<hz*2;i++)Step(new PlayerInput{Tracking=true,Shield=true,GuardIntent=true});
                Restored();Require(hits==1&&fx.Impacts==1&&fx.Launches==1,"Guard changed single damage resolution");
                b=Start();hits=0;Draw(dt);Launch();b.Pause();Draw();Restored();
                for(int i=0;i<hz;i++)Step(new PlayerInput{Tracking=true});Require(hits==0,"Pause caused late damage");
                b=Start();Draw(dt);Restored();Require(fx.Launches==0&&fx.Impacts==0,"New round retained effect counters");
                Launch();Draw(0,true);Restored(); // closeup / photo presentation suppression
                Draw();Require(rig.Detached,"Un-suppress failed to restore live shot");
                fx.SetHero("Tiga");Restored();fx.SetHero("Zero");Draw();Require(rig.Detached,"Same-frame hero profile reset failed");
                var other=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);world.BindActors(other,enemy);Restored();world.SetHeroProfile("Tiga");
                string row=$"{hz}Hz speed={speed:F2} cancelBeforeLaunch=pass guardFlight=pass impactOnce=pass zeroTime=pass bounds=pass camera=pass movingHeadDock=pass pause=pass newRound=pass suppression=pass heroSwitch=pass passed";
                Debug.Log("[ZeroSluggerLifecycle] "+row);report.AppendLine(row);
            }
            string folder=Path.GetFullPath("../artifacts/zero-slugger-20261009");Directory.CreateDirectory(folder);File.WriteAllText(folder+"/lifecycle.txt",report.ToString());
        }
    }
}
