using UnityEngine;

namespace UltramanGame.Runtime
{
    // These bones own the original, rigidly weighted head blades. Moving them
    // keeps the model's mesh, UVs, material and scale, without a second copy.
    public sealed class ZeroSluggerRig
    {
        readonly SkinnedMeshRenderer skin;
        readonly Transform[] bones=new Transform[2];
        readonly Vector3[] centers=new Vector3[2],positions=new Vector3[2],scales=new Vector3[2];
        readonly Quaternion[] rotations=new Quaternion[2];
        readonly Bounds restBounds;
        readonly int[] counts=new int[2];
        public bool Detached {get;private set;}
        public int VertexCount(int i)=>counts[i];
        public Vector3 Center(int i)=>bones[i].TransformPoint(centers[i]);
        public Vector3 MountedCenter(int i)=>bones[i].parent.TransformPoint(positions[i]+rotations[i]*Vector3.Scale(scales[i],centers[i]));
        public Quaternion MountedRotation(int i)=>bones[i].parent.rotation*rotations[i];

        public static ZeroSluggerRig Create(Renderer[] renderers)
        {
            foreach(var renderer in renderers)
            {
                if(!(renderer is SkinnedMeshRenderer skin)||!skin.sharedMesh.isReadable)continue;
                var all=skin.bones;int left=-1,right=-1;
                for(int i=0;i<all.Length;i++)
                {if(all[i].name=="slugger_L")left=i;if(all[i].name=="slugger_R")right=i;}
                if(left<0||right<0)continue;
                var rig=new ZeroSluggerRig(skin,left,right);
                if(rig.counts[0]>0&&rig.counts[1]>0)return rig;
            }
            Debug.LogWarning("[ZeroSlugger] Dedicated rigid head blades unavailable; using fallback effect");return null;
        }
        ZeroSluggerRig(SkinnedMeshRenderer renderer,int left,int right)
        {
            skin=renderer;restBounds=skin.localBounds;
            int[] indices={left,right};var mesh=skin.sharedMesh;var vertices=mesh.vertices;var weights=mesh.boneWeights;var bind=mesh.bindposes;
            bool rigid=true;
            for(int i=0;i<2;i++)
            {
                bones[i]=skin.bones[indices[i]];
                for(int v=0;v<vertices.Length;v++)
                {
                    var w=weights[v];float weight=0;
                    if(w.boneIndex0==indices[i])weight+=w.weight0;if(w.boneIndex1==indices[i])weight+=w.weight1;
                    if(w.boneIndex2==indices[i])weight+=w.weight2;if(w.boneIndex3==indices[i])weight+=w.weight3;
                    if(weight<=0)continue;
                    if(weight<.9999f)rigid=false;
                    centers[i]+=bind[indices[i]].MultiplyPoint3x4(vertices[v]);counts[i]++;
                }
                if(counts[i]>0)centers[i]/=counts[i];
            }
            if(!rigid){counts[0]=counts[1]=0;return;}
            CaptureMountedPose();
        }
        // Restore before the actor samples/blends clips, capture after the body
        // pose is final, then apply flight after both actors have updated.
        public void CaptureMountedPose()
        {
            for(int i=0;i<2;i++)
            {positions[i]=bones[i].localPosition;rotations[i]=bones[i].localRotation;scales[i]=bones[i].localScale;}
        }
        public void Restore()
        {
            if(!Detached)return;
            for(int i=0;i<2;i++)if(bones[i])
            {bones[i].localPosition=positions[i];bones[i].localRotation=rotations[i];bones[i].localScale=scales[i];}
            if(skin)skin.localBounds=restBounds;Detached=false;
        }
        public void Pose(int i,Vector3 center,Quaternion rotation)
        {
            if(!bones[i])return;
            bones[i].rotation=rotation;bones[i].position+=center-Center(i);Detached=true;
            // Keep both original skin and flying blades inside the culling box.
            var bounds=restBounds;var scale=skin.transform.lossyScale;
            float radius=.75f/Mathf.Max(.0001f,Mathf.Min(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z)));
            for(int blade=0;blade<2;blade++)
            {var point=skin.transform.InverseTransformPoint(Center(blade));bounds.Encapsulate(point-Vector3.one*radius);bounds.Encapsulate(point+Vector3.one*radius);}
            skin.localBounds=bounds;
        }
    }
}
