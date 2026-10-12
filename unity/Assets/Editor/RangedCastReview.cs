using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class RangedCastReview
    {
        static bool TigaOnly=>Array.IndexOf(Environment.GetCommandLineArgs(),"--tiga-only")>=0;
        public static void Before(){Run("before","Tiga",60,true);if(!TigaOnly)Run("before","Zero",60,true);}
        public static void Release(){After();RangedCastTransitions.Validate();}
        public static void After()
        {foreach(string id in TigaOnly?new[]{"Tiga"}:new[]{"Tiga","Mebius","Zero","Geed","Grigio"})foreach(int hz in new[]{15,30,60})Run("after",id,hz,hz==60&&(id=="Tiga"||id=="Zero"));}
        static void Run(string version,string id,int hz,bool movie)
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--ranged-cast-output");
            string output=at>=0?args[at+1]:"../artifacts/ranged-cast-20261009";
            string folder=Path.GetFullPath(Path.Combine(output,version,id+"-"+hz));Directory.CreateDirectory(folder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(1053);
            var world=new GameWorld();var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);
            if(!hero.IsRigged||!enemy.IsRigged)throw new Exception("Actual model missing");world.BindActors(hero,enemy);world.SetHeroProfile(id);
            var b=new Battle();b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});b.GiveInstructionTime(20);while(b.TryCue(out _)){}
            var rt=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
            for(int f=0;f<40;f++){hero.Update(b,world.Camera,1/60f,f/60f);enemy.Update(b,world.Camera,1/60f,f/60f);world.Tick(b,1/60f,f/60f);}
            if(movie)Directory.CreateDirectory(folder+"/frames");
            var csv=new StringBuilder("frame,action,age,shotAge,punches,health,energy,sequence,visible\n");float dt=1f/hz,health=50;int hits=0;bool flight=false,back=false,launchLight=false,impactLight=false;
            Transform FindBone(string a,string b){foreach(var bone in hero.Root.GetComponentsInChildren<Transform>())if(bone.name==a||bone.name==b)return bone;throw new Exception("Missing bone "+a);}
            var hip=FindBone("hip","bip_pelvis");var leftUpper=FindBone("armBase_L","bip_upperArm_L");var rightUpper=FindBone("armBase_R","bip_upperArm_R");var head=FindBone("head","bip_head");
            var leftLower=FindBone("ForearmBase_L","bip_lowerArm_L");var rightLower=FindBone("ForearmBase_R","bip_lowerArm_R");
            var extension=new StringBuilder("frame,action,age,elbowAngle,reachRatio,chainLength\n");
            var mesh=new Mesh();float ground=100;
            float restHip=hip.position.y,minHip=restHip,maxOtherReach=0,maxSupportRelease=0,minHeadDistance=100,maxStep=0;Vector3 lastLeft=hero.StrikeOrigin(HeroAction.LeftPunch),lastRight=hero.HandPosition;bool launchPose=false;
            var body=new StringBuilder("frame,action,age,hipY,leftX,leftY,leftZ,rightX,rightY,rightZ\n");
            int objects=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
            int[] starts={Mathf.RoundToInt(.4f*hz),2*hz,Mathf.RoundToInt(3.4f*hz)};float[] speeds={.65f,1,1.7f};
            try
            {
                for(int f=0;f<hz*5;f++)
                {
                    var input=new PlayerInput{Tracking=true};for(int i=0;i<3;i++)if(f==starts[i]){input.RangedAttack=true;input.LeftPunch=i==1;input.RightPunch=i!=1;input.AttackSpeed=speeds[i];}
                    b.Tick(dt,input);while(b.TryCue(out var cue))world.Cue(cue,b);
                    hero.Update(b,world.Camera,dt,f*dt);enemy.Update(b,world.Camera,dt,f*dt);
                    if(b.EnemyHealth<health){hits++;world.Hit(false,b);}health=b.EnemyHealth;
                    world.Tick(b,dt,f*dt);
                    var left=hero.StrikeOrigin(HeroAction.LeftPunch);var right=hero.HandPosition;
                    maxStep=Mathf.Max(maxStep,Vector3.Distance(left,lastLeft),Vector3.Distance(right,lastRight));lastLeft=left;lastRight=right;
                    body.AppendLine(FormattableString.Invariant($"{f},{b.Action},{b.ActionAge:F5},{hip.position.y:F5},{left.x:F5},{left.y:F5},{left.z:F5},{right.x:F5},{right.y:F5},{right.z:F5}"));
                    if(b.IsRangedPunch)
                    {
                        bool activeLeft=b.Action==HeroAction.LeftPunch;var shoulder=(activeLeft?leftUpper:rightUpper).position;var elbow=(activeLeft?leftLower:rightLower).position;var wrist=activeLeft?left:right;
                        float length=Vector3.Distance(shoulder,elbow)+Vector3.Distance(elbow,wrist);
                        extension.AppendLine(FormattableString.Invariant($"{f},{b.Action},{b.ActionAge:R},{Vector3.Angle(shoulder-elbow,wrist-elbow):R},{Vector3.Distance(shoulder,wrist)/length:R},{length:R}"));
                        if(version=="after"&&id=="Tiga"&&b.ActionAge>=.13f&&b.ActionAge<=.22f&&Vector3.Angle(shoulder-elbow,wrist-elbow)<145)
                            throw new Exception("Tiga release arm remains folded");
                        if(version=="after"&&id=="Tiga"&&Vector3.Angle(shoulder-elbow,wrist-elbow)>177)
                            throw new Exception("Tiga elbow locks at full extension");
                        if(f%3==0)foreach(var skin in hero.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {skin.BakeMesh(mesh,true);foreach(var vertex in mesh.vertices)ground=Mathf.Min(ground,skin.transform.TransformPoint(vertex).y);}
                        minHip=Mathf.Min(minHip,hip.position.y);bool isLeft=b.Action==HeroAction.LeftPunch;
                        float support=Vector3.Dot((isLeft?right:left)-(isLeft?rightUpper:leftUpper).position,world.BattleAxis);
                        maxOtherReach=Mathf.Max(maxOtherReach,support);
                        // The current ready stance deliberately opens both
                        // arms at the end of recovery. Test the support hand
                        // during release, not the already-restored idle pose.
                        if(b.ActionAge>=.10f&&b.ActionAge<=.21f)maxSupportRelease=Mathf.Max(maxSupportRelease,support);
                        if(b.ActionAge>=.045f&&b.ActionAge<=.08f)minHeadDistance=Mathf.Min(minHeadDistance,Vector3.Distance(isLeft?left:right,head.position));
                        if(!launchPose&&b.ActionAge>=.12f&&b.ActionAge<=.15f){launchPose=true;CharacterReview.Save(world.Camera,rt,folder+"/release-pose.png");}
                    }
                    if(version=="after")
                    {
                        launchLight|=world.Projectile.LaunchVisible;impactLight|=world.Projectile.ImpactVisible;
                        if(world.Projectile.Impacts!=hits)throw new Exception("Impact count differs from actual damage");
                        if(id=="Zero"&&world.Projectile.Visible)
                            for(int i=0;i<2;i++)if(Vector3.Distance(world.Projectile.BladePosition(i),world.Projectile.BladeTrailTip(i))>.0001f)throw new Exception("Detached blade wake");
                        if(b.Shot.Sequence==1&&world.Projectile.LaunchVisible&&b.ActionAge<.1f&&hz==60)CharacterReview.Save(world.Camera,rt,folder+"/launch.png");
                        if(b.Punches==1&&world.Projectile.ImpactVisible&&b.Shot.Age>.34f&&b.Shot.Age<.34f+dt*.66f)CharacterReview.Save(world.Camera,rt,folder+"/impact-volume.png");
                    }
                    if(b.Shot.Sequence==1&&world.Projectile.Visible)
                    {
                        if(!flight&&b.Shot.Age>.22f&&b.Shot.Age<.30f){flight=true;CharacterReview.Save(world.Camera,rt,folder+"/flight.png");}
                        if(!back&&b.Shot.Age>.35f&&id=="Zero"){back=true;CharacterReview.Save(world.Camera,rt,folder+"/return.png");}
                    }
                    if(b.Punches==1&&b.Shot.Age>.30f&&b.Shot.Age<.30f+dt*.66f)CharacterReview.Save(world.Camera,rt,folder+"/contact.png");
                    if(movie&&f%2==0)SaveMovie(world.Camera,rt,folder+"/frames/"+(f/2).ToString("D4")+".jpg");
                    csv.AppendLine(FormattableString.Invariant($"{f},{b.Action},{b.ActionAge:F5},{b.Shot.Age:F5},{b.Punches},{b.EnemyHealth},{b.Energy},{b.Shot.Sequence},{world.Projectile.Visible}"));
                }
                if(b.Punches!=3||hits!=3||world.Projectile.Launches!=3||!flight||id=="Zero"&&!back)throw new Exception("Missing shot lifecycle "+id+"-"+hz);
                if(version=="after"&&(!launchLight||!impactLight||world.Projectile.LaunchVisible||world.Projectile.ImpactVisible))throw new Exception("Missing launch/impact or effect survived its lifetime");
                if(UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length!=objects)throw new Exception("Per-shot objects allocated");
                b.Pause();world.Tick(b,0,5);if(world.Projectile.Visible||world.Projectile.ImpactVisible||world.Projectile.LaunchVisible)throw new Exception("Pause left projectile visible");
                world.ResetPresentation();
                File.WriteAllText(folder+"/extension.csv",extension.ToString());File.WriteAllText(folder+"/trace.csv",csv.ToString());File.WriteAllText(folder+"/body.csv",body.ToString());
                string pose=$"hipDrop={restHip-minHip:F4} supportReach={maxOtherReach:F4} supportAtRelease={maxSupportRelease:F4} headReach={minHeadDistance:F4} maxHandStep={maxStep:F4} ground={ground:F4}";
                File.WriteAllText(folder+"/pose.txt",pose);Debug.Log("[RangedCastPose] "+id+"-"+hz+" "+pose);
                // Tiga now protects ahead of the shoulder instead of folding
                // the unused fist into the neck; it must still stay well short
                // of the ~1.05-unit emitting arm and keep its planted body.
                if(version=="after"&&(restHip-minHip>.25f||maxSupportRelease>(id=="Tiga"?.75f:.55f)||ground<-.05f||hz==60&&maxStep>.65f))throw new Exception("Invalid ranged body/support: "+pose);
                if(version=="after"&&id=="Zero"&&hz==60&&minHeadDistance>.45f)throw new Exception("Missing head-slugger preparation");
                string result=$"{id}-{hz} hits={hits} launches={world.Projectile.Launches} impacts={world.Projectile.Impacts} health={b.EnemyHealth} energy={b.Energy} flight={flight} return={back} launchVolume={launchLight} impactVolume={impactLight} pool=stable pause=pass";
                File.WriteAllText(folder+"/validation.txt",result);Debug.Log("[RangedCast] "+result);
            }
            finally{world.Camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(mesh);}
            var sources=new StringBuilder();foreach(string dir in new[]{"Scripts","Resources"})foreach(string file in Directory.GetFiles(Application.dataPath+"/"+dir,"*",SearchOption.AllDirectories))
            {if(!file.EndsWith(".cs")&&!file.EndsWith(".shader"))continue;using(var sha=System.Security.Cryptography.SHA256.Create())sources.AppendLine(file.Substring(Application.dataPath.Length+1)+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file))).Replace("-","").ToLowerInvariant());}
            File.WriteAllText(folder+"/sources.txt",sources.ToString());
        }
        static void SaveMovie(Camera camera,RenderTexture rt,string path)
        {
            // Offline batch rendering has no player loop to refresh skinning.
            // Bake the sampled pose on every movie frame, as for still images.
            CharacterReview.Save(camera,rt,path);
        }
    }
}
