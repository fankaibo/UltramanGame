using System.Collections.Generic;
using UnityEngine;

namespace UltramanGame.Runtime
{
    // Sample the imported boot once. The ankle and local axes alone do not
    // identify its sole or toe; all per-frame motion uses this cached geometry.
    public sealed class FootContactRoll
    {
        readonly Transform foot;
        readonly Vector3 toe;
        readonly Vector3[] points;
        public FootContactRoll(Transform foot,Renderer[] surfaces,Vector3 forward)
        {
            this.foot=foot;var samples=new List<Vector3>();var mesh=new Mesh();
            try
            {
                foreach(var renderer in surfaces)
                {
                    if(!(renderer is SkinnedMeshRenderer skin)||!foot)continue;
                    var bones=skin.bones;var weights=skin.sharedMesh.boneWeights;skin.BakeMesh(mesh,true);var vertices=mesh.vertices;
                    float Weight(int index,float value)=>(bones[index]==foot||bones[index].IsChildOf(foot))?value:0;
                    for(int i=0;i<weights.Length;i++)
                    {
                        var w=weights[i];if(Weight(w.boneIndex0,w.weight0)+Weight(w.boneIndex1,w.weight1)+Weight(w.boneIndex2,w.weight2)+Weight(w.boneIndex3,w.weight3)<.95f)continue;
                        samples.Add(foot.InverseTransformPoint(skin.transform.TransformPoint(vertices[i])));
                    }
                }
                points=samples.ToArray();float floor=float.PositiveInfinity,front=float.NegativeInfinity;
                foreach(var p in points)floor=Mathf.Min(floor,foot.TransformPoint(p).y);
                foreach(var p in points){var world=foot.TransformPoint(p);if(world.y<floor+.025f)front=Mathf.Max(front,Vector3.Dot(world,forward));}
                Vector3 sum=Vector3.zero;int count=0;
                foreach(var p in points){var world=foot.TransformPoint(p);if(world.y<floor+.025f&&Vector3.Dot(world,forward)>front-.025f){sum+=p;count++;}}
                toe=count>0?sum/count:Vector3.zero;
            }
            finally{if(Application.isPlaying)Object.Destroy(mesh);else Object.DestroyImmediate(mesh);}
        }
        public void Target(float angle,Vector3 axis,out Vector3 ankle,out Quaternion rotation)
        {
            ankle=foot.position;rotation=foot.rotation;if(points.Length==0||angle<=0)return;
            var turn=Quaternion.AngleAxis(angle,axis);var pivot=foot.TransformPoint(toe);
            float before=float.PositiveInfinity,after=float.PositiveInfinity;
            foreach(var p in points)
            {var world=foot.TransformPoint(p);before=Mathf.Min(before,world.y);after=Mathf.Min(after,(pivot+turn*(world-pivot)).y);}
            ankle=pivot+turn*(ankle-pivot)+Vector3.up*Mathf.Max(0,before-after);
            rotation=turn*rotation;
        }
    }
}
