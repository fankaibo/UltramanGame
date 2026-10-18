using System.Collections.Generic;
using UnityEngine;

namespace UltramanGame.Runtime
{
    public static class TigaLensMesh
    {
        // Smooth only the lens faces, including duplicated seam vertices.
        // Keep the black rim, source positions, UVs, indices and skin weights.
        public static void Apply(Transform owner,Renderer renderer)
        {
            var skin=renderer as SkinnedMeshRenderer;var filter=renderer.GetComponent<MeshFilter>();
            var source=skin?skin.sharedMesh:filter?filter.sharedMesh:null;if(!source)return;
            var materials=renderer.sharedMaterials;int part=-1;
            for(int i=0;i<materials.Length;i++)if(materials[i]&&materials[i].name=="TigaEyesGlow")part=i;
            if(part<0)return;
            var vertices=source.vertices;var normals=source.normals;var selected=new HashSet<int>();
            var sums=new Dictionary<Vector3Int,Vector3>();
            Vector3Int Key(int i)=>Vector3Int.RoundToInt(vertices[i]*100000);
            var triangles=source.GetTriangles(part);
            for(int i=0;i<triangles.Length;i+=3)
            {
                int a=triangles[i],b=triangles[i+1],c=triangles[i+2];var normal=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]);
                foreach(int j in new[]{a,b,c}){selected.Add(j);var key=Key(j);sums.TryGetValue(key,out var sum);sums[key]=sum+normal;}
            }
            for(int partIndex=0;partIndex<source.subMeshCount;partIndex++)if(partIndex!=part)
                foreach(int i in source.GetTriangles(partIndex))selected.Remove(i);
            foreach(int i in selected)if(sums[Key(i)].sqrMagnitude>1e-16f)
            {var normal=sums[Key(i)].normalized;normals[i]=Vector3.Dot(normal,normals[i])<0?-normal:normal;}
            var mesh=RuntimeResources.Own(owner,Object.Instantiate(source));mesh.name=source.name+" lens normals";mesh.normals=normals;
            if(skin)skin.sharedMesh=mesh;else filter.sharedMesh=mesh;
        }
    }
}
