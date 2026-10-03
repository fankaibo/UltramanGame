using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class FinalStrikeReview
    {
        static string Root=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/final-strike"));
        public static void Before()=>Run("before","Tiga",true);
        public static void After(){for(int i=0;i<HeroRoster.Count;i++)Run("after",HeroRoster.At(i).Id,i==0);PauseAndResume();}
        public static void PauseAndResume()
        {
            var report=new StringBuilder();
            foreach(float interrupt in new[]{.55f,.95f,1.35f})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var world=new GameWorld();var b=new Battle(24);var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);
                var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});Step(b,Battle.TransformationSeconds+.2f);
                for(int i=0;i<15;i++){b.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});Step(b,.5f);}
                while(b.TryCue(out _)){}hero.Update(b,world.Camera,0,0);enemy.Update(b,world.Camera,0,0);world.Tick(b,1,0);
                float health=b.EnemyHealth,time=0;int releases=0,hits=0,victories=0;
                do
                {
                    b.Tick(world.BattleDelta(.02f,b),new PlayerInput{Tracking=true,Beam=b.Action!=HeroAction.Beam});
                    while(b.TryCue(out var cue))world.Cue(cue,b);
                    time+=.02f;hero.Update(b,world.Camera,.02f,time);enemy.Update(b,world.Camera,.02f,time);
                    if(b.EnemyHealth<health){hits++;world.Hit(true,b);}health=b.EnemyHealth;world.Tick(b,.02f,time);if(world.BeamStarted)releases++;
                }while(!b.Finishing||b.ActionAge<interrupt);
                float actionAge=b.ActionAge;int landings=enemy.BeamLandings;Vector3 heroHand=hero.HandPosition,claw=enemy.HandPosition;
                b.Pause();for(int f=0;f<50;f++){b.Tick(.02f,default);time+=.02f;hero.Update(b,world.Camera,.02f,time);enemy.Update(b,world.Camera,.02f,time);world.Tick(b,.02f,time);}
                if(Vector3.Distance(heroHand,hero.HandPosition)>.0001f||Vector3.Distance(claw,enemy.HandPosition)>.0001f||world.BeamVisible||b.ActionAge!=actionAge)throw new Exception("Terminal pause changed pose/clock or left a beam");
                while(b.Phase==GamePhase.Paused)b.Tick(.02f,new PlayerInput{Tracking=true});
                hero.Update(b,world.Camera,0,time);enemy.Update(b,world.Camera,0,time);world.Tick(b,0,time);if(world.BeamStarted)releases++;
                float resumeHand=Mathf.Max(Vector3.Distance(heroHand,hero.HandPosition),Vector3.Distance(claw,enemy.HandPosition));
                for(int f=0;f<100;f++)
                {
                    b.Tick(world.BattleDelta(.02f,b),new PlayerInput{Tracking=true});
                    while(b.TryCue(out var cue)){world.Cue(cue,b);if(cue==GameCue.Victory)victories++;}
                    time+=.02f;hero.Update(b,world.Camera,.02f,time);enemy.Update(b,world.Camera,.02f,time);world.Tick(b,.02f,time);if(world.BeamStarted)releases++;
                }
                string result=$"pauseAt={interrupt:F2} hits={hits} releases={releases} victories={victories} resumeHand={resumeHand:F6} braceDelta={enemy.BeamLandings-landings}";
                if(hits!=1||releases!=1||victories!=1||resumeHand>.015f||enemy.BeamLandings!=1||b.Phase!=GamePhase.Victory)throw new Exception(result);
                report.AppendLine(result);Debug.Log("[FinalStrikePause] "+result);
            }
            Directory.CreateDirectory(Root);File.WriteAllText(Path.Combine(Root,"pause-validation.txt"),report.ToString());
        }
        static void Run(string version,string heroId,bool film)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(260930);
            var world=new GameWorld();var b=new Battle(24);
            var hero=new AnimatedActor(heroId,world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});Step(b,Battle.TransformationSeconds+.2f);
            for(int hit=0;hit<15;hit++){b.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});Step(b,.5f);}
            b.GiveInstructionTime(10);while(b.TryCue(out _)){}
            hero.Update(b,world.Camera,0,0);enemy.Update(b,world.Camera,0,0);world.Tick(b,1,0);
            string folder=Path.Combine(Root,version,heroId);Directory.CreateDirectory(folder+"/frames");
            var sources=new StringBuilder();using(var sha=System.Security.Cryptography.SHA256.Create())
                foreach(string file in new[]{"Scripts/Core/Battle.cs","Scripts/Runtime/ArcadeHud.cs","Scripts/Runtime/GameWorld.cs","Scripts/Runtime/RiggedActor.cs","Scripts/Runtime/GameAudio.cs","Scripts/Runtime/ArenaController.cs","Editor/FinalStrikeReview.cs"})
                    sources.AppendLine(file+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Application.dataPath,file)))).Replace("-","").ToLowerInvariant());
            File.WriteAllText(folder+"/sources.txt",sources.ToString());File.Copy(Path.Combine(Application.dataPath,"Editor/FinalStrikeReview.cs"),folder+"/review-source.cs",true);
            var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
            var trace=new StringBuilder("time,phase,actionAge,health,beam,victoryAge\n");int wins=0,hits=0,releases=0,landings=0;float victoryActionAge=0,lastBeamAge=0,health=b.EnemyHealth,contactTime=-1;
            float entryHandJump=-1,minimumGround=100,zeroDrift=0;bool repeated=false;var mesh=new Mesh();
            bool[] saved=new bool[5];float[] times={.12f,.55f,1.0f,1.8f,4.7f};string[] names={"contact","sustain","release","collapse","hero"};
            try
            {
                for(int frame=0;frame<540;frame++)
                {
                    const float dt=1/60f;float time=frame*dt;
                    var beforeHand=enemy.HandPosition;bool won=false;
                    b.Tick(world.BattleDelta(dt,b),new PlayerInput{Tracking=true,Beam=frame==12});
                    while(b.TryCue(out var cue)){world.Cue(cue,b);if(cue==GameCue.Victory){wins++;won=true;victoryActionAge=b.ActionAge;}}
                    hero.Update(b,world.Camera,dt,time);enemy.Update(b,world.Camera,dt,time);
                    if(b.EnemyHealth<health){hits++;contactTime=time;world.Hit(true,b);}health=b.EnemyHealth;world.Tick(b,dt,time);enemy.SetPresentationOpacity(world.EnemyOpacity);
                    if(world.BeamStarted)releases++;if(world.MonsterLanded)landings++;
                    if(won)entryHandJump=Vector3.Distance(beforeHand,enemy.HandPosition);
                    if(b.Phase==GamePhase.Victory&&world.VictoryAge<1.2f&&frame%3==0)
                        foreach(var skin in enemy.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {skin.BakeMesh(mesh,true);foreach(var point in mesh.vertices)minimumGround=Mathf.Min(minimumGround,skin.transform.TransformPoint(point).y);}
                    if(b.Phase==GamePhase.Victory&&world.VictoryAge>.16f&&!repeated)
                    {
                        var bones=enemy.Root.GetComponentsInChildren<Transform>();var points=new Vector3[bones.Length];for(int i=0;i<bones.Length;i++)points[i]=bones[i].position;
                        enemy.Update(b,world.Camera,0,time);
                        for(int i=0;i<bones.Length;i++)zeroDrift=Mathf.Max(zeroDrift,Vector3.Distance(points[i],bones[i].position));repeated=true;
                    }
                    if(world.BeamVisible)lastBeamAge=b.ActionAge;
                    trace.AppendLine(FormattableString.Invariant($"{time:F4},{b.Phase},{b.ActionAge:F4},{b.EnemyHealth},{world.BeamVisible},{world.VictoryAge:F4}"));
                    if(film&&frame%2==0)CharacterReview.Save(world.Camera,rt,$"{folder}/frames/{frame/2:D4}.png");
                    for(int i=0;i<times.Length;i++)if(!saved[i]&&contactTime>=0&&time-contactTime>=times[i])
                    {saved[i]=true;CharacterReview.Save(world.Camera,rt,folder+"/"+names[i]+".png");}
                }
                string result=$"{version}/{heroId} hits={hits} releases={releases} victory={wins} landings={landings} health={b.EnemyHealth} punches={b.Punches} lastBeamAge={lastBeamAge:F4} victoryActionAge={victoryActionAge:F4} entryHandJump={entryHandJump:F4} minimumGround={minimumGround:F4} zeroDrift={zeroDrift:F6} ended={b.Phase}";
                File.WriteAllText(folder+"/sequence.csv",trace.ToString());Debug.Log("[FinalStrikeReview] "+result);
                if(wins!=1||hits!=1||releases!=1||landings!=1||b.Punches!=15||b.EnemyHealth!=0||b.Phase!=GamePhase.Victory||world.BeamVisible)throw new Exception(result);
                if(version=="after"&&(lastBeamAge<1.40f||victoryActionAge<Battle.BeamSeconds))throw new Exception("Finisher still interrupted: "+result);
                if(version=="after"&&(entryHandJump<0||entryHandJump>.12f||minimumGround<-.035f||zeroDrift>.0002f))throw new Exception("Defeat transition discontinuous: "+result);
                File.WriteAllText(folder+"/validation.txt",result);
            }
            finally{world.Camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(mesh);}
        }
        static void Step(Battle b,float duration)
        {for(float t=0;t<duration;t+=.02f)b.Tick(.02f,new PlayerInput{Tracking=true,Shield=true});}
    }
}
