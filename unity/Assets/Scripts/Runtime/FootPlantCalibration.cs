using UnityEngine;

namespace UltramanGame.Runtime
{
    // Calibrate once from the imported, skinned boot. Foot-bone axes differ
    // between models and an ankle on the floor does not imply a planted sole.
    public static class FootPlantCalibration
    {
        static bool[] Vertices(SkinnedMeshRenderer skin,Transform foot)
        {
            var bones=skin.bones;var weights=skin.sharedMesh.boneWeights;var selected=new bool[weights.Length];
            float Weight(int index,float value)=>(bones[index]==foot||bones[index].IsChildOf(foot))?value:0;
            for(int i=0;i<weights.Length;i++)
            {
                var w=weights[i];selected[i]=Weight(w.boneIndex0,w.weight0)+Weight(w.boneIndex1,w.weight1)+Weight(w.boneIndex2,w.weight2)+Weight(w.boneIndex3,w.weight3)>.5f;
            }
            return selected;
        }
        public static void Apply(Transform root,Transform foot,Renderer[] surfaces,ref Vector3 anchor,ref Quaternion rotation)
        {
            if(!foot)return;
            var mesh=new Mesh();var original=foot.rotation;Vector3 normal=Vector3.zero;float area=0;
            try
            {
                foreach(var surface in surfaces)
                {
                    if(!(surface is SkinnedMeshRenderer skin))continue;
                    var selected=Vertices(skin,foot);skin.BakeMesh(mesh,true);var v=mesh.vertices;var indices=mesh.triangles;
                    for(int i=0;i<indices.Length;i+=3)
                    {
                        int a=indices[i],b=indices[i+1],c=indices[i+2];if(!selected[a]||!selected[b]||!selected[c])continue;
                        var p=skin.transform.TransformPoint(v[a]);var q=skin.transform.TransformPoint(v[b]);var r=skin.transform.TransformPoint(v[c]);
                        Vector3 cross=Vector3.Cross(q-p,r-p);float magnitude=cross.magnitude;if(magnitude<.000001f)continue;
                        // Reject the ankle, side walls and toe cap. The broad
                        // underside supplies the support plane, including roll.
                        if(cross.y/magnitude>-.75f||(p.y+q.y+r.y)/3>foot.position.y-.015f)continue;
                        normal+=cross;area+=magnitude;
                    }
                }
                if(area<.002f||normal.magnitude<.001f)return;
                foot.rotation=Quaternion.FromToRotation(normal.normalized,Vector3.down)*original;
                float bottom=float.PositiveInfinity;
                foreach(var surface in surfaces)
                {
                    if(!(surface is SkinnedMeshRenderer skin))continue;
                    var selected=Vertices(skin,foot);skin.BakeMesh(mesh,true);var v=mesh.vertices;
                    for(int i=0;i<selected.Length;i++)if(selected[i])bottom=Mathf.Min(bottom,skin.transform.TransformPoint(v[i]).y);
                }
                if(float.IsInfinity(bottom))return;
                rotation=Quaternion.Inverse(root.rotation)*foot.rotation;
                // The arena's flat floor is at -0.012. Leave a small mesh
                // clearance, without burying the toe to hide a wrong rotation.
                anchor.y+=root.position.y+.002f-bottom;
            }
            finally
            {
                foot.rotation=original;
                if(Application.isPlaying)Object.Destroy(mesh);else Object.DestroyImmediate(mesh);
            }
        }
    }
}
