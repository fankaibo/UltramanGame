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
    public static class ReadyEntryReview
    {
        public static void Before()=>Run(false);
        public static void After()=>Run(true);
        static Transform Bone(Transform root,string a,string b)=>root.GetComponentsInChildren<Transform>().Single(t=>t.name==a||t.name==b);
        static void Run(bool check)
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/ready-entry-20261010/"+(check?"after":"before")));
            Directory.CreateDirectory(folder);var report=new StringBuilder();var protectedPoses=new StringBuilder();
            foreach(string id in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(101069);
                var world=new GameWorld();var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);
                var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                var upper=new[]{Bone(hero.Root,"armBase_L","bip_upperArm_L"),Bone(hero.Root,"armBase_R","bip_upperArm_R")};
                var lower=new[]{Bone(hero.Root,"ForearmBase_L","bip_lowerArm_L"),Bone(hero.Root,"ForearmBase_R","bip_lowerArm_R")};
                var wrist=new[]{Bone(hero.Root,"HandBase_L","bip_hand_L"),Bone(hero.Root,"HandBase_R","bip_hand_R")};
                var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
                try
                {
                    foreach(string context in new[]{"waiting","preview","arrival","battle"})
                    {
                        var state=new Battle();int preview=context=="preview"?0:-1;
                        if(context=="arrival"||context=="battle")
                        {
                            state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
                            float duration=context=="arrival"?MonsterEntranceMotion.Start+.3f:Battle.TransformationSeconds+.1f;
                            for(float t=0;t<duration;t+=.02f)state.Tick(.02f,new PlayerInput{Tracking=true});
                        }
                        for(int f=0;f<30;f++)hero.Update(state,world.Camera,1/60f,f/60f,preview);
                        enemy.Update(state,world.Camera,0,.5f,preview);world.Showcase=preview>=0;world.Tick(state,1,.5f);
                        for(int i=0;i<2;i++)
                        {
                            float span=Vector3.Distance(upper[i].position,lower[i].position)+Vector3.Distance(lower[i].position,wrist[i].position);
                            float reach=Vector3.Distance(upper[i].position,wrist[i].position)/span;
                            float angle=Vector3.Angle(upper[i].position-lower[i].position,wrist[i].position-lower[i].position);
                            report.AppendLine(FormattableString.Invariant($"{id} {context} hand={i} reach={reach:F4} angle={angle:F2}"));
                            if(check&&(reach<.80f||reach>.94f||angle<100||angle>150))throw new Exception("Folded/locked arm: "+report);
                        }
                        var left=wrist[0].position;var right=wrist[1].position;
                        hero.Update(state,world.Camera,0,29/60f,preview);
                        if(Vector3.Distance(left,wrist[0].position)>.0001f||Vector3.Distance(right,wrist[1].position)>.0001f)throw new Exception("Repeated pose drift: "+id+context);
                        CharacterReview.Save(world.Camera,rt,folder+"/"+id+"-"+context+".png");
                    }
                    // Sample independent preview poses on fresh actors so the
                    // ready stance cannot silently replace guard, beam or photo.
                    foreach(int pose in new[]{3,4,6,7})
                    {
                        var actor=new AnimatedActor(id,world.HeroHome,world.EnemyHome);
                        actor.Update(new Battle(),world.Camera,0,0,pose);
                        foreach(var bone in actor.Root.GetComponentsInChildren<Transform>())
                        {var p=bone.localPosition;var q=bone.localRotation;protectedPoses.AppendLine(FormattableString.Invariant($"{id},{pose},{bone.name},{p.x:F5},{p.y:F5},{p.z:F5},{q.x:F5},{q.y:F5},{q.z:F5},{q.w:F5}"));}
                        UnityEngine.Object.DestroyImmediate(actor.Root.gameObject);
                    }
                }
                finally{world.Camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
            }
            File.WriteAllText(folder+"/metrics.txt",report.ToString());File.WriteAllText(folder+"/protected-poses.csv",protectedPoses.ToString());
            if(check&&File.ReadAllText(Path.Combine(folder,"../before/protected-poses.csv"))!=protectedPoses.ToString())throw new Exception("Unrelated preview pose changed");
            Debug.Log("[ReadyEntryReview] PASS "+folder+"\n"+report);
        }
    }
}
