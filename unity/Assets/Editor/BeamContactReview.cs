using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class BeamContactReview
    {
        public static void Before()=>Run("before",false);
        public static void After()=>Run("after",true);
        static void Run(string version,bool enforce)
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/beam-volume",version));Directory.CreateDirectory(folder);
            var report=new StringBuilder();
            foreach(string id in new[]{"Tiga","Geed"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var world=new GameWorld();var state=new Battle(200);var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);
                var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});for(int i=0;i<120;i++)state.Tick(.02f,new PlayerInput{Tracking=true});state.GiveInstructionTime(30);
                for(int n=0;n<15;n++){state.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});for(int i=0;i<25;i++)state.Tick(.02f,new PlayerInput{Tracking=true});}
                while(state.TryCue(out _)){}hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,1,0);
                float health=state.EnemyHealth,maxGap=0;int samples=0;var mesh=new Mesh();
                try
                {
                    for(int frame=0;frame<260;frame++)
                    {
                        const float dt=1/60f;float time=frame*dt;
                        state.Tick(world.BattleDelta(dt,state),new PlayerInput{Tracking=true,Beam=frame==0});while(state.TryCue(out var cue))world.Cue(cue,state);
                        hero.Update(state,world.Camera,dt,time);enemy.Update(state,world.Camera,dt,time);
                        if(state.EnemyHealth<health)world.Hit(true,state);health=state.EnemyHealth;world.Tick(state,dt,time);
                        if(!world.BeamVisible||state.ActionAge<Battle.BeamHitSeconds||frame%3!=0)continue;
                        var target=world.BeamTarget;float distance=float.MaxValue;
                        foreach(var skin in enemy.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {
                            mesh.Clear();skin.BakeMesh(mesh,true);var vertices=mesh.vertices;var indices=mesh.triangles;
                            var weights=skin.sharedMesh.boneWeights;var bones=skin.bones;
                            for(int t=0;t<indices.Length;t+=3)
                            {
                                int a=indices[t],b=indices[t+1],c=indices[t+2];
                                // Restrict measurement to actual torso skin;
                                // a nearby claw cannot pass a chest-contact test.
                                if(Chest(weights[a],bones)+Chest(weights[b],bones)+Chest(weights[c],bones)<2.2f)continue;
                                var p=Nearest(target,skin.transform.TransformPoint(vertices[a]),skin.transform.TransformPoint(vertices[b]),skin.transform.TransformPoint(vertices[c]));
                                distance=Mathf.Min(distance,Vector3.Distance(p,target));
                            }
                        }
                        maxGap=Mathf.Max(maxGap,distance);samples++;
                        if(enforce&&distance>.004f)throw new Exception($"Beam missed deformed chest {id} age={state.ActionAge:F3} gap={distance:F4}");
                    }
                }
                finally{UnityEngine.Object.DestroyImmediate(mesh);}
                if(samples<15)throw new Exception("Insufficient chest contact samples");
                report.AppendLine($"{id} samples={samples} maximumDistanceToBakedTorso={maxGap:F6}");
            }
            File.WriteAllText(folder+"/chest-contact.txt",report.ToString());Debug.Log("[BeamContactReview] "+version+"\n"+report);
        }
        static float Chest(BoneWeight w,Transform[] bones)
        {
            float Part(int i,float weight)=>weight>0&&bones[i]&&bones[i].name.StartsWith("bip_spine",StringComparison.Ordinal)?weight:0;
            return Part(w.boneIndex0,w.weight0)+Part(w.boneIndex1,w.weight1)+Part(w.boneIndex2,w.weight2)+Part(w.boneIndex3,w.weight3);
        }
        static Vector3 Nearest(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
        {
            var ab=b-a;var ac=c-a;var ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);
            if(d1<=0&&d2<=0)return a;
            var bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0&&d4<=d3)return b;
            float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0)return a+ab*(d1/(d1-d3));
            var cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return c;
            float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0)return a+ac*(d2/(d2-d6));
            float va=d3*d6-d5*d4;if(va<=0&&d4-d3>=0&&d5-d6>=0)return b+(c-b)*((d4-d3)/((d4-d3)+(d5-d6)));
            float denom=va+vb+vc;if(Mathf.Abs(denom)<1e-12f)return a;return a+ab*(vb/denom)+ac*(vc/denom);
        }
    }
}
