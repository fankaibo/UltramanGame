using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Runtime;
using UltramanGame.Core;

namespace UltramanGame.Editor
{
    // Source-motion experiment only. Nothing here is loaded by the player.
    public static class MocapRecoveryReview
    {
        [Serializable] sealed class Frame {public float[] positions;}
        [Serializable] sealed class Capture {public string[] joints;public Frame[] frames;public float legLength,duration;}
        sealed class Binding {public Transform Bone;public Quaternion Rest,Anatomical;public string Name;}
        static readonly string[][] Map={
            new[]{"Hips","hip","bip_pelvis"},new[]{"LowerBack","spineLower","bip_spine_0"},
            new[]{"Spine","spineUpper","bip_spine_1"},new[]{"Spine1","spineChest","bip_spine_2"},
            new[]{"Neck","neckLower","bip_neck"},new[]{"Head","head","bip_head"},
            new[]{"LeftArm","armBase_L","bip_upperArm_L"},new[]{"RightArm","armBase_R","bip_upperArm_R"},
            new[]{"LeftForeArm","ForearmBase_L","bip_lowerArm_L"},new[]{"RightForeArm","ForearmBase_R","bip_lowerArm_R"},
            new[]{"LeftHand","HandBase_L","bip_hand_L"},new[]{"RightHand","HandBase_R","bip_hand_R"},
            new[]{"LeftUpLeg","ThighBase_L","bip_hip_L"},new[]{"RightUpLeg","ThighBase_R","bip_hip_R"},
            new[]{"LeftLeg","Shin_L","bip_knee_L"},new[]{"RightLeg","Shin_R","bip_knee_R"},
            new[]{"LeftFoot","Foot_L","bip_foot_L"},new[]{"RightFoot","Foot_R","bip_foot_R"}};
        static int Depth(Transform bone){int depth=0;for(var p=bone.parent;p;p=p.parent)depth++;return depth;}
        static Quaternion Along(Vector3 direction,Vector3 plane)
        {
            var up=Vector3.ProjectOnPlane(plane,direction);
            if(up.sqrMagnitude<.000001f)up=Vector3.ProjectOnPlane(Vector3.forward,direction);
            return Quaternion.LookRotation(direction,up);
        }
        static Quaternion Anatomy(string name,Func<string,Vector3> p)
        {
            Vector3 shoulderRight=p("RightArm")-p("LeftArm"),hipRight=p("RightUpLeg")-p("LeftUpLeg");
            if(name=="Hips")return Along(Vector3.Cross(hipRight,p("LowerBack")-p("Hips")),p("LowerBack")-p("Hips"));
            if(name=="LowerBack"||name=="Spine"||name=="Spine1"||name=="Neck"||name=="Head")
            {
                string next=name=="LowerBack"?"Spine":name=="Spine"?"Spine1":name=="Spine1"?"Neck":"Head";
                Vector3 up=name=="Head"?p("Head")-p("Neck"):p(next)-p(name);
                return Along(Vector3.Cross(shoulderRight,up),up);
            }
            string side=name.StartsWith("Left")?"Left":"Right";
            if(name.EndsWith("Foot"))return Along(p(side+"ToeBase")-p(name),Vector3.up);
            bool arm=name.Contains("Arm")||name.Contains("Hand");
            string a=side+(arm?"Arm":"UpLeg"),b=side+(arm?"ForeArm":"Leg"),c=side+(arm?"Hand":"Foot");
            Vector3 first=p(b)-p(a),second=p(c)-p(b);
            Vector3 plane=Vector3.Cross(first,second);
            Vector3 axis=name==a?first:second;
            return Along(axis,Vector3.Cross(plane,axis));
        }
        public static void Preview()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/mocap-recovery-20261010"));
            var capture=JsonUtility.FromJson<Capture>(File.ReadAllText(folder+"/side-rise-positions.json"));
            string variant="anatomical-sequence";
            var report=new StringBuilder();Directory.CreateDirectory(folder+"/"+variant);
            foreach(string id in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var world=new GameWorld();var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);
                var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                var state=new Battle();hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,0,0);
                Quaternion facing=hero.Root.rotation;Vector3 home=hero.Root.position;
                var joints=hero.Root.GetComponentsInChildren<Transform>();var bindings=new List<Binding>();
                var reference=new Dictionary<string,Vector3>();
                foreach(var map in Map)
                {
                    var bone=joints.FirstOrDefault(t=>t.name==map[1]||t.name==map[2]);if(!bone)continue;
                    reference[map[0]]=hero.Root.InverseTransformPoint(bone.position);
                    bindings.Add(new Binding{Bone=bone,Rest=Quaternion.Inverse(facing)*bone.rotation,Name=map[0]});
                }
                if(!reference.ContainsKey("Spine1"))reference["Spine1"]=Vector3.Lerp(reference["Spine"],reference["Neck"],.5f);
                reference["LeftToeBase"]=reference["LeftFoot"]+Vector3.forward;
                reference["RightToeBase"]=reference["RightFoot"]+Vector3.forward;
                foreach(var b in bindings)b.Anatomical=Anatomy(b.Name,n=>reference[n]);
                bindings.Sort((a,b)=>Depth(a.Bone).CompareTo(Depth(b.Bone)));
                Transform Bone(string name)=>bindings.Single(b=>b.Name==name).Bone;
                var hips=Bone("Hips");float leg=Vector3.Distance(Bone("LeftUpLeg").position,Bone("LeftLeg").position)+Vector3.Distance(Bone("LeftLeg").position,Bone("LeftFoot").position);
                float scale=leg/capture.legLength;
                var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
                string output=folder+"/"+variant+"/"+id;Directory.CreateDirectory(output);
                if(id=="Tiga")Directory.CreateDirectory(output+"/frames");
                var trace=new StringBuilder("frame,minimumMeshY,hipY,leftWristY,rightWristY,leftFootY,rightFootY,maximumJointStep\n");
                var baked=new Mesh();var old=new Vector3[bindings.Count];bool first=true;
                float minGround=100,maxStep=0;
                try
                {
                    int count=capture.frames.Length;
                    for(int n=0;n<count;n++)
                    {
                        int index=n;var sample=capture.frames[index];
                        Vector3 Point(string name){int i=Array.IndexOf(capture.joints,name)*3;return new Vector3(sample.positions[i],sample.positions[i+1],sample.positions[i+2]);}
                        hero.Root.SetPositionAndRotation(home,facing);
                        foreach(var b in bindings)b.Bone.rotation=facing*Anatomy(b.Name,Point)*Quaternion.Inverse(b.Anatomical)*b.Rest;
                        hips.position=home+facing*Point("Hips")*scale;
                        // Review-only contact offset, measured independently for every pose.
                        float lowest=Mathf.Min(Bone("LeftFoot").position.y,Bone("RightFoot").position.y);
                        hips.position+=Vector3.up*(home.y+.18f-lowest);
                        float ground=100,step=0;
                        foreach(var skin in hero.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {skin.BakeMesh(baked,true);foreach(var v in baked.vertices)ground=Mathf.Min(ground,skin.transform.TransformPoint(v).y);}
                        for(int i=0;i<bindings.Count;i++)
                        {
                            Vector3 p=bindings[i].Bone.position;
                            if(float.IsNaN(p.sqrMagnitude)||float.IsInfinity(p.sqrMagnitude))throw new Exception("Nonfinite retargeted joint");
                            if(!first)step=Mathf.Max(step,Vector3.Distance(old[i],p));old[i]=p;
                        }
                        minGround=Mathf.Min(minGround,ground);maxStep=Mathf.Max(maxStep,step);first=false;
                        trace.AppendLine(FormattableString.Invariant($"{index},{ground:F6},{hips.position.y:F6},{Bone("LeftHand").position.y:F6},{Bone("RightHand").position.y:F6},{Bone("LeftFoot").position.y:F6},{Bone("RightFoot").position.y:F6},{step:F6}"));
                        if(n%12==0)CharacterReview.Save(world.Camera,rt,$"{output}/pose-{index:D3}.png");
                        if(id=="Tiga")CharacterReview.Save(world.Camera,rt,$"{output}/frames/{index:D4}.png");
                    }
                    report.AppendLine($"{id} bindings={bindings.Count} scale={scale:F5} samples={count} minimumMeshY={minGround:F5} maximumJointStep={maxStep:F5} status=prototype-not-release-ready");
                    File.WriteAllText(output+"/trace.csv",trace.ToString());
                }
                finally{world.Camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(baked);}
            }
            File.WriteAllText(folder+"/"+variant+"/metrics.txt",report.ToString());Debug.Log("[MocapRecoveryPrototype] "+report);
        }
    }
}
