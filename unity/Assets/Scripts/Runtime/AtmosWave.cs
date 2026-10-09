using UnityEngine;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    // Moving pressure fronts, not independent projectiles. All fronts share
    // Battle's one hit; their positions can be sampled repeatedly without drift.
    public sealed class AtmosWave
    {
        const int Count=12;
        const float Interval=.075f, Dissolve=.20f;
        readonly Renderer[] rings=new Renderer[Count];
        readonly MaterialPropertyBlock properties=new MaterialPropertyBlock();
        readonly Material material;
        public bool Visible {get;private set;}
        public int ActiveRings {get;private set;}
        public Vector3 Origin {get;private set;}
        public Vector3 FirstFront {get;private set;}
        public AtmosWave(Transform parent)
        {
            material=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("AtmosWave")));
            var mesh=RuntimeResources.Own(parent,CreateRing());
            for(int i=0;i<Count;i++)
            {
                var obj=new GameObject("Atmos pressure front "+i);obj.transform.SetParent(parent,false);
                obj.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
                rings[i]=renderer;
            }
            Clear();
        }
        static Mesh CreateRing()
        {
            const int segments=64,sides=10;
            var vertices=new Vector3[(segments+1)*(sides+1)];var uv=new Vector2[vertices.Length];
            var triangles=new int[segments*sides*6];int at=0;
            for(int i=0;i<=segments;i++)for(int j=0;j<=sides;j++)
            {
                float a=i*2*Mathf.PI/segments,b=j*2*Mathf.PI/sides;
                int k=i*(sides+1)+j;float radius=1+.085f*Mathf.Cos(b);
                vertices[k]=new Vector3(radius*Mathf.Cos(a),radius*Mathf.Sin(a),.085f*Mathf.Sin(b));
                uv[k]=new Vector2(i/(float)segments,j/(float)sides);
                if(i==segments||j==sides)continue;
                int next=k+sides+1;
                triangles[at++]=k;triangles[at++]=next;triangles[at++]=k+1;
                triangles[at++]=k+1;triangles[at++]=next;triangles[at++]=next+1;
            }
            var mesh=new Mesh{name="Atmos pressure torus"};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateBounds();return mesh;
        }
        public void Clear(){foreach(var ring in rings)ring.enabled=false;Visible=false;ActiveRings=0;}
        public void Sample(bool active,Vector3 origin,Vector3 target,float age,Color tint,Color accent)
        {
            if(!active){Clear();return;}
            Origin=origin;FirstFront=Vector3.Lerp(origin,target,BeamStream.Travel(age));ActiveRings=0;
            var direction=(target-origin).normalized;
            var rotation=Quaternion.LookRotation(direction.sqrMagnitude>.01f?direction:Vector3.forward);
            float flight=Battle.BeamHitSeconds-BeamStream.LaunchSeconds;
            for(int i=0;i<Count;i++)
            {
                float elapsed=age-BeamStream.LaunchSeconds-i*Interval;
                var ring=rings[i];ring.enabled=elapsed>0&&elapsed<flight+Dissolve&&age<Battle.BeamSeconds;
                if(!ring.enabled)continue;
                ActiveRings++;float travel=Mathf.Clamp01(elapsed/flight),contact=Mathf.Clamp01((elapsed-flight)/Dissolve);
                ring.transform.SetPositionAndRotation(Vector3.Lerp(origin,target,travel)-direction*(contact*.02f),rotation);
                float radius=Mathf.Lerp(.17f,.68f,travel)+contact*.40f;
                ring.transform.localScale=Vector3.one*radius;
                properties.SetColor("_Tint",tint);properties.SetColor("_Accent",accent);
                properties.SetFloat("_Phase",elapsed*9+i*.71f);
                properties.SetFloat("_Power",Mathf.SmoothStep(0,1,elapsed/.025f)*(1-Mathf.SmoothStep(0,1,contact)));
                ring.SetPropertyBlock(properties);
            }
            Visible=ActiveRings>0;
        }
    }
}
