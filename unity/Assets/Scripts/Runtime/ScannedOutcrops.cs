using UnityEngine;

namespace UltramanGame.Runtime
{
    // The large silhouettes use real scanned geometry with its matching UV maps.
    // Small scree keeps the cheaper shared stage treatment.
    public sealed class ScannedOutcrops
    {
        readonly Transform parent;
        readonly GameObject[] sources=new GameObject[2];
        readonly Material[] materials=new Material[2];
        public int Count {get;private set;}
        public int Triangles {get;private set;}
        public ScannedOutcrops(Transform parent)
        {
            this.parent=parent;
            string[] names={"rock_07","rock_09"};
            var shader=Resources.Load<Shader>("ScannedRock");
            for(int i=0;i<names.Length;i++)
            {
                string path="Environment/ScannedRocks/"+names[i];
                sources[i]=Resources.Load<GameObject>(path+"_2k");
                if(!sources[i]||!shader)continue;
                var mat=RuntimeResources.Own(parent,new Material(shader){name=names[i]+" volcanic scan"});
                mat.mainTexture=Resources.Load<Texture2D>(path+"_diff_2k");
                mat.SetTexture("_Normal",Resources.Load<Texture2D>(path+"_nor_gl_2k"));
                mat.SetTexture("_ARM",Resources.Load<Texture2D>(path+"_arm_2k"));
                materials[i]=mat;
            }
        }
        public bool Place(Vector3 position,Vector3 size,float yaw)
        {
            if(Mathf.Max(size.x,size.z)<.70f)return false;
            int kind=Count%2;
            if(!sources[kind]||!materials[kind])return false;
            var root=new GameObject(kind==0?"Scanned volcanic boulder":"Scanned fractured outcrop").transform;
            root.SetParent(parent,false);
            var model=Object.Instantiate(sources[kind],root,false);
            // Source files may contain multiple authored LOD meshes. Use one
            // highest-detail surface, never overlapping all their silhouettes.
            MeshFilter selected=null;
            foreach(var mesh in model.GetComponentsInChildren<MeshFilter>(true))
                if(mesh.sharedMesh&&(!selected||mesh.sharedMesh.vertexCount>selected.sharedMesh.vertexCount))selected=mesh;
            if(!selected){Object.Destroy(model);Object.Destroy(root.gameObject);return false;}
            foreach(var lod in model.GetComponentsInChildren<LODGroup>(true))lod.enabled=false;
            foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))renderer.enabled=renderer.gameObject==selected.gameObject;
            var surface=selected.GetComponent<Renderer>();
            if(!surface){Object.Destroy(root.gameObject);return false;}
            selected.gameObject.SetActive(true);
            var mapped=new Material[selected.sharedMesh.subMeshCount];
            for(int i=0;i<mapped.Length;i++)mapped[i]=materials[kind];surface.sharedMaterials=mapped;
            Bounds bounds=surface.bounds;
            // Retain each scan's real proportions instead of flattening it to
            // the old procedural block. The base is slightly buried in ash.
            float width=Mathf.Max(bounds.size.x,bounds.size.z);
            float scale=Mathf.Max(size.x,size.z)*1.75f/Mathf.Max(.001f,width);
            model.transform.position-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            root.localScale=Vector3.one*scale;
            root.localRotation=Quaternion.Euler(0,yaw,0);
            root.localPosition=position-Vector3.up*.06f;
            Count++;for(int i=0;i<selected.sharedMesh.subMeshCount;i++)Triangles+=(int)selected.sharedMesh.GetIndexCount(i)/3;
            return true;
        }
    }
}
