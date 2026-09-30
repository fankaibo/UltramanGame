using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class GroundingReview
    {
        static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/grounding"));
        public static void Before()=>Capture("before");
        public static void After()=>Capture("after");
        public static void Validate()
        {
            CheckShadowHeight();Capture("after");RosterFootworkReview.After();
            RosterPunchReview.CheckGuardTransitions();KnockdownReview.After();KnockdownReview.CheckInterruptions();VictoryReview.After();
            Debug.Log("[GroundingRelease] soles shadowHeight rapidPunches guard knockdown victory=passed");
        }
        static void CheckShadowHeight()
        {
            Directory.CreateDirectory(Folder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            foreach(string name in new[]{"ContactSilhouette","ContactShadow"})
            {var shader=Resources.Load<Shader>(name);if(!shader||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new Exception("Invalid contact shader "+name);}
            var camera=new GameObject("Main Camera").AddComponent<Camera>();var shadows=camera.gameObject.AddComponent<ContactShadows>();
            var material=new Material(Resources.Load<Shader>("KaijuSurface")){color=Color.white};
            var sole=GameObject.CreatePrimitive(PrimitiveType.Cube);sole.layer=ContactShadows.ActorLayer;sole.transform.localScale=new Vector3(.6f,.2f,.8f);sole.GetComponent<Renderer>().sharedMaterial=material;
            var torso=GameObject.CreatePrimitive(PrimitiveType.Cube);torso.layer=ContactShadows.ActorLayer;torso.transform.position=Vector3.up*2;torso.transform.localScale=Vector3.one;torso.GetComponent<Renderer>().sharedMaterial=material;
            var shadowCamera=GameObject.Find("Actor shadow camera").GetComponent<Camera>();var mask=shadowCamera.targetTexture;var image=new Texture2D(mask.width,mask.height,TextureFormat.RGB24,false);
            Color Read()
            {shadows.RenderNow();RenderTexture.active=mask;image.ReadPixels(new Rect(0,0,mask.width,mask.height),0,0);image.Apply();return image.GetPixelBilinear(.5f,.5f);}
            var report=new StringBuilder();
            try
            {
                sole.transform.position=Vector3.up*.10f;var planted=Read();
                sole.transform.position=Vector3.up*.25f;var raised=Read();
                sole.transform.position=Vector3.up*.55f;var airborne=Read();
                if(planted.r<.95f||planted.g<.95f||raised.r>=planted.r-.1f||raised.r<=.10f||airborne.r>.005f||airborne.g<.95f)
                    throw new Exception($"Contact mask fails height/occlusion: planted={planted} raised={raised} airborne={airborne}");
                sole.transform.position=Vector3.up*.10f;torso.SetActive(false);material.color=new Color(1,1,1,.35f);var faded=Read();
                if(Mathf.Abs(faded.r-.35f)>.03f||Mathf.Abs(faded.g-.35f)>.03f)throw new Exception("Contact ignores presentation opacity");
                material.SetFloat("_DissolveAmount",1);var dissolved=Read();
                if(dissolved.r>.005f||dissolved.g>.005f)throw new Exception("Dissolved actor leaves contact shadow");
                material.SetFloat("_DissolveAmount",0);sole.SetActive(false);var empty=Read();
                if(empty.r>.005f||empty.g>.005f)throw new Exception("Removed actor leaves shadow history");
                report.AppendLine($"GPU planted={planted.r:F3} raised={raised.r:F3} airborne={airborne.r:F3} broadAirborne={airborne.g:F3} faded={faded.r:F3} overheadTorso=passed dissolve=clear removed=clear");
                File.WriteAllText(Folder+"/shadow-height.txt",report.ToString());Debug.Log("[ContactHeight] "+report);
            }
            finally{RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(material);}
        }
        public static void Shadows()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var world=new GameWorld();var hero=new AnimatedActor("Grigio",world.HeroHome,world.EnemyHome);
            var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
            var state=new Battle();hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,1,0);
            var cam=world.Camera;cam.cullingMask&=~(1<<ContactShadows.ActorLayer);
            foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))light.shadows=LightShadows.None;
            var target=new RenderTexture(1280,720,24);target.Create();cam.targetTexture=target;cam.aspect=16f/9;
            string folder=Folder+"/shadow-diagnostic";Directory.CreateDirectory(folder);
            var receiver=GameObject.Find("Transparent actor shadows").GetComponent<Renderer>();
            var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            Color32[] Read(string name)
            {CharacterReview.Save(cam,target,folder+"/"+name+".png");RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();return image.GetPixels32();}
            var on=Read("on");receiver.enabled=false;var off=Read("off");
            var shadow=GameObject.Find("Actor shadow camera").GetComponent<Camera>();var mask=shadow.targetTexture;
            RenderTexture.active=mask;var maskImage=new Texture2D(mask.width,mask.height,TextureFormat.RGB24,false);maskImage.ReadPixels(new Rect(0,0,mask.width,mask.height),0,0);maskImage.Apply();File.WriteAllBytes(folder+"/mask.png",maskImage.EncodeToPNG());
            foreach(bool left in new[]{true,false})
            {
                var point=hero.FootPosition(left);point.y=.006f;var uv=shadow.WorldToViewportPoint(point);var projected=new Vector2(point.x/10+.5f,point.z/10+.5f);
                var s=cam.WorldToViewportPoint(point);int x=(int)(s.x*1280),y=(int)(s.y*720);int delta=0;
                for(int j=-4;j<=4;j++)for(int i=-4;i<=4;i++)
                {int p=(y+j)*1280+x+i;delta+=Math.Abs(on[p].r-off[p].r)+Math.Abs(on[p].g-off[p].g)+Math.Abs(on[p].b-off[p].b);}
                Debug.Log($"[ContactProjection] {left} camera={uv:F3} projected={projected:F3} normal={maskImage.GetPixelBilinear(projected.x,projected.y).r:F3} flipped={maskImage.GetPixelBilinear(projected.x,1-projected.y).r:F3} footPixelDelta={delta/81f:F2}");
            }
            cam.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(maskImage);
        }
        public static void Planes()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var world=new GameWorld();var mesh=new Mesh();
            foreach(string id in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})
            {
                var actor=new AnimatedActor(id,world.HeroHome,world.EnemyHome);
                foreach(bool left in new[]{true,false})
                {
                    var foot=Foot(actor.Root,left);var groups=new Dictionary<string,(float,Vector3)>();
                    foreach(var skin in actor.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        var bones=skin.bones;var w=skin.sharedMesh.boneWeights;var footVertex=new bool[w.Length];
                        float Weight(int i,float value)=>bones[i]==foot||bones[i].IsChildOf(foot)?value:0;
                        for(int i=0;i<w.Length;i++)footVertex[i]=Weight(w[i].boneIndex0,w[i].weight0)+Weight(w[i].boneIndex1,w[i].weight1)+Weight(w[i].boneIndex2,w[i].weight2)+Weight(w[i].boneIndex3,w[i].weight3)>.5f;
                        skin.BakeMesh(mesh,true);var v=mesh.vertices;var indices=mesh.triangles;
                        for(int i=0;i<indices.Length;i+=3)
                        {
                            int a=indices[i],b=indices[i+1],c=indices[i+2];if(!footVertex[a]||!footVertex[b]||!footVertex[c])continue;
                            var p=skin.transform.TransformPoint(v[a]);var q=skin.transform.TransformPoint(v[b]);var r=skin.transform.TransformPoint(v[c]);
                            var n=Vector3.Cross(q-p,r-p);float area=n.magnitude*.5f;n.Normalize();
                            if(n.y>-.35f||(p.y+q.y+r.y)/3>foot.position.y-.015f)continue;
                            var local=actor.Root.InverseTransformDirection(n);string key=$"{Mathf.Round(local.x*5)},{Mathf.Round(local.y*5)},{Mathf.Round(local.z*5)}";
                            groups.TryGetValue(key,out var g);groups[key]=(g.Item1+area,g.Item2+n*area);
                        }
                    }
                    foreach(var p in groups)Debug.Log($"[SolePlane] {id}/{left} area={p.Value.Item1:F5} normal={actor.Root.InverseTransformDirection(p.Value.Item2.normalized):F3} ankle={foot.position.y:F4}");
                }
                UnityEngine.Object.DestroyImmediate(actor.Root.gameObject);
            }
            UnityEngine.Object.DestroyImmediate(mesh);
        }
        static Transform Foot(Transform root,bool left)
        {
            string suffix=left?"L":"R";
            foreach(var t in root.GetComponentsInChildren<Transform>())if(t.name=="Foot_"+suffix||t.name=="bip_foot_"+suffix)return t;
            throw new Exception("Missing foot");
        }
        static float Sole(AnimatedActor actor,bool left,Mesh mesh)
        {
            var foot=Foot(actor.Root,left);float min=100;int count=0;
            foreach(var skin in actor.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var bones=skin.bones;var selected=new bool[bones.Length];
                for(int i=0;i<bones.Length;i++)selected[i]=bones[i]==foot||bones[i].IsChildOf(foot);
                var weights=skin.sharedMesh.boneWeights;skin.BakeMesh(mesh,true);var vertices=mesh.vertices;
                for(int i=0;i<weights.Length;i++)
                {
                    var w=weights[i];float amount=(selected[w.boneIndex0]?w.weight0:0)+(selected[w.boneIndex1]?w.weight1:0)+(selected[w.boneIndex2]?w.weight2:0)+(selected[w.boneIndex3]?w.weight3:0);
                    if(amount<.25f)continue;count++;min=Mathf.Min(min,skin.transform.TransformPoint(vertices[i]).y);
                }
            }
            if(count<10)throw new Exception("Missing rendered sole vertices");return min;
        }
        static void Capture(string version)
        {
            string folder=Folder+"/"+version;Directory.CreateDirectory(folder+"/frames");
            var report=new StringBuilder("hero,pose,left_sole_y,right_sole_y,ground_y\n");
            foreach(string id in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);UnityEngine.Random.InitState(930);
                var world=new GameWorld();var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);
                var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                var state=new Battle();state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
                for(int i=0;i<(Battle.TransformationSeconds+0.2f)/(.02f);i++)state.Tick(.02f,new PlayerInput{Tracking=true});state.GiveInstructionTime(20);while(state.TryCue(out _)){}
                hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,1,0);
                var target=new RenderTexture(1280,720,24){antiAliasing=4};target.Create();world.Camera.targetTexture=target;world.Camera.aspect=16f/9;
                var detail=new GameObject("Foot grounding camera").AddComponent<Camera>();detail.enabled=false;detail.fieldOfView=27;detail.aspect=16f/9;detail.allowHDR=true;detail.targetTexture=target;
                var mesh=new Mesh();float health=state.EnemyHealth;int contacts=0;
                try
                {
                    for(int frame=0;frame<240;frame++)
                    {
                        const float dt=1/60f;float t=frame*dt;
                        state.Tick(world.BattleDelta(dt,state),new PlayerInput{Tracking=true,LeftPunch=frame==30,RightPunch=frame==110});
                        while(state.TryCue(out var cue))world.Cue(cue,state);
                        hero.Update(state,world.Camera,dt,t);enemy.Update(state,world.Camera,dt,t);
                        bool hit=state.EnemyHealth<health;if(hit){contacts++;world.Hit(false,state);}health=state.EnemyHealth;world.Tick(state,dt,t);
                        if(id=="Zero"&&frame%2==0)CharacterReview.Save(world.Camera,target,$"{folder}/frames/frame-{frame/2:D4}.png");
                        if(frame!=0&&!hit&&frame!=239)continue;
                        string pose=frame==0?"idle":frame==239?"recovered":contacts==1?"left":"right";
                        float l=Sole(hero,true,mesh),r=Sole(hero,false,mesh);
                        report.AppendLine(FormattableString.Invariant($"{id},{pose},{l:F6},{r:F6},-0.012000"));
                        CharacterReview.Save(world.Camera,target,$"{folder}/{id}-{pose}.png");
                        Vector3 center=(hero.FootPosition(true)+hero.FootPosition(false))*.5f;center.y=.20f;
                        detail.transform.position=center+Vector3.ProjectOnPlane(world.Camera.transform.position-center,Vector3.up).normalized*3.3f+Vector3.up*.95f;
                        detail.transform.LookAt(center);CharacterReview.Save(detail,target,$"{folder}/{id}-{pose}-feet.png");
                        if(version=="after"&&(Mathf.Min(l,r)<-.035f||Mathf.Min(l,r)>.04f))throw new Exception("Ground contact absent: "+id+" "+pose);
                    }
                    if(contacts!=2||state.EnemyHealth!=48)throw new Exception("Grounding sequence did not complete");
                }
                finally{detail.targetTexture=world.Camera.targetTexture=null;RenderTexture.active=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(mesh);}
            }
            File.WriteAllText(folder+"/geometry.csv",report.ToString());
            var sources=new StringBuilder();using(var sha=System.Security.Cryptography.SHA256.Create())
                foreach(string file in new[]{"Scripts/Runtime/ContactShadows.cs","Scripts/Runtime/RiggedActor.cs","Scripts/Runtime/VolcanoStage.cs","Resources/ContactShadow.shader","Resources/ShadowSilhouette.shader","Scripts/Runtime/GameWorld.cs","Scripts/Runtime/CombatVfx.cs"})
                    sources.AppendLine(file+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Application.dataPath,file)))).Replace("-","").ToLowerInvariant());
            File.WriteAllText(folder+"/sources.txt",sources.ToString());Debug.Log("[GroundingReview] "+version+"\n"+report);
            using(var sha=System.Security.Cryptography.SHA256.Create())
                foreach(string file in new[]{"Scripts/Runtime/FootPlantCalibration.cs","Resources/ContactSilhouette.shader"})
                    if(File.Exists(Path.Combine(Application.dataPath,file)))File.AppendAllText(folder+"/sources.txt",file+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Application.dataPath,file)))).Replace("-","").ToLowerInvariant()+"\n");
        }
    }
}
