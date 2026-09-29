using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class ClawReactionReview
    {
        public static void Before()=>Render("before",60,true);
        public static void After()
        {Render("after",60,true);Render("after",15,false);Render("after",30,false);}
        static Transform Bone(Transform root,string name)
        {foreach(var b in root.GetComponentsInChildren<Transform>())if(b.name==name)return b;throw new Exception(name);}
        static Battle Ready(string mode)
        {
            var b=new Battle(80);b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
            for(int i=0;i<140;i++)b.Tick(.02f,new PlayerInput{Tracking=true});b.GiveInstructionTime(50);
            int count=mode=="beam"?15:mode=="uppercut"?9:0;
            for(int n=0;n<count;n++){b.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});for(int i=0;i<25;i++)b.Tick(.02f,new PlayerInput{Tracking=true});}
            return b;
        }
        static void Render(string version,int rate,bool movie)
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/claw-reaction/"+version));Directory.CreateDirectory(folder+"/frames");
            var report=new StringBuilder();int image=0;
            foreach(string mode in new[]{"left","right","uppercut","beam"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(931);
                var world=new GameWorld();var state=Ready(mode);var hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);
                var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                while(state.TryCue(out _)){}hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,1,0);
                var arms=new Transform[2,3];var fingers=new Transform[2,4];
                for(int s=0;s<2;s++)
                {string side=s==0?"L":"R";arms[s,0]=Bone(enemy.Root,"bip_upperArm_"+side);arms[s,1]=Bone(enemy.Root,"bip_lowerArm_"+side);arms[s,2]=Bone(enemy.Root,"bip_hand_"+side);
                 int f=0;foreach(string name in new[]{"index","middle","ring","pinky"})fingers[s,f++]=Bone(enemy.Root,"bip_"+name+"_0_"+side);}
                var lengths=new float[2,2];for(int s=0;s<2;s++)for(int j=0;j<2;j++)lengths[s,j]=Vector3.Distance(arms[s,j].position,arms[s,j+1].position);
                var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
                var sequence=new StringBuilder("frame,action,age,health,energy,punches\n");
                var motion=new StringBuilder("frame,leftX,leftY,leftZ,rightX,rightY,rightZ,rootX,rootY,rootZ,footLX,footLY,footLZ,footRX,footRY,footRZ,chestX,chestY,chestZ\n");
                float dt=1f/rate,health=state.EnemyHealth,hitAt=-1,maxBend=0,drift=0,lengthError=0,step=0;int contacts=0;
                var oldL=arms[0,2].position;var oldR=arms[1,2].position;
                try
                {
                    for(int frame=0;frame<rate*4;frame++)
                    {
                        float time=frame*dt;bool start=frame==rate/5;
                        state.Tick(world.BattleDelta(dt,state),new PlayerInput{Tracking=true,Beam=start&&mode=="beam",LeftPunch=start&&(mode=="left"||mode=="uppercut"),RightPunch=start&&mode=="right"});
                        while(state.TryCue(out var cue))world.Cue(cue,state);
                        hero.Update(state,world.Camera,dt,time);enemy.Update(state,world.Camera,dt,time);
                        if(state.EnemyHealth<health){hitAt=time;contacts++;world.Hit(mode=="beam",state);}health=state.EnemyHealth;world.Tick(state,dt,time);enemy.SetPresentationOpacity(world.EnemyOpacity);
                        var l=arms[0,2].position;var r=arms[1,2].position;
                        step=Mathf.Max(step,Vector3.Distance(l,oldL),Vector3.Distance(r,oldR));oldL=l;oldR=r;
                        for(int s=0;s<2;s++)
                        {
                            Vector3 center=Vector3.zero;for(int f=0;f<4;f++)center+=fingers[s,f].position;
                            maxBend=Mathf.Max(maxBend,Vector3.Angle(center/4-arms[s,2].position,arms[s,2].position-arms[s,1].position));
                            for(int j=0;j<2;j++)lengthError=Mathf.Max(lengthError,Mathf.Abs(Vector3.Distance(arms[s,j].position,arms[s,j+1].position)-lengths[s,j]));
                        }
                        var root=enemy.Root.position;var fl=enemy.FootPosition(true);var fr=enemy.FootPosition(false);var chest=enemy.BeamSurfaceContact;
                        enemy.Update(state,world.Camera,0,time);
                        drift=Mathf.Max(drift,Vector3.Distance(l,arms[0,2].position),Vector3.Distance(r,arms[1,2].position),Vector3.Distance(root,enemy.Root.position));
                        sequence.AppendLine(FormattableString.Invariant($"{frame},{state.Action},{state.ActionAge:F5},{health},{state.Energy},{state.Punches}"));
                        motion.AppendLine(FormattableString.Invariant($"{frame},{l.x:F5},{l.y:F5},{l.z:F5},{r.x:F5},{r.y:F5},{r.z:F5},{root.x:F5},{root.y:F5},{root.z:F5},{fl.x:F5},{fl.y:F5},{fl.z:F5},{fr.x:F5},{fr.y:F5},{fr.z:F5},{chest.x:F5},{chest.y:F5},{chest.z:F5}"));
                        if(movie&&frame%2==0)CharacterReview.Save(world.Camera,rt,$"{folder}/frames/{image++:D4}.png");
                        if(hitAt>=0&&time-hitAt>(mode=="beam"?.35f:.15f)&&time-hitAt<(mode=="beam"?.35f:.15f)+dt*1.01f)
                            CharacterReview.Save(world.Camera,rt,$"{folder}/{mode}-{rate}-impact.png");
                    }
                    string line=$"{mode}/{rate} contacts={contacts} bend={maxBend:F4} zeroTime={drift:F6} lengthError={lengthError:F6} maxStep={step:F4}";
                    Debug.Log("[ClawReactionReview] "+line);report.AppendLine(line);
                    File.WriteAllText($"{folder}/{mode}-{rate}-sequence.csv",sequence.ToString());File.WriteAllText($"{folder}/{mode}-{rate}-motion.csv",motion.ToString());
                    if(contacts!=1||maxBend>35.1f||drift>.001f||lengthError>.001f)throw new Exception(line);
                }
                finally{world.Camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
            }
            File.WriteAllText($"{folder}/{rate}-validation.txt",report.ToString());
            if(movie)
            {
                var hashes=new StringBuilder();using(var sha=System.Security.Cryptography.SHA256.Create())
                foreach(string file in new[]{"Scripts/Runtime/RiggedActor.cs","Scripts/Runtime/GameWorld.cs","Scripts/Core/MonsterClawMotion.cs"})
                {string path=Path.Combine(Application.dataPath,file);if(File.Exists(path))hashes.AppendLine(file+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant());}
                File.WriteAllText(folder+"/sources.txt",hashes.ToString());
            }
        }
    }
}
