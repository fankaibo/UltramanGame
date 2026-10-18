using System;
using System.IO;
using System.Text;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class ContactCameraReview
    {
        static string Folder=>Path.GetFullPath("../artifacts/a170-contact-camera");
        public static void Before()=>Run("before");
        public static void After()=>Run("after");
        public static void Release(){After();ComboCameraReview.Framing();}
        static float Coverage(Camera camera)
        {
            var backdrop=GameObject.Find("Realistic Mount Fuji night backdrop").transform;
            var plane=new Plane(backdrop.forward,backdrop.position);float edge=0;
            for(int x=0;x<2;x++)for(int y=0;y<2;y++)
            {var ray=camera.ViewportPointToRay(new Vector3(x,y));if(!plane.Raycast(ray,out float d))throw new Exception("Background behind lens");var p=backdrop.InverseTransformPoint(ray.GetPoint(d));edge=Mathf.Max(edge,Mathf.Abs(p.x),Mathf.Abs(p.y));}
            return edge;
        }
        static void Run(string version)
        {
            var impact=typeof(GameWorld).GetField("impact",BindingFlags.Instance|BindingFlags.NonPublic);
            var report=new StringBuilder();
            foreach(int hz in new[]{15,30,60})foreach(string mode in new[]{"combo","ranged","guard","hurt"})foreach(bool quiet in new[]{false,true})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(170);
                var w=new GameWorld();var b=new Battle();var h=new AnimatedActor("Tiga",w.HeroHome,w.EnemyHome);
                var e=new AnimatedActor("Golza",w.EnemyHome,w.HeroHome,true);w.BindActors(h,e);w.SetHeroProfile("Tiga");
                b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
                while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});
                if(mode=="combo")for(int n=0;n<4;n++){b.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});for(int j=0;j<30;j++)b.Tick(.02f,new PlayerInput{Tracking=true});}
                if(mode=="guard"||mode=="hurt")
                {while(!(b.Enemy==EnemyPhase.Windup&&b.WarningDuration-b.EnemyAge<.6f))b.Tick(.02f,new PlayerInput{Tracking=true});}
                else b.GiveInstructionTime(20);
                while(b.TryCue(out _)){}float dt=1f/hz,time=0;
                for(int f=0;f<hz/2;f++){h.Update(b,w.Camera,dt,time);e.Update(b,w.Camera,dt,time);w.Tick(b,dt,time);time+=dt;}
                string folder=$"{Folder}/{version}/{mode}-{hz}";Directory.CreateDirectory(folder);
                if(!quiet&&hz==60)Directory.CreateDirectory(folder+"/frames");
                var trace=new StringBuilder("frame,health,energy,action,age,enemy,enemyAge,blocks,hits,x,y,z,rx,ry,rz,rw,fov,contact,combo,exchange\n");
                var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();w.Camera.targetTexture=rt;w.Camera.aspect=16f/9;
                int contacts=0;float speed=0,edge=0;var prior=w.Camera.transform.position;
                try
                {
                    for(int f=0;f<hz*4;f++)
                    {
                        float health=b.EnemyHealth;int receipts=b.Blocks+b.HitsTaken;
                        bool strike=(mode=="combo"||mode=="ranged")&&(f==hz/2||f==hz*3/2||f==hz*5/2);
                        b.Tick(w.BattleDelta(dt,b),new PlayerInput{Tracking=true,Shield=mode=="guard",LeftPunch=strike&&f==hz*3/2,RightPunch=strike&&f!=hz*3/2,RangedAttack=mode=="ranged",AttackSpeed=1.2f});
                        while(b.TryCue(out var cue))w.Cue(cue,b);
                        h.Update(b,w.Camera,dt,time);e.Update(b,w.Camera,dt,time);
                        bool hit=b.EnemyHealth<health;if(hit)w.Hit(false,b);if(hit||b.Blocks+b.HitsTaken>receipts)contacts++;
                        if(quiet)impact.SetValue(w,0f);w.Tick(b,dt,time);time+=dt;
                        var p=w.Camera.transform.position;var q=w.Camera.transform.rotation;
                        speed=Mathf.Max(speed,Vector3.Distance(p,prior)/dt);prior=p;edge=Mathf.Max(edge,Coverage(w.Camera));
                        trace.AppendLine(FormattableString.Invariant($"{f},{b.EnemyHealth},{b.Energy},{b.Action},{b.ActionAge:F6},{b.Enemy},{b.EnemyAge:F6},{b.Blocks},{b.HitsTaken},{p.x:F6},{p.y:F6},{p.z:F6},{q.x:F6},{q.y:F6},{q.z:F6},{q.w:F6},{w.Camera.fieldOfView:F6},{contacts},{w.ComboFocus:F5},{w.ExchangeFocus:F5}"));
                        w.Tick(b,0,time-dt);
                        if(Vector3.Distance(p,w.Camera.transform.position)>.0001f||Quaternion.Angle(q,w.Camera.transform.rotation)>.05f)throw new Exception("Zero-time camera drift");
                        if(hz==60&&!quiet&&f%2==0)CharacterReview.Save(w.Camera,rt,$"{folder}/frames/{f/2:D4}.jpg");
                    }
                    if(contacts!=(mode=="combo"||mode=="ranged"?3:1)||speed>14||edge>.49f)throw new Exception($"Contact camera invalid {mode}/{hz}: contacts={contacts} speed={speed} edge={edge}");
                    File.WriteAllText(folder+(quiet?"/quiet.csv":"/trace.csv"),trace.ToString());
                    report.AppendLine($"{mode}/{hz} quiet={quiet} contacts={contacts} maxSpeed={speed:F3} backdropEdge={edge:F4} zero-time=passed");
                }
                finally{w.Camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
            }
            File.WriteAllText($"{Folder}/{version}/validation.txt",report.ToString());
        }
    }
}
