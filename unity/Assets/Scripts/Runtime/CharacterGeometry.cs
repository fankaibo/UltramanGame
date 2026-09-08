using System;
using System.Collections.Generic;
using UnityEngine;

namespace UltramanGame.Runtime
{
    // Editable mesh construction: actual closed surfaces, independent of the pose recognizer.
    public static class CharacterGeometry
    {
        public struct Ring
        {
            public float Y,X,Z,Offset;
            public Ring(float y,float x,float z,float offset=0) {Y=y;X=x;Z=z;Offset=offset;}
        }
        public static Transform MeshObject(Transform parent,string name,Mesh mesh,params Material[] materials)
        {
            var obj=new GameObject(name);obj.transform.SetParent(parent,false);
            obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterials=materials;
            return obj.transform;
        }
        public static Transform Loft(Transform parent,string name,Ring[] rings,Material[] materials,
            Func<float,float,int> paint=null,float roughness=0,int sides=48,int subdivisions=3)
        {
            int rows=(rings.Length-1)*subdivisions+1;
            var vertices=new Vector3[rows*(sides+1)];var uv=new Vector2[vertices.Length];
            var triangles=new List<int>[materials.Length];for(int i=0;i<triangles.Length;i++)triangles[i]=new List<int>();
            for(int r=0;r<rows;r++)
            {
                float f=r/(float)subdivisions;int k=Mathf.Min(rings.Length-2,(int)f);float t=f-k;
                var a=rings[k];var b=rings[k+1];
                float y=Mathf.Lerp(a.Y,b.Y,t),rx=Mathf.Lerp(a.X,b.X,t),rz=Mathf.Lerp(a.Z,b.Z,t),z=Mathf.Lerp(a.Offset,b.Offset,t);
                for(int s=0;s<=sides;s++)
                {
                    float angle=s*2*Mathf.PI/sides,x=Mathf.Sin(angle),c=Mathf.Cos(angle);
                    float noise=1+roughness*(Mathf.PerlinNoise(x*8+y*3,c*8+y*5)-.5f);
                    int i=r*(sides+1)+s;vertices[i]=new Vector3(x*rx*noise,y,c*rz*noise+z);uv[i]=new Vector2(s/(float)sides,r/(float)(rows-1));
                    if(r==rows-1||s==sides)continue;
                    int slot=paint==null?0:paint((r+.5f)/(rows-1),(s+.5f)*2*Mathf.PI/sides);
                    int n=i+sides+1;triangles[slot].AddRange(new[]{i,i+1,n,i+1,n+1,n});
                }
            }
            // Close both ends so bent joints cannot expose an empty tube.
            int bottom=vertices.Length,top=bottom+1;Array.Resize(ref vertices,vertices.Length+2);Array.Resize(ref uv,uv.Length+2);
            vertices[bottom]=new Vector3(0,rings[0].Y,rings[0].Offset);vertices[top]=new Vector3(0,rings[rings.Length-1].Y,rings[rings.Length-1].Offset);
            for(int s=0;s<sides;s++) {int last=(rows-1)*(sides+1);triangles[0].AddRange(new[]{bottom,s+1,s,top,last+s,last+s+1});}
            var mesh=new Mesh {name=name,vertices=vertices,uv=uv,subMeshCount=materials.Length};
            for(int i=0;i<triangles.Length;i++)mesh.SetTriangles(triangles[i],i);
            mesh.RecalculateNormals();var normals=mesh.normals;
            for(int r=0;r<rows;r++)
            { int i=r*(sides+1);var n=(normals[i]+normals[i+sides]).normalized;normals[i]=normals[i+sides]=n; }
            mesh.normals=normals;mesh.RecalculateTangents();mesh.RecalculateBounds();
            return MeshObject(parent,name,mesh,materials);
        }
        public static Transform Ellipsoid(Transform parent,string name,Vector3 position,Vector3 radius,Material material,float roughness=0)
        {
            var rings=new Ring[17];
            for(int i=0;i<rings.Length;i++)
            { float angle=i*Mathf.PI/(rings.Length-1);float sin=Mathf.Max(.001f,Mathf.Sin(angle));rings[i]=new Ring(-Mathf.Cos(angle)*radius.y,sin*radius.x,sin*radius.z); }
            var obj=Loft(parent,name,rings,new[]{material},null,roughness,32,1);obj.localPosition=position;return obj;
        }
        // A beveled raised polygon. Outline is counterclockwise when viewed from +Z.
        public static Transform Plate(Transform parent,string name,Vector3[] outline,float depth,Material material)
        {
            int n=outline.Length;var vertices=new List<Vector3>();var indices=new List<int>();var center=Vector3.zero;
            foreach(var p in outline)center+=p;center/=n;
            vertices.Add(center+Vector3.forward*depth);
            for(int i=0;i<n;i++)vertices.Add(Vector3.Lerp(outline[i],center,.12f)+Vector3.forward*depth);
            for(int i=0;i<n;i++)vertices.Add(outline[i]);
            int back=vertices.Count;vertices.Add(center);
            for(int i=0;i<n;i++)
            { int a=1+i,b=1+(i+1)%n,c=1+n+i,d=1+n+(i+1)%n;indices.AddRange(new[]{0,a,b,a,c,d,a,d,b,back,d,c}); }
            var mesh=new Mesh {name=name,vertices=vertices.ToArray(),triangles=indices.ToArray()};mesh.RecalculateNormals();mesh.RecalculateBounds();
            return MeshObject(parent,name,mesh,material);
        }
        public static Transform Tube(Transform parent,string name,Vector3[] path,float[] radii,Material material,int sides=12)
        {
            var vertices=new Vector3[path.Length*(sides+1)];var uv=new Vector2[vertices.Length];var triangles=new List<int>();
            for(int r=0;r<path.Length;r++)
            {
                Vector3 direction=path[Mathf.Min(r+1,path.Length-1)]-path[Mathf.Max(0,r-1)];
                var rotation=Quaternion.FromToRotation(Vector3.up,direction);
                for(int s=0;s<=sides;s++)
                {
                    float a=s*2*Mathf.PI/sides;int i=r*(sides+1)+s;
                    vertices[i]=path[r]+rotation*new Vector3(Mathf.Sin(a)*radii[r],0,Mathf.Cos(a)*radii[r]);uv[i]=new Vector2(s/(float)sides,r/(float)(path.Length-1));
                    if(r==path.Length-1||s==sides)continue;int next=i+sides+1;triangles.AddRange(new[]{i,i+1,next,i+1,next+1,next});
                }
            }
            var mesh=new Mesh {name=name,vertices=vertices,uv=uv,triangles=triangles.ToArray()};mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            return MeshObject(parent,name,mesh,material);
        }
        public static Transform Ribbon(Transform parent,string name,Vector3[] path,float width,Material material)
        {
            var vertices=new Vector3[path.Length*2];var triangles=new List<int>();
            for(int i=0;i<path.Length;i++)
            {
                vertices[i*2]=path[i]+Vector3.up*width*.5f;vertices[i*2+1]=path[i]-Vector3.up*width*.5f;
                if(i==path.Length-1)continue;int n=i*2;
                bool forward=Vector3.Cross(vertices[n+1]-vertices[n],path[i+1]-path[i]).z*Mathf.Sign(path[i].z)>0;
                if(forward)triangles.AddRange(new[]{n,n+1,n+2,n+1,n+3,n+2});
                else triangles.AddRange(new[]{n,n+2,n+1,n+1,n+2,n+3});
            }
            var mesh=new Mesh {name=name,vertices=vertices,triangles=triangles.ToArray()};mesh.RecalculateNormals();mesh.RecalculateBounds();
            return MeshObject(parent,name,mesh,material);
        }
        // Collapse details on one rigid bone by material; preserve the articulated parent.
        public static void Combine(Transform root)
        {
            var groups=new Dictionary<Material,List<CombineInstance>>();var sources=root.GetComponentsInChildren<MeshFilter>();
            foreach(var filter in sources)
            {
                var renderer=filter.GetComponent<MeshRenderer>();
                for(int s=0;s<filter.sharedMesh.subMeshCount;s++)
                {
                    var material=renderer.sharedMaterials[s];if(!groups.ContainsKey(material))groups[material]=new List<CombineInstance>();
                    groups[material].Add(new CombineInstance {mesh=filter.sharedMesh,subMeshIndex=s,transform=root.worldToLocalMatrix*filter.transform.localToWorldMatrix});
                }
            }
            foreach(var pair in groups)
            {
                var mesh=new Mesh {name=root.name+" / "+pair.Key.name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray(),true,true);
                MeshObject(root,mesh.name,mesh,pair.Key);
            }
            foreach(var source in sources)
            {
                var mesh=source.sharedMesh;source.gameObject.SetActive(false);
                if(Application.isPlaying) {UnityEngine.Object.Destroy(source.gameObject);UnityEngine.Object.Destroy(mesh);}
                else {UnityEngine.Object.DestroyImmediate(source.gameObject);UnityEngine.Object.DestroyImmediate(mesh);}
            }
        }
    }
}
