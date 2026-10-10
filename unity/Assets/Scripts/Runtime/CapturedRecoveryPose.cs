using System;
using System.IO;
using UnityEngine;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    // Offline contact-corrected CMU motion. Loaded only when this actor first
    // rises; no mesh baking, source skeleton solve or per-frame allocation.
    public sealed class CapturedRecoveryPose
    {
        readonly Transform root;
        readonly string id;
        readonly bool fall;
        Transform[] bones;
        Vector3[] positions,basePositions,rootOffsets;
        Quaternion[] samples,baseRotations,baseWorldRotations,worldRotations;
        float[] times;
        readonly float[] clearance=new float[2];
        readonly Transform[,] limbs=new Transform[4,3];
        readonly Vector3[] baseEnds=new Vector3[4],baseBends=new Vector3[4],ends=new Vector3[4],bends=new Vector3[4];
        readonly Quaternion[] baseEndRotations=new Quaternion[4],endRotations=new Quaternion[4];
        Vector3 baseRoot;
        Quaternion baseFacing;
        bool tried;
        public bool Applied {get;private set;}
        public bool Loaded=>bones!=null;
        public int SampleCount=>times?.Length??0;
        public CapturedRecoveryPose(Transform root,string id,bool fall=false){this.root=root;this.id=id;this.fall=fall;}
        static float Read(BinaryReader data)
        {float x=data.ReadSingle();if(float.IsNaN(x)||float.IsInfinity(x))throw new InvalidDataException("Nonfinite recovery value");return x;}
        static Vector3 Vector(BinaryReader data)=>new Vector3(Read(data),Read(data),Read(data));
        bool Load()
        {
            if(tried)return Loaded;tried=true;
            var asset=Resources.Load<TextAsset>("Motions/"+(fall?"Fall":"Recovery")+"/"+id);if(!asset)return false;
            try
            {
                using(var stream=new MemoryStream(asset.bytes,false))using(var data=new BinaryReader(stream))
                {
                    if(data.ReadInt32()!=0x31524355)throw new InvalidDataException("Recovery schema mismatch");
                    int count=data.ReadInt32(),frames=data.ReadInt32();
                    if(count<1||count>512||frames<2||frames>1024)throw new InvalidDataException("Recovery bounds");
                    clearance[0]=Read(data);clearance[1]=Read(data);
                    var targets=new Transform[count];positions=new Vector3[count];
                    for(int i=0;i<count;i++)
                    {string path=data.ReadString();targets[i]=root.Find(path);if(!targets[i])throw new InvalidDataException("Recovery bone missing: "+path);positions[i]=Vector(data);}
                    times=new float[frames];rootOffsets=new Vector3[frames];samples=new Quaternion[count*frames];
                    for(int f=0;f<frames;f++)
                    {
                        times[f]=Read(data);if(f==0&&Mathf.Abs(times[f])>.0001f||f>0&&times[f]<=times[f-1])throw new InvalidDataException("Recovery sample order");
                        rootOffsets[f]=Vector(data);
                        for(int i=0;i<count;i++)
                        {
                            var q=new Quaternion(Read(data),Read(data),Read(data),Read(data));
                            float norm=q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w;
                            if(norm<.98f||norm>1.02f)throw new InvalidDataException("Recovery rotation norm");samples[f*count+i]=q;
                        }
                    }
                    if(stream.Position!=stream.Length)throw new InvalidDataException("Recovery trailing bytes");
                    Transform Find(string a,string b){foreach(var t in targets)if(t.name==a||t.name==b)return t;throw new InvalidDataException("Recovery limb missing "+a);}
                    limbs[0,0]=Find("ThighBase_L","bip_hip_L");limbs[0,1]=Find("Shin_L","bip_knee_L");limbs[0,2]=Find("Foot_L","bip_foot_L");
                    limbs[1,0]=Find("ThighBase_R","bip_hip_R");limbs[1,1]=Find("Shin_R","bip_knee_R");limbs[1,2]=Find("Foot_R","bip_foot_R");
                    limbs[2,0]=Find("armBase_L","bip_upperArm_L");limbs[2,1]=Find("ForearmBase_L","bip_lowerArm_L");limbs[2,2]=Find("HandBase_L","bip_hand_L");
                    limbs[3,0]=Find("armBase_R","bip_upperArm_R");limbs[3,1]=Find("ForearmBase_R","bip_lowerArm_R");limbs[3,2]=Find("HandBase_R","bip_hand_R");
                    bones=targets;basePositions=new Vector3[count];baseRotations=new Quaternion[count];baseWorldRotations=new Quaternion[count];worldRotations=new Quaternion[count];
                    Debug.Log($"[Captured{(fall?"Fall":"Recovery")}] {id} samples={frames} bones={count} bytes={stream.Length}");
                }
                return true;
            }
            catch(Exception error)
            {bones=null;Debug.LogWarning("[CapturedRecovery] authored fallback: "+id+" "+error.Message);return false;}
        }
        public void Restore()
        {
            if(!Applied)return;
            root.SetPositionAndRotation(baseRoot,baseFacing);
            for(int i=0;i<bones.Length;i++){bones[i].localPosition=basePositions[i];bones[i].localRotation=baseRotations[i];}
            Applied=false;
        }
        public void Apply(float age,Vector3 home,Vector3 forward)
        {
            float weight=fall?KnockdownMotion.FallWeight(age):KnockdownMotion.CapturedWeight(age);if(weight<=0||!Load())return;
            baseRoot=root.position;baseFacing=root.rotation;
            for(int i=0;i<bones.Length;i++){basePositions[i]=bones[i].localPosition;baseRotations[i]=bones[i].localRotation;baseWorldRotations[i]=bones[i].rotation;}
            for(int i=0;i<4;i++){baseEnds[i]=limbs[i,2].position;baseBends[i]=limbs[i,1].position;baseEndRotations[i]=limbs[i,2].rotation;}
            float seconds=(fall?KnockdownMotion.FallProgress(age):KnockdownMotion.CapturedProgress(age))*times[times.Length-1];int next=1;
            while(next<times.Length-1&&times[next]<seconds)next++;
            int previous=next-1;float fraction=Mathf.InverseLerp(times[previous],times[next],seconds);
            var facing=Quaternion.LookRotation(forward,Vector3.up);
            var destination=home+facing*Vector3.Lerp(rootOffsets[previous],rootOffsets[next],fraction);
            root.SetPositionAndRotation(destination,facing);
            for(int i=0;i<bones.Length;i++)
            {bones[i].localPosition=positions[i];bones[i].localRotation=Quaternion.Slerp(samples[previous*bones.Length+i],samples[next*bones.Length+i],fraction);}
            for(int i=0;i<4;i++){ends[i]=limbs[i,2].position;bends[i]=limbs[i,1].position;endRotations[i]=limbs[i,2].rotation;}
            for(int i=0;i<bones.Length;i++)worldRotations[i]=bones[i].rotation;
            root.SetPositionAndRotation(Vector3.Lerp(baseRoot,destination,weight),Quaternion.Slerp(baseFacing,facing,weight));
            // Parent-first world orientations avoid compounding independent
            // local arcs through the mirrored, multi-spine costume rigs.
            for(int i=0;i<bones.Length;i++)
            {bones[i].localPosition=Vector3.Lerp(basePositions[i],positions[i],weight);bones[i].rotation=Quaternion.Slerp(baseWorldRotations[i],worldRotations[i],weight);}
            // Blend endpoints as well as rotations. Pure local rotation blends
            // can sweep a planted hand/boot through the floor during handoff.
            for(int i=0;i<4;i++)
            {
                var upper=limbs[i,0];var lower=limbs[i,1];var end=limbs[i,2];
                var rotation=Quaternion.Slerp(baseEndRotations[i],endRotations[i],weight);
                Vector3 target=Vector3.Lerp(baseEnds[i],ends[i],weight);target.y=Mathf.Max(home.y+(i<2?clearance[i]:.15f),target.y);
                Vector3 start=upper.position,to=target-start;float a=Vector3.Distance(start,lower.position),b=Vector3.Distance(lower.position,end.position);
                float d=Mathf.Clamp(to.magnitude,Mathf.Abs(a-b)+.001f,a+b-.001f);Vector3 axis=to.normalized;
                Vector3 pole=Vector3.Lerp(baseBends[i],bends[i],weight)-start;
                Vector3 bend=Vector3.ProjectOnPlane(pole,axis).normalized;
                float along=(a*a-b*b+d*d)/(2*d);
                Vector3 joint=start+axis*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
                upper.rotation=Quaternion.FromToRotation(lower.position-start,joint-start)*upper.rotation;
                lower.rotation=Quaternion.FromToRotation(end.position-lower.position,start+axis*d-lower.position)*lower.rotation;
                end.rotation=rotation;
            }
            Applied=true;
        }
    }
}
