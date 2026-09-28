using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class PlayfieldReview
    {
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/playfield"));
        static readonly System.Collections.Generic.List<string> failures=new System.Collections.Generic.List<string>();
        public static void Release(){After();ComboCameraReview.Flow();EnemyExchangeReview.Flow();ThreatCameraReview.Flow();}
        public static void Before(){foreach(string mode in new[]{"ordinary","combo","uppercut","beam"})Run("before","Tiga",30,mode,true);}
        public static void After()
        {
            failures.Clear();
            foreach(string id in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})foreach(int rate in id=="Tiga"?new[]{15,30,60}:new[]{60})
                foreach(string mode in new[]{"ordinary","combo","uppercut","beam"})Run("after",id,rate,mode,id=="Tiga"&&rate==30);
            if(failures.Count>0)throw new Exception("Playfield occlusion or scale failed:\n"+string.Join("\n",failures));
        }
        static Battle Ready(string mode)
        {
            var state=new Battle(80);state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
            for(int i=0;i<140;i++)state.Tick(.02f,new PlayerInput{Tracking=true});state.GiveInstructionTime(30);
            int count=mode=="combo"?4:mode=="uppercut"?9:mode=="beam"?15:0;
            for(int n=0;n<count;n++){state.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});for(int i=0;i<25;i++)state.Tick(.02f,new PlayerInput{Tracking=true});}
            while(state.TryCue(out _)){}return state;
        }
        static void Run(string version,string id,int rate,string mode,bool film)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(951);
            var world=new GameWorld();var state=Ready(mode);var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);
            var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            string folder=$"{Folder}/{version}/{id}-{rate}-{mode}";Directory.CreateDirectory(folder+"/frames");File.Delete(folder+"/validation.txt");
            var target=new RenderTexture(1280,720,24){antiAliasing=4};target.Create();world.Camera.targetTexture=target;world.Camera.aspect=16f/9;
            hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,1,0);
            var mesh=new Mesh();float dt=1f/rate,health=state.EnemyHealth,maxSpeed=0,minY=1,maxY=0,heroHeight=1,edge=0;
            int header=0,rail=0,preview=0,clipped=0,contacts=0;var csv=new StringBuilder("frame,action,age,enemy,enemyAge,health,energy,punches\n");
            Rect guide=version=="before"?new Rect(370,638,540,65):GuideRect;
            Rect beam=version=="before"?new Rect(406,630,468,42):BeamRect;
            try
            {
                for(int f=0;f<rate*5;f++)
                {
                    bool punch=mode!="beam"&&(f==rate/5||f==rate/5+Mathf.CeilToInt(rate*(mode=="ordinary"?1:.45f)));
                    float oldHealth=state.EnemyHealth;var oldPosition=world.Camera.transform.position;
                    state.Tick(world.BattleDelta(dt,state),new PlayerInput{Tracking=true,LeftPunch=punch&&f==rate/5,RightPunch=punch&&f!=rate/5,Beam=mode=="beam"&&f==rate/5});
                    while(state.TryCue(out var cue))world.Cue(cue,state);
                    hero.Update(state,world.Camera,dt,f*dt);enemy.Update(state,world.Camera,dt,f*dt);
                    if(state.EnemyHealth<oldHealth){contacts++;world.Hit(mode=="beam",state);}health=state.EnemyHealth;
                    world.Tick(state,dt,f*dt);
                    bool cutin=world.Closeup.Active;
                    if(!cutin)
                    {
                        if(mode!="beam")maxSpeed=Mathf.Max(maxSpeed,Vector3.Distance(oldPosition,world.Camera.transform.position)/dt);
                        var backdrop=GameObject.Find("Realistic Mount Fuji night backdrop").transform;var plane=new Plane(backdrop.forward,backdrop.position);
                        for(int x=0;x<2;x++)for(int y=0;y<2;y++)
                        {var ray=world.Camera.ViewportPointToRay(new Vector3(x,y,0));if(!plane.Raycast(ray,out float distance))throw new Exception("Backdrop behind camera");var p=backdrop.InverseTransformPoint(ray.GetPoint(distance));edge=Mathf.Max(edge,Mathf.Abs(p.x),Mathf.Abs(p.y));}
                        foreach(var actor in new[]{hero,enemy})
                        {
                            float bottom=1,top=0;
                            foreach(var skin in actor.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                            {
                                skin.BakeMesh(mesh,true);foreach(var v in mesh.vertices)
                                {
                                    var p=world.Camera.WorldToViewportPoint(skin.transform.TransformPoint(v));
                                    bottom=Mathf.Min(bottom,p.y);top=Mathf.Max(top,p.y);minY=Mathf.Min(minY,p.y);maxY=Mathf.Max(maxY,p.y);
                                    var screen=new Vector2(p.x*1280,(1-p.y)*720);
                                    if(screen.y<100&&(screen.x>=24&&screen.x<=448||screen.x>=832&&screen.x<=1256))header++;
                                    if((state.Action==HeroAction.Beam?beam:guide).Contains(screen))rail++;
                                    if(new Rect(1096,548,164,157).Contains(screen))preview++;
                                    if(p.x<0||p.x>1||p.y<0||p.y>1)clipped++;
                                }
                            }
                            if(actor==hero&&state.Action==HeroAction.None&&world.ComboFocus<.01f&&enemy.LaunchAge>=10)heroHeight=Mathf.Min(heroHeight,top-bottom);
                        }
                    }
                    var position=world.Camera.transform.position;var rotation=world.Camera.transform.rotation;float lens=world.Camera.fieldOfView;
                    world.Tick(state,0,f*dt);
                    if(Vector3.Distance(position,world.Camera.transform.position)>.0001f||Quaternion.Angle(rotation,world.Camera.transform.rotation)>.05f||Mathf.Abs(lens-world.Camera.fieldOfView)>.001f)
                        throw new Exception("Repeated frame changed camera");
                    if(film)CharacterReview.Save(world.Camera,target,$"{folder}/frames/{f:D4}.png");
                    if(f==rate*2)CharacterReview.Save(world.Camera,target,folder+"/return.png");
                    csv.AppendLine(FormattableString.Invariant($"{f},{state.Action},{state.ActionAge:F5},{state.Enemy},{state.EnemyAge:F5},{health},{state.Energy},{state.Punches}"));
                }
                string line=$"{id}/{rate}/{mode} header={header} rail={rail} preview={preview} clipped={clipped} height={heroHeight:F4} y={minY:F4}..{maxY:F4} backdrop={edge:F4} speed={maxSpeed:F3} contacts={contacts}";
                Debug.Log("[PlayfieldReview] "+version+" "+line);
                File.WriteAllText(folder+"/sequence.csv",csv.ToString());
                if(contacts!=(mode=="beam"?1:2))throw new Exception("Incomplete input sequence");
                if(version=="after"&&(header>0||rail>0||preview>0||clipped>0||heroHeight<.62f||edge>.49f||maxSpeed>14)){failures.Add(line);return;}
                File.WriteAllText(folder+"/validation.txt",line+" passed");
                var sources=new StringBuilder();using(var sha=System.Security.Cryptography.SHA256.Create())
                    foreach(string file in new[]{"Scripts/Runtime/GameWorld.cs","Scripts/Runtime/ArcadeHud.cs","Scripts/Runtime/RiggedActor.cs","Scripts/Runtime/BattleHudLayout.cs","Scripts/Core/Battle.cs",$"Resources/Characters/{id}/{id}.fbx","Resources/Characters/Golza/Golza.fbx","Editor/PlayfieldReview.cs"})
                        if(File.Exists(Path.Combine(Application.dataPath,file)))sources.AppendLine(file+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Application.dataPath,file)))).Replace("-","").ToLowerInvariant());
                File.WriteAllText(folder+"/sources.txt",sources.ToString());
            }
            finally{world.Camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(mesh);}
        }
        static Rect GuideRect=>BattleHudLayout.Guide;
        static Rect BeamRect=>BattleHudLayout.BeamTitle;
    }
}
