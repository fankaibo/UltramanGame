using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class EntranceReview
    {
        public static void Before()=>Render("before","Tiga",true);
        public static void After(){for(int i=0;i<HeroRoster.Count;i++)Render("after",HeroRoster.At(i).Id,i==0);PauseAndRestart();}
        static float Coverage(Camera camera)
        {
            var backdrop=GameObject.Find("Realistic Mount Fuji night backdrop").transform;
            var plane=new Plane(backdrop.forward,backdrop.position);float max=0;
            for(int x=0;x<2;x++)for(int y=0;y<2;y++)
            {var ray=camera.ViewportPointToRay(new Vector3(x,y,0));if(!plane.Raycast(ray,out float d))throw new Exception("Backdrop behind intro camera");var p=backdrop.InverseTransformPoint(ray.GetPoint(d));max=Mathf.Max(max,Mathf.Abs(p.x),Mathf.Abs(p.y));}
            return max;
        }
        static void PauseAndRestart()
        {
            foreach(float pauseAt in new[]{.55f,1.5f,1.9f})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var world=new GameWorld();var state=new Battle();var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);
                var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
                while(state.TransformationAge<pauseAt)
                {state.Tick(.02f,new PlayerInput{Tracking=true});hero.Update(state,world.Camera,.02f,0);enemy.Update(state,world.Camera,.02f,0);world.Tick(state,.02f,0);}
                float age=state.TransformationAge;state.Pause();world.Tick(state,.02f,0);
                if(world.TransformationCloseup||GameObject.Find("Light transformation veil")||GameObject.Find("Transformation radiance").GetComponent<Light>().intensity!=0)throw new Exception("Paused transformation still visible");
                while(state.Phase==GamePhase.Paused)state.Tick(.02f,new PlayerInput{Tracking=true});
                world.Tick(state,.02f,0);
                if(Mathf.Abs(state.TransformationAge-age)>.001f||Mathf.Abs(world.EntranceAge-age)>.001f)throw new Exception("Transformation restarted after pause");
                for(int f=0;f<120;f++){state.Tick(.02f,new PlayerInput{Tracking=true});world.Tick(state,.02f,0);}
                if(state.Phase!=GamePhase.Battle||world.TransformationCloseup||GameObject.Find("Light transformation veil"))throw new Exception("Transformation effect leaked into battle");
                world.ResetPresentation();world.Tick(new Battle(),.02f,0);
                if(world.EntranceAge!=0||world.TransformationCloseup||world.EnemyOpacity!=1)throw new Exception("Transformation survives restart");
                Debug.Log($"[EntranceInterruption] pauseAt={pauseAt:F2} same-clock-resume and reset passed");
            }
        }
        static void Render(string version,string id,bool film)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(2709);
            var world=new GameWorld();var state=new Battle();
            var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);
            var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/entrance",version,id));Directory.CreateDirectory(folder+"/frames");File.Delete(folder+"/validation.txt");
            var source=new StringBuilder("UTC: "+DateTime.UtcNow.ToString("O")+"\n");
            using(var sha=System.Security.Cryptography.SHA256.Create())
                foreach(string file in new[]{"Scripts/Runtime/RiggedActor.cs","Scripts/Runtime/GameWorld.cs","Scripts/Runtime/ArcadeStageFx.cs","Resources/TransformationVeil.shader","Scripts/Core/Battle.cs","Scripts/Core/TransformationMotion.cs","Editor/EntranceReview.cs"})
                {string p=Path.Combine(Application.dataPath,file);if(File.Exists(p))source.AppendLine(file+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant());}
            File.WriteAllText(folder+"/render-source.txt",source.ToString());
            var target=new RenderTexture(1280,720,24){antiAliasing=4};target.Create();world.Camera.targetTexture=target;world.Camera.aspect=16f/9;
            float age=-1,minY=1,maxY=0,backdropEdge=0;int starts=0,closeupFrames=0;var mesh=new Mesh();var moments=new[]{.35f,.9f,1.45f,1.95f,2.4f};var saved=new bool[moments.Length];
            try
            {
                for(int frame=0;frame<264;frame++)
                {
                    const float dt=1/60f;float time=frame*dt;
                    state.Tick(dt,new PlayerInput{Tracking=true,Transform=frame==12});
                    while(state.TryCue(out var cue)){world.Cue(cue,state);if(cue==GameCue.Transform)age=0;if(cue==GameCue.BattleStart)starts++;}
                    hero.Update(state,world.Camera,dt,time);enemy.Update(state,world.Camera,dt,time);world.Tick(state,dt,time);enemy.SetPresentationOpacity(world.EnemyOpacity);
                    if(version=="after")
                    {
                        backdropEdge=Mathf.Max(backdropEdge,Coverage(world.Camera));
                        if(world.TransformationCloseup)
                        {closeupFrames++;foreach(var r in enemy.Root.GetComponentsInChildren<Renderer>())if(r.enabled)throw new Exception("Monster overlaps transformation shot");}
                        if(state.Phase==GamePhase.Battle&&(world.TransformationCloseup||world.EnemyOpacity!=1||GameObject.Find("Light transformation veil")))throw new Exception("Entrance cleanup failed");
                    }
                    if(age>=0&&age<2.2f&&frame%3==0)
                        foreach(var skin in hero.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {skin.BakeMesh(mesh,true);foreach(var vertex in mesh.vertices){var p=world.Camera.WorldToViewportPoint(skin.transform.TransformPoint(vertex));minY=Mathf.Min(minY,p.y);maxY=Mathf.Max(maxY,p.y);}}
                    if(film&&frame%2==0)CharacterReview.Save(world.Camera,target,$"{folder}/frames/{frame/2:D4}.png");
                    for(int i=0;i<moments.Length;i++)if(!saved[i]&&age>=moments[i]){saved[i]=true;CharacterReview.Save(world.Camera,target,$"{folder}/stage-{i}.png");}
                    if(age>=0)age+=dt;
                }
                string result=$"{version}/{id}: starts={starts} phase={state.Phase} health={state.EnemyHealth} punches={state.Punches} viewportY=({minY:F3},{maxY:F3}) backdropEdge={backdropEdge:F3} closeupFrames={closeupFrames} frames={(film?132:0)}";
                Debug.Log("[EntranceReview] "+result);if(starts!=1||state.Phase!=GamePhase.Battle||state.EnemyHealth!=50||state.Punches!=0)throw new Exception(result);
                if(version=="after"&&(minY<.03f||maxY>.95f||backdropEdge>.49f||closeupFrames<60))throw new Exception("Entrance framing failed: "+result);
                File.WriteAllText(folder+"/validation.txt",result);
            }
            finally{world.Camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(mesh);}
        }
    }
}
