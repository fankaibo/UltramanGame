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
    public static class MonsterTailReview
    {
        public static void Before()=>Render("before",60,true);
        public static void After(){Render("after",60,true);Render("after",15,false);Render("after",30,false);}
        static void Render(string version,int rate,bool movie)
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/monster-tail-20261010/"+version));
            Directory.CreateDirectory(folder+"/frames");int image=0;var report=new StringBuilder();
            for(int attack=1;attack<=4;attack++)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(984);
                var world=new GameWorld();var b=new Battle();b.Tick(.01f,new PlayerInput{Tracking=true,Transform=true});
                for(int i=0;i<10000;i++)
                {if(b.Enemy==EnemyPhase.Windup&&b.EnemyAttackCount==attack-1&&b.WarningDuration-b.EnemyAge<=.7f)break;b.Tick(.01f,new PlayerInput{Tracking=true,Shield=true});}
                while(b.TryCue(out _)){}
                var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                var tails=enemy.Root.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("tail_",StringComparison.Ordinal)).OrderBy(t=>t.name).ToArray();
                if(tails.Length<3)throw new Exception("Need a segmented tail");
                var lengths=tails.Skip(1).Select(t=>Vector3.Distance(t.position,t.parent.position)).ToArray();
                var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
                var trace=new StringBuilder("frame,enemy,age,blocks,hurt,leftX,leftY,leftZ,rightX,rightY,rightZ,tailX,tailY,tailZ\n");
                float lengthError=0,minY=99,repeat=0,maxTipStep=0;Vector3 oldTip=Vector3.zero;
                try
                {
                    for(int f=0;f<rate*4;f++)
                    {
                        float dt=1f/rate,time=f*dt;b.Tick(world.BattleDelta(dt,b),new PlayerInput{Tracking=true,Shield=true});while(b.TryCue(out var cue))world.Cue(cue,b);
                        hero.Update(b,world.Camera,dt,time);enemy.Update(b,world.Camera,dt,time);world.Tick(b,dt,time);
                        Vector3 tip=tails[tails.Length-1].position,l=enemy.StrikeOrigin(HeroAction.LeftPunch),r=enemy.HandPosition;
                        if(f>0)maxTipStep=Mathf.Max(maxTipStep,Vector3.Distance(tip,oldTip));oldTip=tip;
                        for(int i=1;i<tails.Length;i++)lengthError=Mathf.Max(lengthError,Mathf.Abs(Vector3.Distance(tails[i].position,tails[i].parent.position)-lengths[i-1]));
                        foreach(var tail in tails)minY=Mathf.Min(minY,tail.position.y);
                        enemy.Update(b,world.Camera,0,time);
                        repeat=Mathf.Max(repeat,Vector3.Distance(tip,tails[tails.Length-1].position),Vector3.Distance(l,enemy.StrikeOrigin(HeroAction.LeftPunch)),Vector3.Distance(r,enemy.HandPosition));
                        trace.AppendLine(FormattableString.Invariant($"{f},{b.Enemy},{b.EnemyAge:F5},{b.Blocks},{b.HitsTaken},{l.x:F5},{l.y:F5},{l.z:F5},{r.x:F5},{r.y:F5},{r.z:F5},{tip.x:F5},{tip.y:F5},{tip.z:F5}"));
                        if(movie&&f%2==0)CharacterReview.Save(world.Camera,rt,$"{folder}/frames/frame-{image++:0000}.png");
                    }
                    string line=$"attack={attack} rate={rate} bones={tails.Length} blocks={b.Blocks} hurt={b.HitsTaken} boneLengthError={lengthError:F6} minJointY={minY:F4} repeat={repeat:F6} maxTipStep={maxTipStep:F4}";
                    report.AppendLine(line);Debug.Log("[MonsterTailReview] "+line);
                    File.WriteAllText($"{folder}/attack-{attack}-{rate}.csv",trace.ToString());
                    if(lengthError>.001f||repeat>.001f||minY<-.01f||b.Blocks!=attack||b.HitsTaken!=0)throw new Exception(line);
                }
                finally{world.Camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
            }
            File.WriteAllText($"{folder}/validation-{rate}.txt",report.ToString());
        }
    }
}
