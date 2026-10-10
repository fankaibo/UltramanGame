using UnityEngine;

namespace UltramanGame.Runtime
{
    // Preserve the complete pose the player saw before additive animation is
    // removed. This is presentation-only; the shield owns damage immediately.
    public sealed class HeroPoseHandoff
    {
        readonly Transform root,leftFoot,rightFoot;
        readonly Transform[] bones;
        readonly Vector3[] fromPositions,basePositions;
        readonly Quaternion[] fromRotations,baseRotations;
        Vector3 fromRoot,baseRoot,fromLeft,fromRight;
        Quaternion fromFacing,baseFacing,fromLeftRotation,fromRightRotation;
        float age=1,duration=.18f;
        bool applied;
        public bool Active=>age<duration;
        public float Progress=>Mathf.Clamp01(age/duration);
        public Vector3 LeftTarget {get;private set;}
        public Vector3 RightTarget {get;private set;}
        public Quaternion LeftRotation {get;private set;}
        public Quaternion RightRotation {get;private set;}
        public HeroPoseHandoff(Transform root,Transform[] bones,Transform leftFoot,Transform rightFoot)
        {
            this.root=root;this.bones=bones;this.leftFoot=leftFoot;this.rightFoot=rightFoot;
            fromPositions=new Vector3[bones.Length];basePositions=new Vector3[bones.Length];
            fromRotations=new Quaternion[bones.Length];baseRotations=new Quaternion[bones.Length];
        }
        public void Begin(float seconds=.18f)
        {
            duration=seconds;age=0;fromRoot=root.position;fromFacing=root.rotation;
            fromLeft=leftFoot.position;fromRight=rightFoot.position;
            fromLeftRotation=leftFoot.rotation;fromRightRotation=rightFoot.rotation;
            for(int i=0;i<bones.Length;i++){fromPositions[i]=bones[i].localPosition;fromRotations[i]=bones[i].localRotation;}
        }
        public void Restore()
        {
            if(!applied)return;
            root.SetPositionAndRotation(baseRoot,baseFacing);
            for(int i=0;i<bones.Length;i++){bones[i].localPosition=basePositions[i];bones[i].localRotation=baseRotations[i];}
            applied=false;
        }
        public void Clear(){Restore();age=duration;}
        public bool Apply(float dt)
        {
            if(!Active)return false;
            age=Mathf.Min(duration,age+Mathf.Max(0,dt));
            float t=Mathf.SmoothStep(0,1,age/duration);
            // Each boot travels from its real previous position. The forward
            // boot lifts briefly on retraction; a stationary support stays put.
            LeftTarget=Foot(fromLeft,leftFoot.position,t);RightTarget=Foot(fromRight,rightFoot.position,t);
            LeftRotation=Quaternion.Slerp(fromLeftRotation,leftFoot.rotation,t);
            RightRotation=Quaternion.Slerp(fromRightRotation,rightFoot.rotation,t);
            baseRoot=root.position;baseFacing=root.rotation;
            for(int i=0;i<bones.Length;i++){basePositions[i]=bones[i].localPosition;baseRotations[i]=bones[i].localRotation;}
            root.SetPositionAndRotation(Vector3.Lerp(fromRoot,baseRoot,t),Quaternion.Slerp(fromFacing,baseFacing,t));
            for(int i=0;i<bones.Length;i++)
            {bones[i].localPosition=Vector3.Lerp(fromPositions[i],basePositions[i],t);bones[i].localRotation=Quaternion.Slerp(fromRotations[i],baseRotations[i],t);}
            applied=true;return true;
        }
        static Vector3 Foot(Vector3 from,Vector3 to,float t)
        {
            float distance=Vector3.ProjectOnPlane(to-from,Vector3.up).magnitude;
            return Vector3.Lerp(from,to,t)+Vector3.up*(.09f*Mathf.Clamp01(distance/.4f)*Mathf.Sin(t*Mathf.PI));
        }
    }
}
