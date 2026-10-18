using System;
using System.Collections.Generic;
using UnityEngine;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    // A presentation clock: touch the left brace, open the arms, then cross
    // into Mebium Shoot. Combat and the child's gesture keep their own clocks.
    public sealed class MebiumShoot
    {
        readonly Transform root;
        readonly Transform[] arms;
        readonly List<Transform> fingers=new List<Transform>();
        readonly Quaternion[] armBase=new Quaternion[6];
        readonly Quaternion[] fingerBase,fingerOpen;
        readonly Vector3[] palmForward=new Vector3[2],palmNormal=new Vector3[2],entryHands=new Vector3[2];
        readonly Action<Transform,Transform,Transform,Vector3,float,Vector3,float> solve;
        readonly Func<Vector3> brace;
        Battle observed;
        bool wasBeam,applied;
        float age,returnAge=1;
        public float Age=>age;
        public bool Active=>wasBeam||returnAge<.34f;
        public bool Crossed=>wasBeam&&age>=1.10f;
        public Vector3 Muzzle=>Vector3.Lerp(arms[4].position,arms[5].position,.72f)+root.forward*.035f;
        public int FingerCount=>fingers.Count;
        public MebiumShoot(Transform root,Transform[] arms,Renderer[] surfaces,Func<Vector3> brace,
            Action<Transform,Transform,Transform,Vector3,float,Vector3,float> solve)
        {
            this.root=root;this.arms=arms;this.brace=brace;this.solve=solve;
            for(int side=0;side<2;side++)
            {
                var hand=arms[side*3+2];Vector3 center=Vector3.zero;int count=0;Transform index=null,pinky=null;
                foreach(var bone in hand.GetComponentsInChildren<Transform>())
                {
                    if(bone==hand)continue;
                    if(!(bone.name.Contains("index")||bone.name.Contains("middle")||bone.name.Contains("ring")||bone.name.Contains("pinky")||bone.name.Contains("thumb")))continue;
                    fingers.Add(bone);
                    if(bone.parent==hand&&!bone.name.Contains("thumb")){center+=bone.position;count++;if(bone.name.Contains("index"))index=bone;if(bone.name.Contains("pinky"))pinky=bone;}
                }
                var forward=count>0?(center/count-hand.position).normalized:(hand.position-arms[side*3+1].position).normalized;
                var across=index&&pinky?(index.position-pinky.position).normalized:root.right;
                palmForward[side]=hand.InverseTransformDirection(forward);
                palmNormal[side]=hand.InverseTransformDirection(Vector3.Cross(forward,across).normalized*(side==0?1:-1));
            }
            fingerBase=new Quaternion[fingers.Count];fingerOpen=new Quaternion[fingers.Count];
            var bind=new Dictionary<Transform,Matrix4x4>();
            foreach(var surface in surfaces)if(surface is SkinnedMeshRenderer skin)
            {var bones=skin.bones;var poses=skin.sharedMesh.bindposes;for(int i=0;i<bones.Length;i++)if(bones[i])bind[bones[i]]=skin.transform.localToWorldMatrix*poses[i].inverse;}
            for(int i=0;i<fingers.Count;i++)
            {var bone=fingers[i];fingerOpen[i]=bind.ContainsKey(bone)&&bind.ContainsKey(bone.parent)?(bind[bone.parent].inverse*bind[bone]).rotation:bone.localRotation;}
            Debug.Log($"[MebiumShoot] fingerBones={fingers.Count} braceAnchored=True");
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
            var rightElbow=arms[3].position+(-side*.24f-up*.27f+forward*.31f).normalized*upper;
            var rightCross=rightElbow+up*lower;
            var leftCross=rightElbow+up*.21f+side*.25f-forward*.30f;
            float spread=Ease(age,.28f,.70f),cross=Ease(age,.70f,1.10f),recover=wasBeam?0:Ease(returnAge,0,.34f);
            for(int i=0;i<2;i++)
            {
                int j=i*3;float sign=i==0?-1:1;
                var touch=shoulder+side*(i==0?.15f:-.05f)-up*.24f+forward*.55f;
                if(i==1)touch=brace()+up*.07f+forward*.035f;
                var open=shoulder+side*(sign*.67f)+up*.04f+forward*.20f;
                var target=Vector3.Lerp(touch,open,spread);
                target=Vector3.Lerp(target,i==0?leftCross:rightCross,cross);
                target=Vector3.Lerp(root.TransformPoint(entryHands[i]),target,Ease(age,0,.24f));
                target=Vector3.Lerp(target,arms[j+2].position,recover);
                Vector3 pole=i==0?-side+forward*.8f:Vector3.Lerp(side-up, rightElbow-(arms[3].position+rightCross)*.5f,cross);
                solve(arms[j],arms[j+1],arms[j+2],target,1,pole,.3f);
                var hand=arms[j+2];var targetDirection=i==0?side:up;var normal=i==0?up:-side;
                var touchRotation=Quaternion.LookRotation(i==0?side:-side,up)*Quaternion.Inverse(Quaternion.LookRotation(palmForward[i],palmNormal[i]));
                var rotation=Quaternion.LookRotation(targetDirection,normal)*Quaternion.Inverse(Quaternion.LookRotation(palmForward[i],palmNormal[i]));
                rotation=Quaternion.Slerp(touchRotation,rotation,cross);
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
