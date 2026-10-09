using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
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
        [Serializable] sealed class Capture {public string[] joints;public Frame[] frames;public float legLength,duration;public float[] times;}
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
        static Vector3 Point(Capture capture,Frame sample,string name)
        {int i=Array.IndexOf(capture.joints,name)*3;return new Vector3(sample.positions[i],sample.positions[i+1],sample.positions[i+2]);}
        static void Limb(Transform upper,Transform lower,Transform end,Vector3 target,Vector3 pole)
        {
            Vector3 start=upper.position,to=target-start;float a=Vector3.Distance(start,lower.position),b=Vector3.Distance(lower.position,end.position);
            float d=Mathf.Clamp(to.magnitude,Mathf.Abs(a-b)+.002f,a+b-.002f);Vector3 axis=to.normalized;
            Vector3 bend=Vector3.ProjectOnPlane(pole,axis).normalized;
            float along=(a*a-b*b+d*d)/(2*d);Vector3 knee=start+axis*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            upper.rotation=Quaternion.FromToRotation(lower.position-start,knee-start)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(end.position-lower.position,start+axis*d-lower.position)*lower.rotation;
        }
        public static void Preview()=>Render(false,false);
        public static void Grounded()=>Render(true,false);
        public static void Bake()=>Render(true,true);
        static void Render(bool grounded,bool bake)
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/mocap-recovery-20261010"));
            var capture=JsonUtility.FromJson<Capture>(File.ReadAllText(folder+"/side-rise-positions.json"));
            string variant=bake?"baked-sequence":grounded?"reach-supported-sequence":"anatomical-sequence";
            var report=new StringBuilder();Directory.CreateDirectory(folder+"/"+variant);
            foreach(string id in new[]{"Tiga","Mebius","Zero","Geed","Grigio"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var world=new GameWorld();var hero=new AnimatedActor(id,world.HeroHome,world.EnemyHome);
                var enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);world.BindActors(hero,enemy);
                var state=new Battle();hero.Update(state,world.Camera,0,0);enemy.Update(state,world.Camera,0,0);world.Tick(state,0,0);
                Quaternion facing=hero.Root.rotation;Vector3 home=hero.Root.position;
                Vector3 floorOrigin=home;floorOrigin.y=world.HeroHome.y;
                var joints=hero.Root.GetComponentsInChildren<Transform>();var bindings=new List<Binding>();
                var originalPositions=joints.Select(t=>t.localPosition).ToArray();var originalRotations=joints.Select(t=>t.localRotation).ToArray();
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
                var footRotations=new Quaternion[2];var footClearances=new float[2];var sourceFloor=new float[2];
                var surfaces=hero.Root.GetComponentsInChildren<Renderer>();
                Vector3 sourceEndFeet=(Point(capture,capture.frames.Last(),"LeftFoot")+Point(capture,capture.frames.Last(),"RightFoot"))*.5f;
                Vector3 offset=(reference["LeftFoot"]+reference["RightFoot"])*.5f-sourceEndFeet*scale;offset.y=0;
                for(int s=0;s<2;s++)
                {
                    string side=s==0?"Left":"Right";var foot=Bone(side+"Foot");var anchor=hero.Root.InverseTransformPoint(foot.position);var rotation=Quaternion.Inverse(facing)*foot.rotation;
                    FootPlantCalibration.Apply(hero.Root,foot,surfaces,ref anchor,ref rotation);footClearances[s]=anchor.y;footRotations[s]=rotation;
                    sourceFloor[s]=capture.frames.Min(f=>Mathf.Min(Point(capture,f,side+"Foot").y,Point(capture,f,side+"ToeBase").y));
                }
                var footTargets=new Vector3[2][];var footYaws=new float[2][];var planted=new bool[2][];
                for(int s=0;s<2;s++)
                {
                    string side=s==0?"Left":"Right";int count=capture.frames.Length;
                    footTargets[s]=new Vector3[count];footYaws[s]=new float[count];planted[s]=new bool[count];var candidates=new bool[count];
                    for(int n=0;n<count;n++)
                    {
                        Vector3 foot=Point(capture,capture.frames[n],side+"Foot"),toe=Point(capture,capture.frames[n],side+"ToeBase");
                        float height=Mathf.Max(0,(Mathf.Min(foot.y,toe.y)-sourceFloor[s])*scale);
                        var target=foot*scale+offset;target.y=footClearances[s]+Mathf.Max(0,height-.035f);footTargets[s][n]=target;
                        footYaws[s][n]=Vector3.SignedAngle(Vector3.forward,Vector3.ProjectOnPlane(toe-foot,Vector3.up),Vector3.up);
                        int firstSample=Mathf.Max(0,n-2),last=Mathf.Min(count-1,n+2);
                        Vector3 delta=Point(capture,capture.frames[last],side+"Foot")-Point(capture,capture.frames[firstSample],side+"Foot");delta.y=0;
                        float speed=delta.magnitude*scale/(capture.times[last]-capture.times[firstSample]);
                        candidates[n]=height<.085f&&speed<.35f;
                    }
                    for(int start=0;start<count;start++)
                    {
                        if(!candidates[start])continue;int end=start;while(end+1<count&&candidates[end+1])end++;
                        if(end-start>=5)
                        {
                            Vector3 anchor=Vector3.zero;float yaw=0;for(int i=start;i<=end;i++){anchor+=footTargets[s][i];yaw+=footYaws[s][i];}
                            anchor/=(end-start+1);anchor.y=footClearances[s];yaw/=end-start+1;
                            for(int i=start;i<=end;i++)
                            {
                                float weight=Mathf.SmoothStep(0,1,Mathf.Min(i-start,end-i)/3f);
                                footTargets[s][i]=Vector3.Lerp(footTargets[s][i],anchor,weight);
                                footYaws[s][i]=Mathf.LerpAngle(footYaws[s][i],yaw,weight);planted[s][i]=weight>.999f;
                            }
                        }
                        start=end;
                    }
                }
                var rt=new RenderTexture(1280,720,24){antiAliasing=4};rt.Create();world.Camera.targetTexture=rt;world.Camera.aspect=16f/9;
                string output=folder+"/"+variant+"/"+id;Directory.CreateDirectory(output);
                if(id=="Tiga")Directory.CreateDirectory(output+"/frames");
                var trace=new StringBuilder("frame,minimumMeshY,hipY,leftWristY,rightWristY,leftFootY,rightFootY,maximumJointStep,bodyLift,footError,leftPlant,rightPlant\n");
                var baked=new Mesh();var old=new Vector3[bindings.Count];bool first=true;
                float minGround=100,maxStep=0,maxLift=0,maxFootError=0;
                var motionStream=new MemoryStream();var motion=new BinaryWriter(motionStream);
                if(bake)
                {
                    motion.Write(0x31524355);motion.Write(joints.Length-1);motion.Write(capture.frames.Length);
                    motion.Write(footClearances[0]);motion.Write(footClearances[1]);
                    for(int i=1;i<joints.Length;i++)
                    {
                        motion.Write(AnimationUtility.CalculateTransformPath(joints[i],hero.Root));
                        var p=originalPositions[i];motion.Write(p.x);motion.Write(p.y);motion.Write(p.z);
                    }
                }
                try
                {
                    int count=capture.frames.Length;
                    for(int n=0;n<count;n++)
                    {
                        int index=n;var sample=capture.frames[index];
                        Vector3 P(string name)=>Point(capture,sample,name);
                        float liftBody=0,error=0;
                        float Ground()
                        {float lowest=100;foreach(var skin in hero.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                            {skin.BakeMesh(baked,true);foreach(var v in baked.vertices)lowest=Mathf.Min(lowest,skin.transform.TransformPoint(v).y);}return lowest;}
                        void Feet()
                        {
                            error=0;
                            for(int s=0;s<2;s++)
                            {
                                string side=s==0?"Left":"Right";Vector3 target=floorOrigin+facing*footTargets[s][n];
                                var upper=Bone(side+"UpLeg");var lower=Bone(side+"Leg");var foot=Bone(side+"Foot");
                                Limb(upper,lower,foot,target,facing*(P(side+"Leg")-P(side+"UpLeg")));
                                foot.rotation=facing*Quaternion.AngleAxis(footYaws[s][n],Vector3.up)*footRotations[s];
                                error=Mathf.Max(error,Vector3.Distance(foot.position,target));
                            }
                        }
                        void FitReach()
                        {
                            // The source actor and costume have different hip
                            // offsets. Move the body inside both leg reach
                            // spheres before solving, preserving planted boots.
                            for(int pass=0;pass<6;pass++)for(int s=0;s<2;s++)
                            {
                                string side=s==0?"Left":"Right";var upper=Bone(side+"UpLeg");var lower=Bone(side+"Leg");var foot=Bone(side+"Foot");
                                Vector3 delta=floorOrigin+facing*footTargets[s][n]-upper.position;
                                float reach=Vector3.Distance(upper.position,lower.position)+Vector3.Distance(lower.position,foot.position)-.015f;
                                if(delta.magnitude>reach)hero.Root.position+=delta*(1-reach/delta.magnitude);
                            }
                        }
                        hero.Root.SetPositionAndRotation(home,facing);
                        if(grounded)for(int i=1;i<joints.Length;i++){joints[i].localPosition=originalPositions[i];joints[i].localRotation=originalRotations[i];}
                        foreach(var b in bindings)b.Bone.rotation=facing*Anatomy(b.Name,P)*Quaternion.Inverse(b.Anatomical)*b.Rest;
                        if(grounded)
                        {
                            // Moving the actor includes skeleton branches that
                            // are not children of the pelvis in imported rigs.
                            hero.Root.position+=floorOrigin+facing*(P("Hips")*scale+offset)-hips.position;
                            FitReach();Feet();
                            // Adapt low seated hip clearance to each costume's
                            // geometry while solving the SAME foot targets.
                            for(int pass=0;pass<3;pass++)
                            {
                                float needed=floorOrigin.y+.002f-Ground();if(needed<.0001f)break;
                                hero.Root.position+=Vector3.up*needed;liftBody+=needed;FitReach();Feet();
                            }
                        }
                        else
                        {
                            hips.position=home+facing*P("Hips")*scale;
                            float lowest=Mathf.Min(Bone("LeftFoot").position.y,Bone("RightFoot").position.y);
                            hips.position+=Vector3.up*(home.y+.18f-lowest);
                        }
                        float ground=Ground(),step=0;
                        for(int i=0;i<bindings.Count;i++)
                        {
                            Vector3 p=bindings[i].Bone.position;
                            if(float.IsNaN(p.sqrMagnitude)||float.IsInfinity(p.sqrMagnitude))throw new Exception("Nonfinite retargeted joint");
                            if(!first)step=Mathf.Max(step,Vector3.Distance(old[i],p));old[i]=p;
                        }
                        minGround=Mathf.Min(minGround,ground);maxStep=Mathf.Max(maxStep,step);maxLift=Mathf.Max(maxLift,liftBody);maxFootError=Mathf.Max(maxFootError,error);first=false;
                        trace.AppendLine(FormattableString.Invariant($"{index},{ground:F6},{hips.position.y:F6},{Bone("LeftHand").position.y:F6},{Bone("RightHand").position.y:F6},{Bone("LeftFoot").position.y:F6},{Bone("RightFoot").position.y:F6},{step:F6},{liftBody:F6},{error:F6},{planted[0][n]},{planted[1][n]}"));
                        if(bake)
                        {
                            motion.Write(capture.times[n]);var delta=Quaternion.Inverse(facing)*(hero.Root.position-world.HeroHome);
                            motion.Write(delta.x);motion.Write(delta.y);motion.Write(delta.z);
                            for(int i=1;i<joints.Length;i++){var q=joints[i].localRotation;motion.Write(q.x);motion.Write(q.y);motion.Write(q.z);motion.Write(q.w);}
                        }
                        if(n%12==0)CharacterReview.Save(world.Camera,rt,$"{output}/pose-{index:D3}.png");
                        if(id=="Tiga")CharacterReview.Save(world.Camera,rt,$"{output}/frames/{index:D4}.png");
                    }
                    report.AppendLine($"{id} bindings={bindings.Count} scale={scale:F5} samples={count} minimumMeshY={minGround:F5} maximumJointStep={maxStep:F5} hipLift={maxLift:F5} footError={maxFootError:F5} planted=({planted[0].Count(v=>v)},{planted[1].Count(v=>v)}) status=prototype-not-release-ready");
                    File.WriteAllText(output+"/trace.csv",trace.ToString());
                    if(bake)
                    {
                        if(minGround<-.01f||maxFootError>.005f)throw new Exception("Captured motion contact not ready: "+report);
                        Directory.CreateDirectory(folder+"/bakes");File.WriteAllBytes(folder+"/bakes/"+id+".bytes",motionStream.ToArray());
                        report.AppendLine($"{id} bakedBones={joints.Length-1} bytes={motionStream.Length}");
                    }
                }
                finally{motion.Dispose();motionStream.Dispose();world.Camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(baked);}
            }
            File.WriteAllText(folder+"/"+variant+"/metrics.txt",report.ToString());Debug.Log("[MocapRecoveryPrototype] "+report);
        }
    }
}
