using System;
using System.Collections.Generic;
using UnityEngine;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    // Grigio Shot crosses at the wrists, with the upright right palm emitting
    // the beam. The circular wind-up is authored here; combat keeps its clock.
    public sealed class GrigioShot
    {
        readonly Transform root;
        readonly Transform[] arms;
        readonly List<Transform> fingers=new List<Transform>();
        readonly Quaternion[] armBase=new Quaternion[6];
        readonly Quaternion[] fingerBase,fingerOpen;
        readonly Vector3[] palmForward=new Vector3[2],palmNormal=new Vector3[2],entryHands=new Vector3[2];
        readonly Action<Transform,Transform,Transform,Vector3,float,Vector3,float> solve;
        Battle observed;
        bool wasBeam,applied;
        float age,returnAge=1;
        public float Age=>age;
        public bool Active=>wasBeam||returnAge<.34f;
        public bool Crossed=>wasBeam&&age>=1.10f;
        public Vector3 Muzzle=>arms[5].position+Vector3.up*.10f+root.forward*.065f;
        public int FingerCount=>fingers.Count;
        public GrigioShot(Transform root,Transform[] arms,Renderer[] surfaces,
            Action<Transform,Transform,Transform,Vector3,float,Vector3,float> solve)
        {
            this.root=root;this.arms=arms;this.solve=solve;
            foreach(int side in new[]{0,1})
                foreach(var bone in arms[side*3+2].GetComponentsInChildren<Transform>())
                    if(bone.name.StartsWith("bip_middle_",StringComparison.Ordinal))fingers.Add(bone);
            fingerBase=new Quaternion[fingers.Count];fingerOpen=new Quaternion[fingers.Count];
            var bind=new Dictionary<Transform,Matrix4x4>();
            foreach(var surface in surfaces)if(surface is SkinnedMeshRenderer skin)
            {var bones=skin.bones;var poses=skin.sharedMesh.bindposes;for(int i=0;i<bones.Length;i++)if(bones[i])bind[bones[i]]=skin.transform.localToWorldMatrix*poses[i].inverse;}
            for(int i=0;i<fingers.Count;i++)
            {var bone=fingers[i];fingerOpen[i]=bind.ContainsKey(bone)&&bind.ContainsKey(bone.parent)?(bind[bone.parent].inverse*bind[bone]).rotation:bone.localRotation;}
            for(int side=0;side<2;side++)
            {
                var hand=arms[side*3+2];var middle=fingers.Find(b=>b.parent==hand);
                var inverse=bind[hand].inverse;
                palmForward[side]=inverse.MultiplyPoint3x4(bind[middle].GetColumn(3)).normalized;
                // This model has no index/pinky bones. Measure the palm's
                // thin axis from its bind mesh instead of guessing a roll
                // from world axes, which would make it face the camera.
                var axis=Vector3.Cross(palmForward[side],Mathf.Abs(palmForward[side].y)<.9f?Vector3.up:Vector3.right).normalized;
                var other=Vector3.Cross(palmForward[side],axis).normalized;
                float xx=0,xy=0,yy=0;
                foreach(var surface in surfaces)if(surface is SkinnedMeshRenderer skin)
                {
                    int handIndex=Array.IndexOf(skin.bones,hand);var weights=skin.sharedMesh.boneWeights;var vertices=skin.sharedMesh.vertices;
                    var matrix=inverse*skin.transform.localToWorldMatrix;
                    for(int i=0;i<vertices.Length;i++)
                    {
                        var w=weights[i];float weight=(w.boneIndex0==handIndex?w.weight0:0)+(w.boneIndex1==handIndex?w.weight1:0)+(w.boneIndex2==handIndex?w.weight2:0)+(w.boneIndex3==handIndex?w.weight3:0);
                        if(weight<.5f)continue;
                        var point=matrix.MultiplyPoint3x4(vertices[i]);float x=Vector3.Dot(point,axis),y=Vector3.Dot(point,other);
                        xx+=x*x;xy+=x*y;yy+=y*y;
                    }
                }
                float angle=.5f*Mathf.Atan2(2*xy,xx-yy)+Mathf.PI*.5f;
                var normal=axis*Mathf.Cos(angle)+other*Mathf.Sin(angle);
                // Sign follows the bind-pose palm, while the measured plane
                // fixes the roll independently of the sampled idle animation.
                var worldNormal=bind[hand].MultiplyVector(normal).normalized;
                if(Vector3.Dot(worldNormal,root.forward)*(side==0?1:-1)<0)normal=-normal;
                palmNormal[side]=normal;
            }
            Debug.Log($"[GrigioShot] fingerBones={fingers.Count} wristCross=True");
        }
        static float Ease(float t,float a,float b)=>Mathf.SmoothStep(0,1,(t-a)/(b-a));
        public void Clear(){Restore();observed=null;wasBeam=false;age=0;returnAge=1;}
        void Restore()
        {
            if(!applied)return;
            for(int i=0;i<6;i++)arms[i].localRotation=armBase[i];
            for(int i=0;i<fingers.Count;i++)fingers[i].localRotation=fingerBase[i];
            applied=false;
        }
        public void Observe(Battle state,float dt,int preview)
        {
            bool same=ReferenceEquals(observed,state);
            if(!same||preview>=0||(state.Phase!=GamePhase.Battle&&state.Phase!=GamePhase.Victory))
            {observed=state;wasBeam=false;returnAge=1;age=0;Restore();return;}
            bool beam=state.Phase==GamePhase.Battle&&state.Action==HeroAction.Beam;
            if(beam&&!wasBeam){age=0;returnAge=1;for(int i=0;i<2;i++)entryHands[i]=root.InverseTransformPoint(arms[i*3+2].position);}
            else if(beam)age+=Mathf.Max(0,dt);
            else if(wasBeam)returnAge=0;
            else returnAge+=Mathf.Max(0,dt);
            wasBeam=beam;
            if(!beam&&state.Phase==GamePhase.Battle&&(state.IsPunch||state.Shield||state.Action==HeroAction.Hurt))returnAge=1;
            Restore();
        }
        public void Pose(int preview=-1)
        {
            if(preview==4){age=1.10f;wasBeam=true;returnAge=1;}
            if(!Active)return;
            for(int i=0;i<6;i++)armBase[i]=arms[i].localRotation;
            for(int i=0;i<fingers.Count;i++)fingerBase[i]=fingers[i].localRotation;applied=true;
            var side=root.right;var forward=root.forward;var up=Vector3.up;
            var shoulder=(arms[0].position+arms[3].position)*.5f;
            float upper=Vector3.Distance(arms[3].position,arms[4].position),lower=Vector3.Distance(arms[4].position,arms[5].position);
            var rightElbow=arms[3].position+(side*.07f-up*.45f+forward*.28f).normalized*upper;
            var rightCross=rightElbow+up*lower;
            // The left hand passes behind the emitting wrist, not underneath
            // the right elbow as in the shared L-shaped clip.
            var leftCross=rightCross-side*.10f-up*.085f-forward*.19f;
            float circle=Ease(age,0,.72f),cross=Ease(age,.72f,1.10f),recover=wasBeam?0:Ease(returnAge,0,.34f);
            for(int i=0;i<2;i++)
            {
                int j=i*3;float sign=i==0?-1:1;
                float angle=Mathf.Lerp(-70,55,circle)*Mathf.Deg2Rad;
                var open=shoulder+side*(sign*.68f*Mathf.Cos(angle))+up*(.38f*Mathf.Sin(angle)-.12f)+forward*.46f;
                var target=Vector3.Lerp(open,i==0?leftCross:rightCross,cross);
                target=Vector3.Lerp(root.TransformPoint(entryHands[i]),target,Ease(age,0,.22f));
                target=Vector3.Lerp(target,arms[j+2].position,recover);
                var pole=i==0?-side-up*.25f+forward*.2f:Vector3.Lerp(side-up,rightElbow-(arms[3].position+rightCross)*.5f,cross);
                solve(arms[j],arms[j+1],arms[j+2],target,1,pole,.3f);
                var hand=arms[j+2];var targetDirection=i==0?side:up;var normal=i==0?up:-side;
                var openRotation=Quaternion.LookRotation(i==0?side:-side,up)*Quaternion.Inverse(Quaternion.LookRotation(palmForward[i],palmNormal[i]));
                var rotation=Quaternion.LookRotation(targetDirection,normal)*Quaternion.Inverse(Quaternion.LookRotation(palmForward[i],palmNormal[i]));
                rotation=Quaternion.Slerp(openRotation,rotation,cross);
                hand.rotation=Quaternion.Slerp(hand.rotation,rotation,Ease(age,.08f,.28f)*(1-recover));
                // The skin's bind pose has straight fingers. Use its original
                // local rotations, including the fingertip with no child bone.
                for(int f=0;f<fingers.Count;f++)if(fingers[f].IsChildOf(hand))
                    fingers[f].localRotation=Quaternion.Slerp(fingerBase[f],fingerOpen[f],Ease(age,.08f,.28f)*(1-recover));
            }
            if(preview==4){wasBeam=false;age=0;returnAge=1;}
        }
    }
}
