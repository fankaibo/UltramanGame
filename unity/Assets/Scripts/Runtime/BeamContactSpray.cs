using UnityEngine;
using UnityEngine.Rendering;

namespace UltramanGame.Runtime
{
    // A single reusable mesh, sampled from the contact clock. Crossed ribbons
    // retain thickness from side views without tracking the gameplay camera.
    public sealed class BeamContactSpray
    {
        const int Count=36;
        readonly Mesh mesh;
        readonly MeshRenderer renderer;
        readonly Vector3[] vertices=new Vector3[Count*8];
        readonly Color[] colors=new Color[Count*8];
        readonly Vector3[] velocities=new Vector3[Count];
        Vector3 origin,axis,side,up;
        public int ActiveCount {get;private set;}
        public bool Visible=>renderer.enabled;
        public BeamContactSpray(Transform parent)
        {
            var node=new GameObject("Beam contact energetic spray");node.transform.SetParent(parent,false);
            mesh=RuntimeResources.Own(parent,new Mesh{name="Pooled beam contact ribbons"});mesh.MarkDynamic();
            node.AddComponent<MeshFilter>().sharedMesh=mesh;renderer=node.AddComponent<MeshRenderer>();
            renderer.sharedMaterial=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("BeamContactSpray")));
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            var uv=new Vector2[vertices.Length];var triangles=new int[Count*12];
            for(int ribbon=0;ribbon<Count*2;ribbon++)
            {
                int v=ribbon*4,t=ribbon*6;
                uv[v]=new Vector2(0,0);uv[v+1]=new Vector2(0,1);uv[v+2]=new Vector2(1,0);uv[v+3]=new Vector2(1,1);
                triangles[t]=v;triangles[t+1]=v+1;triangles[t+2]=v+2;
                triangles[t+3]=v+2;triangles[t+4]=v+1;triangles[t+5]=v+3;
            }
            mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;Clear();
        }
        public void Hit(Vector3 contact,Vector3 incoming)
        {
            axis=incoming.sqrMagnitude>.0001f?incoming.normalized:Vector3.forward;
            side=Vector3.Cross(Vector3.up,axis);
            if(side.sqrMagnitude<.001f)side=Vector3.Cross(Vector3.right,axis);
            side.Normalize();up=Vector3.Cross(axis,side).normalized;
            origin=contact-axis*.10f;
            for(int i=0;i<Count;i++)
            {
                float a=i*2.399963f,speed=1.8f+(i%7)*.34f;
                velocities[i]=(side*Mathf.Cos(a)+up*Mathf.Sin(a))*speed-axis*(.75f+(i%4)*.28f)+Vector3.up*.20f;
            }
            Sample(0);
        }
        public void Sample(float age)
        {
            ActiveCount=0;
            for(int i=0;i<Count;i++)
            {
                float t=age-(i%6)*.035f,life=.34f+(i%5)*.065f;
                bool live=t>0&&t<life;
                float opacity=live?Mathf.SmoothStep(0,1,t/.025f)*(1-Mathf.SmoothStep(.30f,1,t/life)):0;
                if(live)ActiveCount++;
                float head=Mathf.Max(0,t),tail=Mathf.Max(0,t-(.065f+(i%3)*.02f));
                var a=origin+velocities[i]*tail+Vector3.down*(3.2f*tail*tail);
                var b=origin+velocities[i]*head+Vector3.down*(3.2f*head*head);
                var tangent=b-a;var width=Vector3.Cross(tangent,axis);
                if(width.sqrMagnitude<.00001f)width=side;
                width=width.normalized*(.035f+(i%3)*.009f)*(1-.55f*Mathf.Clamp01(t/life));
                var cross=Vector3.Cross(tangent,width).normalized*width.magnitude;
                for(int r=0;r<2;r++)
                {
                    int v=i*8+r*4;var offset=r==0?width:cross;
                    vertices[v]=a-offset;vertices[v+1]=a+offset;vertices[v+2]=b-offset;vertices[v+3]=b+offset;
                    for(int j=0;j<4;j++)colors[v+j]=new Color(1,1,1,opacity);
                }
            }
            renderer.enabled=ActiveCount>0;
            if(!renderer.enabled)return;
            mesh.vertices=vertices;mesh.colors=colors;mesh.RecalculateBounds();
        }
        public void Clear(){ActiveCount=0;renderer.enabled=false;}
    }
}
