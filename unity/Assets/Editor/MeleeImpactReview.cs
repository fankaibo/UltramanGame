using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    // Same accepted attacks and camera for both captures; no input bypass or
    // screenshot-only effect parameters. Bake skinned meshes on every frame.
    public static class MeleeImpactReview
    {
        public static void Before()=>Render("before");
        public static void After(){Render("after");StrikeContactReview.Check();}
        static void Render(string label)
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/melee-impact-20261012/"+label));
            Directory.CreateDirectory(folder);
            var receipts=new StringBuilder("hz,frame,action,age,health,energy,punches,heroX,heroY,heroZ,enemyX,enemyY,enemyZ\n");
            foreach(int hz in new[]{15,30,60})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(119);
                var w=new GameWorld();var b=new Battle();var h=new AnimatedActor("Tiga",w.HeroHome,w.EnemyHome);var e=new AnimatedActor("Golza",w.EnemyHome,w.HeroHome,true);w.BindActors(h,e);
                b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});for(int i=0;i<(Battle.TransformationSeconds+.2f)/.02f;i++)b.Tick(.02f,new PlayerInput{Tracking=true});b.GiveInstructionTime(20);while(b.TryCue(out _)){}
                h.Update(b,w.Camera,0,0);e.Update(b,w.Camera,0,0);w.Tick(b,1,0);
                var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();w.Camera.targetTexture=rt;w.Camera.aspect=16f/9;
                string frames=folder+"/frames";if(hz==30)Directory.CreateDirectory(frames);
                float health=b.EnemyHealth,time=0,dt=1f/hz;int hits=0;float sinceHit=10;
                try
                {
                    for(int f=0;f<hz*4;f++)
                    {
                        int slot=f/Mathf.RoundToInt(hz*.7f);bool fire=slot<5&&f%Mathf.RoundToInt(hz*.7f)==0;
                        b.Tick(w.BattleDelta(dt,b),new PlayerInput{Tracking=true,LeftPunch=fire&&slot%2==0,RightPunch=fire&&slot%2==1});while(b.TryCue(out var cue))w.Cue(cue,b);
                        h.Update(b,w.Camera,dt,time);e.Update(b,w.Camera,dt,time);
                        if(b.EnemyHealth<health){w.Hit(false,b);hits++;sinceHit=0;}else sinceHit+=dt;
                        health=b.EnemyHealth;w.Tick(b,dt,time);time+=dt;
                        var a=h.Root.position;var z=e.Root.position;
                        receipts.AppendLine(FormattableString.Invariant($"{hz},{f},{b.Action},{b.ActionAge:F5},{b.EnemyHealth},{b.Energy},{b.Punches},{a.x:F5},{a.y:F5},{a.z:F5},{z.x:F5},{z.y:F5},{z.z:F5}"));
                        if(hz==30)
                        {
                            CharacterReview.Save(w.Camera,rt,$"{frames}/{f:D4}.jpg");
                            if(hits==1&&sinceHit<.13f)CharacterReview.Save(w.Camera,rt,$"{folder}/contact-{f:D4}.png");
                        }
                    }
                    if(hits!=5||b.EnemyHealth!=45||b.Energy!=5||w.ActiveContactCount!=0)throw new Exception($"Contact sequence failed hz={hz} hits={hits} health={b.EnemyHealth} energy={b.Energy}");
                    Debug.Log($"[MeleeImpactReview] PASS {label} hz={hz} hits={hits} health={health} expired=True");
                }
                finally{w.Camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
            }
            File.WriteAllText(folder+"/receipts.csv",receipts.ToString());
        }
    }
}
