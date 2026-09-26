using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class VictoryReview
    {
        public static void Before()=>Render("before");
        public static void After(){Render("after");ValidateRoster();}
        public static void Diagnose()=>Render("diagnostics",false);
        static Transform Bone(Transform root,string one,string other)
        {foreach(var b in root.GetComponentsInChildren<Transform>())if(b.name==one||b.name==other)return b;throw new Exception("Missing foot");}
        static float BackdropCoverage(Camera camera)
        {
            var backdrop=GameObject.Find("Realistic Mount Fuji night backdrop").transform;
            var plane=new Plane(backdrop.forward,backdrop.position);float edge=0;
            for(int x=0;x<2;x++)for(int y=0;y<2;y++)
            {
                var ray=camera.ViewportPointToRay(new Vector3(x,y,0));
                if(!plane.Raycast(ray,out float distance))throw new Exception("Backdrop behind camera");
                var local=backdrop.InverseTransformPoint(ray.GetPoint(distance));
                edge=Mathf.Max(edge,Mathf.Abs(local.x),Mathf.Abs(local.y));
            }
            return edge;
        }
        static void ValidateRoster()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/victory-staging/after"));
            var report=new StringBuilder();
            for(int id=0;id<HeroRoster.Count;id++)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                string name=HeroRoster.At(id).Id;var world=new GameWorld();var state=new Battle(10);
                var hero=new AnimatedActor(name,world.HeroHome,world.EnemyHome);
                var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});for(int i=0;i<120;i++)state.Tick(.02f,new PlayerInput{Tracking=true});
                for(int hit=0;hit<10;hit++)
                {state.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});for(int i=0;i<22;i++)state.Tick(.02f,new PlayerInput{Tracking=true});}
                while(state.TryCue(out _)){}world.ResetPresentation();hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,0,0);
                var left=Bone(hero.Root,"Foot_L","bip_foot_L");var right=Bone(hero.Root,"Foot_R","bip_foot_R");
                Vector3 plantedLeft=left.position,plantedRight=right.position;bool switched=false;
                float ground=100,minY=1,maxY=0,footDrift=0,backdropEdge=0;int landings=0;var mesh=new Mesh();
                var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
                try
                {
                    for(int f=0;f<300;f++)
                    {
                        const float dt=1/60f;float age=(f+1)*dt;
                        hero.Update(state,world.Camera,dt,age);enemy.Update(state,world.Camera,dt,age);world.Tick(state,dt,age);
                        backdropEdge=Mathf.Max(backdropEdge,BackdropCoverage(world.Camera));
                        if(world.MonsterLanded)landings++;
                        float turn=VictoryMotion.Turn(age);
                        if(turn>0&&turn<.5f)footDrift=Mathf.Max(footDrift,Vector3.Distance(left.position,plantedLeft));
                        if(turn>=.5f)
                        {if(!switched){switched=true;plantedRight=right.position;}else footDrift=Mathf.Max(footDrift,Vector3.Distance(right.position,plantedRight));}
                        if(f%6==0)
                            foreach(var skin in hero.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                            {skin.BakeMesh(mesh,true);foreach(var v in mesh.vertices){var p=skin.transform.TransformPoint(v);var s=world.Camera.WorldToViewportPoint(p);ground=Mathf.Min(ground,p.y);minY=Mathf.Min(minY,s.y);maxY=Mathf.Max(maxY,s.y);}}
                    }
                    float face=Vector3.Dot(hero.Root.forward,Vector3.ProjectOnPlane(world.Camera.transform.position-hero.Root.position,Vector3.up).normalized);
                    CharacterReview.Save(world.Camera,rt,folder+"/"+name+"-front.png");
                    string result=$"{name}: ground={ground:F4} viewportY=({minY:F3},{maxY:F3}) supportFootDrift={footDrift:F4} facing={face:F3} landings={landings} backdropEdge={backdropEdge:F3}";
                    Debug.Log("[VictoryRoster] "+result);report.AppendLine(result);
                    if(ground<-.035f||minY<.065f||maxY>.915f||footDrift>.035f||face<.90f||landings!=1||backdropEdge>.49f)throw new Exception("Victory pose failed: "+result);
                    foreach(var renderer in enemy.Root.GetComponentsInChildren<Renderer>())if(renderer.enabled)throw new Exception("Defeated monster still visible before photo");
                    world.ResetPresentation();var waiting=new Battle();hero.Update(waiting,world.Camera,0,0);enemy.Update(waiting,world.Camera,0,0);world.Tick(waiting,.02f,0);
                    if(world.MonsterLanded||Vector3.Distance(hero.Root.position,world.HeroHome)>.001f||Vector3.Dot(hero.Root.forward,world.BattleAxis)<.999f)
                        throw new Exception("Victory pose leaks into next round");
                }
                finally{world.Camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(mesh);}
            }
            File.WriteAllText(folder+"/roster-validation.txt",report.ToString());
        }
        static void Render(string version,bool images=true)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(260926);
            var world=new GameWorld();var state=new Battle(10);
            var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);
            var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});for(int i=0;i<120;i++)state.Tick(.02f,new PlayerInput{Tracking=true});
            for(int hit=0;hit<9;hit++)
            {state.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});for(int i=0;i<22;i++)state.Tick(.02f,new PlayerInput{Tracking=true});}
            state.GiveInstructionTime(15);while(state.TryCue(out _)){}
            hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,1,0);
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/victory-staging",version));
            Directory.CreateDirectory(folder+"/frames");File.Delete(folder+"/validation.txt");
            var source=new StringBuilder("UTC: "+DateTime.UtcNow.ToString("O")+"\nUnity: "+Application.unityVersion+"\n");
            using(var sha=System.Security.Cryptography.SHA256.Create())
                foreach(string file in new[]{"Scripts/Runtime/RiggedActor.cs","Scripts/Runtime/GameWorld.cs","Scripts/Core/VictoryMotion.cs","Editor/VictoryReview.cs"})
                {string p=Path.Combine(Application.dataPath,file);if(File.Exists(p))source.AppendLine(file+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant());}
            File.WriteAllText(folder+"/render-source.txt",source.ToString());
            var rt=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
            var csv=new StringBuilder("frame,age,minGround,maxGround,heroFacing,monsterX,monsterY,monsterZ\n");var mesh=new Mesh();
            int victories=0,landings=0;float age=-1,health=state.EnemyHealth,minGround=100,backdropEdge=0;var markers=new bool[5];
            string lowestBone="";Vector3 lowestPoint=Vector3.zero;
            float[] moments={.25f,.9f,1.7f,2.8f,4.4f};string[] names={"stagger","land","settle","fade","hero"};
            try
            {
                for(int f=0;f<390;f++)
                {
                    const float dt=1/60f;float t=f*dt;
                    state.Tick(world.BattleDelta(dt,state),new PlayerInput{Tracking=true,RightPunch=f==12});
                    while(state.TryCue(out var cue)){world.Cue(cue,state);if(cue==GameCue.Victory){victories++;age=0;}}
                    if(age>=0)age+=dt;
                    hero.Update(state,world.Camera,dt,t);enemy.Update(state,world.Camera,dt,t);
                    if(state.EnemyHealth<health)world.Hit(false,state);health=state.EnemyHealth;
                    world.Tick(state,dt,t);
                    backdropEdge=Mathf.Max(backdropEdge,BackdropCoverage(world.Camera));
                    if(world.MonsterLanded)landings++;
                    if(age>=0&&f%3==0)
                    {
                        float lo=100,hi=-100;
                        foreach(var skin in enemy.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {skin.BakeMesh(mesh,true);foreach(var vertex in mesh.vertices){var point=skin.transform.TransformPoint(vertex);float y=point.y;lo=Mathf.Min(lo,y);hi=Mathf.Max(hi,y);
                            if(age<2.8f&&y<minGround){minGround=y;lowestPoint=point;float distance=100;
                                foreach(var bone in skin.bones){float d=Vector3.Distance(point,bone.position);if(d<distance){distance=d;lowestBone=bone.name;}}}
                        }}
                        if(age<2.8f)minGround=Mathf.Min(minGround,lo);
                        float facing=Vector3.Dot(hero.Root.forward,Vector3.ProjectOnPlane(world.Camera.transform.position-hero.Root.position,Vector3.up).normalized);
                        var p=enemy.Root.position;csv.AppendLine(FormattableString.Invariant($"{f},{age:F4},{lo:F4},{hi:F4},{facing:F4},{p.x:F4},{p.y:F4},{p.z:F4}"));
                    }
                    if(images&&f%2==0)CharacterReview.Save(world.Camera,rt,$"{folder}/frames/{f/2:D4}.png");
                    for(int i=0;i<moments.Length;i++)if(!markers[i]&&age>=moments[i])
                    {markers[i]=true;if(images)CharacterReview.Save(world.Camera,rt,folder+"/"+names[i]+".png");}
                }
                if(victories!=1||state.EnemyHealth!=0||!Array.TrueForAll(markers,v=>v))throw new Exception("Incomplete victory sequence");
                if(version=="after"&&(minGround<-.035f||landings!=1||backdropEdge>.49f))throw new Exception($"Monster landing invalid ground={minGround} landings={landings} backdropEdge={backdropEdge}");
                File.WriteAllText(folder+"/motion.csv",csv.ToString());
                string result=$"{version}: victory=1 landings={landings} frames={(images?195:0)} minimumGround={minGround:F4} finishedAge={age:F2} lowestPoint={lowestPoint} nearestBone={lowestBone} backdropEdge={backdropEdge:F3}";
                Debug.Log("[VictoryReview] "+result);File.WriteAllText(folder+"/validation.txt",result);
            }
            finally {world.Camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(mesh);}
        }
    }
}
