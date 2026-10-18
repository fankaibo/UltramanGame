using System;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class TigaFistShapeReview
    {
        static string Folder=>Path.GetFullPath(Application.dataPath+"/../../artifacts/fist-shape-20261012");
        static Transform Bone(Transform root,string name)
        {foreach(var t in root.GetComponentsInChildren<Transform>())if(t.name==name)return t;throw new Exception(name);}
        public static void InspectBindings()
        {
            Directory.CreateDirectory(Folder);
            var prefab=Resources.Load<GameObject>("Characters/Tiga/Tiga");
            var report=new System.Text.StringBuilder();
            report.AppendLine($"quality={QualitySettings.GetQualityLevel()} weights={QualitySettings.skinWeights}");
            foreach(var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh=skin.sharedMesh;report.AppendLine($"{skin.name} vertices={mesh.vertexCount} blendShapes={mesh.blendShapeCount} quality={skin.quality}");
                for(int i=0;i<mesh.blendShapeCount;i++)report.AppendLine($"shape={mesh.GetBlendShapeName(i)} value={skin.GetBlendShapeWeight(i)}");
            }
            File.WriteAllText(Folder+"/unity-bindings.txt",report.ToString());Debug.Log("[TigaFistShapeReview] bindings\n"+report);
        }
        // Offline rejected candidates are retained to make the visual investigation
        // reproducible. None of these offsets are used by the runtime.
        struct Candidate
        {
            public int Id;public float Proximal,Middle,Distal,ThumbAcross,ThumbMiddle,ThumbTip;
            public bool MirrorIndex,SolveThumb,CorrectNormal;
            public Candidate(int id,float proximal,float middle,float distal,float across,float thumbMiddle,float thumbTip,bool mirror=false,bool solve=false,bool normal=false)
            {Id=id;Proximal=proximal;Middle=middle;Distal=distal;ThumbAcross=across;ThumbMiddle=thumbMiddle;ThumbTip=thumbTip;MirrorIndex=mirror;SolveThumb=solve;CorrectNormal=normal;}
        }
        static readonly Candidate[] Candidates={
            new Candidate(0,0,0,0,0,0,0),
            new Candidate(1,10,15,10,0,25,10),new Candidate(2,20,10,0,0,25,10),
            new Candidate(3,20,10,0,0,25,10,true),new Candidate(4,30,10,0,0,25,10,true),
            new Candidate(5,45,10,0,0,25,10,true),new Candidate(6,60,10,0,0,25,10,true),
            new Candidate(7,20,10,0,20,10,0,true),new Candidate(8,30,10,0,35,10,0,true),
            new Candidate(9,40,10,0,50,10,0,true),new Candidate(10,0,35,15,0,25,10,true),
            new Candidate(11,5,50,25,0,25,10,true),new Candidate(12,30,10,0,20,50,25,true),
            new Candidate(13,10,30,0,0,0,0,true,true,false),new Candidate(14,10,30,0,0,0,0,true,true,true),new Candidate(15,0,0,0,0,0,0),new Candidate(16,0,0,0,0,0,0),new Candidate(17,0,0,0,0,0,0)
        };
        public static void Explore()=>Run(false);
        public static void ExploreAll()=>Run(true);
        static void AnatomicalFist(Transform root,bool left,int variant)
        {
            string suffix=left?"_L":"_R";var wrist=Bone(root,"HandBase"+suffix);
            var mid=Bone(root,"Mid1"+suffix);
            var across=(Bone(root,"Index1"+suffix).position-Bone(root,"Pinky1"+suffix).position).normalized;
            var up=Vector3.ProjectOnPlane(mid.position-wrist.position,across).normalized;
            var normal=Vector3.Cross(up,across).normalized;
            if(Vector3.Dot(normal,Bone(root,"Mid2"+suffix).position-mid.position)<0)normal=-normal;
            void Aim(string name,Vector3 direction)
            {var joint=Bone(root,name+suffix);joint.rotation=Quaternion.FromToRotation(joint.up,direction)*joint.rotation;}
            string[] names={"Index","Mid","Ring","Pinky"};float[] gather={-.10f,0,.04f,.16f};
            for(int i=0;i<names.Length&&variant==15;i++)
            {
                Aim(names[i]+"1",normal-up*.05f+across*gather[i]);
                Aim(names[i]+"2",-up-normal*.025f+across*gather[i]*.3f);
                Aim(names[i]+"3",-up*.55f-normal*.835f);
            }
            if(variant==15)
            {
                Aim("Thumb1",up*.70f-across*.10f+normal*.70f);
                Aim("Thumb2",up*.65f-across*.60f+normal*.47f);
                Aim("Thumb3",-up*.05f-across*.998f-normal*.04f);
            }
            else
            {
                Aim("Thumb1",up*.70f-across*.20f+normal*.68f);
                Aim("Thumb2",up*.60f-across*.60f+normal*.50f);
                Aim("Thumb3",up*.15f-across*.94f-normal*.30f);
                if(variant==17)
                {
                    var joint=Bone(root,"Thumb3"+suffix);
                    var targetNormal=Vector3.ProjectOnPlane(normal,joint.up).normalized;
                    joint.rotation=Quaternion.LookRotation(targetNormal*(left?-1:1),joint.up);
                }
            }
        }
        static void Run(bool all)
        {
            Directory.CreateDirectory(Folder);
            foreach(var candidate in Candidates)
            {
                int variant=candidate.Id;if(!all&&variant!=0&&variant!=16&&variant!=17)continue;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(140);
                var world=new GameWorld();var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);
                var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);world.SetHeroProfile("Tiga");
                var battle=new Battle();battle.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
                while(battle.Phase!=GamePhase.Battle)battle.Tick(.02f,new PlayerInput{Tracking=true});
                battle.GiveInstructionTime(30);while(battle.TryCue(out _)){}
                for(int f=0;f<60;f++)
                {battle.Tick(1f/60,new PlayerInput{Tracking=true});hero.Update(battle,world.Camera,1f/60,f/60f);enemy.Update(battle,world.Camera,1f/60,f/60f);world.Tick(battle,1f/60,f/60f);}
                if(variant>=15)
                {foreach(bool left in new[]{true,false})AnatomicalFist(hero.Root,left,variant);}
                else if(variant>0)
                foreach(bool left in new[]{true,false})
                {
                    string side=left?"_L":"_R";float sign=left?-1:1;
                    foreach(string finger in new[]{"Index","Mid","Ring","Pinky"})
                    {
                        Bone(hero.Root,finger+"1"+side).localRotation*=Quaternion.Euler(0,0,sign*candidate.Proximal);
                        Bone(hero.Root,finger+"2"+side).localRotation*=Quaternion.Euler(0,0,sign*candidate.Middle);
                        Bone(hero.Root,finger+"3"+side).localRotation*=Quaternion.Euler(0,0,sign*candidate.Distal);
                    }
                    Bone(hero.Root,"Thumb1"+side).localRotation*=Quaternion.Euler(0,0,sign*candidate.ThumbAcross);
                    Bone(hero.Root,"Thumb2"+side).localRotation*=Quaternion.Euler(candidate.ThumbMiddle,0,0);
                    Bone(hero.Root,"Thumb3"+side).localRotation*=Quaternion.Euler(candidate.ThumbTip,0,0);
                    if(candidate.MirrorIndex&&left)
                    {var q=Bone(hero.Root,"Index3_R").localRotation;Bone(hero.Root,"Index3_L").localRotation=new Quaternion(q.x,-q.y,-q.z,q.w);}
                    if(candidate.SolveThumb)
                    {
                        var wrist=Bone(hero.Root,"HandBase"+side);var m=Bone(hero.Root,"Mid1"+side);
                        var normal=Vector3.Cross(m.position-wrist.position,Bone(hero.Root,"Index1"+side).position-Bone(hero.Root,"Pinky1"+side).position).normalized;
                        if(candidate.CorrectNormal)
                        {if(Vector3.Dot(normal,Bone(hero.Root,"Mid2"+side).position-m.position)<0)normal=-normal;}
                        else normal*=left?1:-1;
                        var thumb=new[]{Bone(hero.Root,"Thumb1"+side),Bone(hero.Root,"Thumb2"+side),Bone(hero.Root,"Thumb3"+side)};
                        var target=Vector3.Lerp(Bone(hero.Root,"Mid2"+side).position,Bone(hero.Root,"Mid3"+side).position,.4f)+normal*(.014f*wrist.lossyScale.x);
                        for(int step=0;step<6;step++)for(int j=2;j>=0;j--)
                        {var tip=thumb[2].TransformPoint(Vector3.up*.025f);var delta=Quaternion.FromToRotation(tip-thumb[j].position,target-thumb[j].position);thumb[j].rotation=Quaternion.RotateTowards(Quaternion.identity,delta,15)*thumb[j].rotation;}
                    }
                }
                var metrics=new System.Text.StringBuilder();
                foreach(bool left in new[]{true,false})
                {
                    string suffix=left?"_L":"_R";
                    var wrist=Bone(hero.Root,"HandBase"+suffix);var mid=Bone(hero.Root,"Mid1"+suffix);
                    var up=(mid.position-wrist.position).normalized;
                    var across=(Bone(hero.Root,"Index1"+suffix).position-Bone(hero.Root,"Pinky1"+suffix).position).normalized;
                    var normal=Vector3.Cross(up,across).normalized;
                    if(Vector3.Dot(normal,Bone(hero.Root,"Mid2"+suffix).position-mid.position)<0)normal=-normal;
                    foreach(string name in new[]{"Index1","Index2","Index3","Mid1","Mid2","Mid3","Thumb1","Thumb2","Thumb3"})
                    {
                        var joint=Bone(hero.Root,name+suffix);var tip=joint.TransformPoint(Vector3.up*(name=="Thumb3"?.025f:.019f));
                        Vector3 Coord(Vector3 point){var d=(point-wrist.position)/wrist.lossyScale.x;return new Vector3(Vector3.Dot(d,up),Vector3.Dot(d,across),Vector3.Dot(d,normal));}
                        metrics.AppendLine($"{name}{suffix} head={Coord(joint.position):F5} tip={Coord(tip):F5}");
                    }
                }
                File.WriteAllText($"{Folder}/metrics-{variant}.txt",metrics.ToString());
                var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
                try
                {
                    CharacterReview.Save(world.Camera,rt,$"{Folder}/explore-{variant}-stage.png");
                    foreach(bool left in new[]{true,false})
                    {
                        var wrist=Bone(hero.Root,left?"HandBase_L":"HandBase_R");var mid=Bone(hero.Root,left?"Mid1_L":"Mid1_R");
                        var center=Vector3.Lerp(wrist.position,mid.position,.65f);
                        var side=Vector3.Cross(Vector3.up,world.BattleAxis);
                        world.Camera.transform.position=center-world.BattleAxis*.42f+Vector3.up*.16f+side*(left?.60f:-.60f);
                        world.Camera.transform.LookAt(center);world.Camera.fieldOfView=28;
                        CharacterReview.Save(world.Camera,rt,$"{Folder}/explore-{variant}-{(left?"left":"right")}.png");
                    }
                }
                finally{world.Camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
            }
            Debug.Log("[TigaFistShapeReview] exploration complete");
        }
    }
}
