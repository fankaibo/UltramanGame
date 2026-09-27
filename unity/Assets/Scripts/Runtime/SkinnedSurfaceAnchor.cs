using System;
using UnityEngine;

namespace UltramanGame.Runtime
{
    // Keeps one point on the actual deforming torso. Only three cached source
    // vertices are skinned per query; the full mesh is baked once at creation.
    public sealed class SkinnedSurfaceAnchor
    {
        readonly Transform[] bones;
        readonly Matrix4x4[] bindPoses;
        readonly Vector3[] vertices;
        readonly BoneWeight[] weights;
        readonly Vector3 barycentric;
        SkinnedSurfaceAnchor(SkinnedMeshRenderer skin,int a,int b,int c,Vector3 bary)
        {
            bones=skin.bones;bindPoses=skin.sharedMesh.bindposes;barycentric=bary;
            var source=skin.sharedMesh.vertices;var sourceWeights=skin.sharedMesh.boneWeights;
            vertices=new[]{source[a],source[b],source[c]};weights=new[]{sourceWeights[a],sourceWeights[b],sourceWeights[c]};
        }
        public Vector3 Position=>Skin(0)*barycentric.x+Skin(1)*barycentric.y+Skin(2)*barycentric.z;
        Vector3 Skin(int index)
        {
            var w=weights[index];var v=vertices[index];
            Vector3 Part(int bone,float weight)=>weight>0&&bones[bone]?bones[bone].TransformPoint(bindPoses[bone].MultiplyPoint3x4(v))*weight:Vector3.zero;
            return Part(w.boneIndex0,w.weight0)+Part(w.boneIndex1,w.weight1)+Part(w.boneIndex2,w.weight2)+Part(w.boneIndex3,w.weight3);
        }
        public static SkinnedSurfaceAnchor Torso(Renderer[] surfaces,Ray ray)
        {
            SkinnedSurfaceAnchor selected=null;float nearest=float.MaxValue;var baked=new Mesh();
            try
            {
                foreach(var renderer in surfaces)
                {
                    if(!(renderer is SkinnedMeshRenderer skin))continue;
                    var mesh=skin.sharedMesh;if(!mesh||!mesh.isReadable)continue;
                    var weights=mesh.boneWeights;var bones=skin.bones;
                    if(weights.Length!=mesh.vertexCount||mesh.bindposes.Length!=bones.Length)continue;
                    baked.Clear();skin.BakeMesh(baked,true);var vertices=baked.vertices;var triangles=baked.triangles;
                    var chest=new float[vertices.Length];
                    for(int v=0;v<vertices.Length;v++)
                    {
                        vertices[v]=skin.transform.TransformPoint(vertices[v]);var w=weights[v];
                        float Part(int b,float weight)=>weight>0&&bones[b]&&bones[b].name.StartsWith("bip_spine",StringComparison.Ordinal)?weight:0;
                        chest[v]=Part(w.boneIndex0,w.weight0)+Part(w.boneIndex1,w.weight1)+Part(w.boneIndex2,w.weight2)+Part(w.boneIndex3,w.weight3);
                    }
                    for(int i=0;i<triangles.Length;i+=3)
                    {
                        int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                        if(chest[a]+chest[b]+chest[c]<2.2f)continue;
                        if(!Intersect(ray,vertices[a],vertices[b],vertices[c],out float distance,out var bary)||distance>=nearest)continue;
                        nearest=distance;selected=new SkinnedSurfaceAnchor(skin,a,b,c,bary);
                    }
                }
            }
            finally {if(Application.isPlaying)UnityEngine.Object.Destroy(baked);else UnityEngine.Object.DestroyImmediate(baked);}
            if(selected==null)Debug.LogWarning("[BeamSurface] No torso surface found; using the chest-bone fallback");
            else Debug.Log("[BeamSurface] torso-anchor=True vertices=3");
            return selected;
        }
        static bool Intersect(Ray ray,Vector3 a,Vector3 b,Vector3 c,out float distance,out Vector3 barycentric)
        {
            distance=0;barycentric=Vector3.zero;var edge1=b-a;var edge2=c-a;var p=Vector3.Cross(ray.direction,edge2);
            float determinant=Vector3.Dot(edge1,p);if(Mathf.Abs(determinant)<1e-8f)return false;
            float inverse=1/determinant;var t=ray.origin-a;float u=Vector3.Dot(t,p)*inverse;if(u<0||u>1)return false;
            var q=Vector3.Cross(t,edge1);float v=Vector3.Dot(ray.direction,q)*inverse;if(v<0||u+v>1)return false;
            distance=Vector3.Dot(edge2,q)*inverse;barycentric=new Vector3(1-u-v,u,v);return distance>=0;
        }
    }
}
