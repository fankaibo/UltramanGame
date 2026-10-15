using System.Collections.Generic;
using UnityEngine;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    // Capture the actual collapsed skin once, then let the same explicit
    // victory clock drive surface erosion and departing light. A new round
    // clears both; there are no delayed callbacks or free-running particles.
    public sealed class MonsterDissolve
    {
        const int Capacity=384;
        readonly Transform actor;
        readonly SkinnedMeshRenderer[] skins;
        readonly Material[] surfaces;
        readonly Mesh mesh,baked;
        readonly MeshRenderer renderer;
        readonly Material motes;
        readonly Light light;
        readonly float[] births=new float[Capacity],lifetimes=new float[Capacity];
        bool captured;
        public int Starts {get;private set;}
        public int ActiveMotes {get;private set;}
        public float Progress {get;private set;}
        public Vector2 HeightBounds {get;private set;}
        public MonsterDissolve(Transform parent,Renderer[] body)
        {
            actor=parent;var materials=new List<Material>();var meshes=new List<SkinnedMeshRenderer>();
            foreach(var r in body)
            {
                if(r is SkinnedMeshRenderer skin)meshes.Add(skin);
                foreach(var mat in r.sharedMaterials)if(mat.HasProperty("_DissolveAmount")&&!materials.Contains(mat))materials.Add(mat);
            }
            surfaces=materials.ToArray();skins=meshes.ToArray();
            var go=new GameObject("Departing monster light",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            mesh=RuntimeResources.Own(parent,new Mesh{name="Collapsed skin light origins"});
            baked=RuntimeResources.Own(parent,new Mesh{name="Dissolve skin sample"});
            go.GetComponent<MeshFilter>().sharedMesh=mesh;renderer=go.GetComponent<MeshRenderer>();
            motes=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("DissolveMotes")));
            renderer.sharedMaterial=motes;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.enabled=false;
            light=new GameObject("Departure glow").AddComponent<Light>();light.transform.SetParent(parent,false);
            light.type=LightType.Point;light.color=new Color(1,.64f,.24f);light.range=5;light.intensity=0;
        }
        public static float Field(Vector3 point,Vector2 bounds)
        {
            float height=Mathf.Clamp01((point.y-bounds.x)/Mathf.Max(.01f,bounds.y));
            float large=.5f+.5f*Mathf.Sin(point.x*11.7f+Mathf.Sin(point.z*7.3f)*2+point.y*4.7f);
            float small=.5f+.5f*Mathf.Sin(point.z*29.1f+point.x*21.3f+Mathf.Sin(point.y*18.7f));
            return .02f+height*.64f+large*.22f+small*.10f;
        }
        static float Birth(float field)
        {
            float lo=0,hi=1;
            for(int i=0;i<18;i++){float mid=(lo+hi)*.5f;if(Mathf.SmoothStep(0,1,mid)<field)lo=mid;else hi=mid;}
            return VictoryMotion.FadeStartSeconds+(lo+hi)*.5f*VictoryMotion.FadeSeconds;
        }
        void Capture()
        {
            var points=new List<Vector3>();var normals=new List<Vector3>();float minimum=100,maximum=-100;
            foreach(var skin in skins)
            {
                skin.BakeMesh(baked,true);var vertices=baked.vertices;var directions=baked.normals;
                for(int i=0;i<vertices.Length;i++)
                {
                    var p=skin.transform.TransformPoint(vertices[i]);points.Add(p);
                    normals.Add(skin.transform.TransformDirection(directions[i]).normalized);
                    minimum=Mathf.Min(minimum,p.y);maximum=Mathf.Max(maximum,p.y);
                }
            }
            if(points.Count==0)return;
            HeightBounds=new Vector2(minimum,maximum-minimum);
            var v=new Vector3[Capacity*4];var n=new Vector3[v.Length];var uv=new Vector2[v.Length];var clocks=new Vector2[v.Length];var sizes=new Vector2[v.Length];var triangles=new int[Capacity*6];
            var random=new System.Random(260929);var selected=new HashSet<int>();
            for(int i=0;i<Capacity;i++)
            {
                int at=random.Next(points.Count);while(selected.Contains(at)&&selected.Count<points.Count)at=(at+1)%points.Count;selected.Add(at);
                var p=points[at];births[i]=Birth(Field(p,HeightBounds));lifetimes[i]=.70f+(float)random.NextDouble()*.36f;
                Vector3 velocity=normals[at]*.20f+Vector3.up*(.50f+(float)random.NextDouble()*.35f);
                float seed=(float)random.NextDouble()*6.28f,size=.014f+(float)random.NextDouble()*.024f;
                for(int corner=0;corner<4;corner++)
                {int k=i*4+corner;v[k]=actor.InverseTransformPoint(p);n[k]=velocity;uv[k]=new Vector2(corner%2,corner/2);clocks[k]=new Vector2(births[i],lifetimes[i]);sizes[k]=new Vector2(seed,size);}
                int t=i*6,b=i*4;triangles[t]=b;triangles[t+1]=b+1;triangles[t+2]=b+2;triangles[t+3]=b+1;triangles[t+4]=b+3;triangles[t+5]=b+2;
            }
            mesh.Clear();mesh.vertices=v;mesh.normals=n;mesh.uv=uv;mesh.uv2=clocks;mesh.uv3=sizes;mesh.triangles=triangles;mesh.RecalculateBounds();var bound=mesh.bounds;bound.Expand(5);mesh.bounds=bound;
            foreach(var mat in surfaces)mat.SetVector("_DissolveBounds",new Vector4(minimum,maximum-minimum,0,0));
            light.transform.position=new Vector3(actor.position.x,(minimum+maximum)*.5f,actor.position.z);
            captured=true;Starts++;
            if(Debug.isDebugBuild)Debug.Log($"[MonsterDissolve] begin samples={Capacity} surfaces={surfaces.Length} height={maximum-minimum:F3}");
        }
        public void Tick(float victoryAge)
        {
            if(victoryAge<VictoryMotion.FadeStartSeconds)
            {
                captured=false;Progress=0;ActiveMotes=0;renderer.enabled=false;light.intensity=0;
                foreach(var mat in surfaces)mat.SetFloat("_DissolveAmount",0);
                return;
            }
            if(!captured)Capture();
            Progress=1-VictoryMotion.Opacity(victoryAge);ActiveMotes=0;
            foreach(var mat in surfaces)mat.SetFloat("_DissolveAmount",Progress);
            for(int i=0;i<Capacity;i++)if(victoryAge>=births[i]&&victoryAge<births[i]+lifetimes[i])ActiveMotes++;
            renderer.enabled=captured&&ActiveMotes>0;motes.SetFloat("_Age",victoryAge);
            light.intensity=Mathf.Sin(Progress*Mathf.PI)*.75f;
        }
    }
}
