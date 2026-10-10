using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace UltramanGame.Runtime
{
    // Two fixed meshes share the stage clock. No physics objects, per-frame
    // allocations or particle-system clocks that keep running during a pause.
    public sealed class VolcanicEjecta
    {
        const int BombCount=64,SparkCount=80,FaceVertices=60;
        const float Gravity=5.4f,Cycle=3.4f,SettleDuration=1.05f;
        struct Flight
        {
            public Vector3 Origin,Velocity,Axis,GroundNormal;
            public float Offset,Size,Spin,Landing;
            public Vector3 Point(float age)=>Origin+Velocity*age+Vector3.down*(Gravity*.5f*age*age);
        }
        readonly Flight[] bombs=new Flight[BombCount],sparks=new Flight[SparkCount];
        readonly Func<float,float,float> groundHeight;
        readonly Mesh rockMesh,sparkMesh;
        readonly Vector3[] rockVertices=new Vector3[BombCount*FaceVertices],rockNormals=new Vector3[BombCount*FaceVertices];
        readonly Vector3[] rockShape=new Vector3[BombCount*FaceVertices],rockShapeNormals=new Vector3[BombCount*FaceVertices];
        readonly Color[] rockColors=new Color[BombCount*FaceVertices];
        readonly Vector3[] sparkVertices=new Vector3[(BombCount+SparkCount)*4];
        readonly Color[] sparkColors=new Color[(BombCount+SparkCount)*4];
        public int ActiveBombs {get;private set;}
        public int ActiveSparks {get;private set;}
        public int LandedBombs {get;private set;}
        public float PeakHeat {get;private set;}
        public VolcanicEjecta(Transform parent,Vector3[] vents,Func<float,float,float> height)
        {
            groundHeight=height;
            var random=new System.Random(944);
            float Pick(float a,float b)=>Mathf.Lerp(a,b,(float)random.NextDouble());
            Flight Create(int i,bool spark)
            {
                float angle=Pick(0,Mathf.PI*2),spread=Pick(spark?.18f:.35f,spark?.75f:1.05f);
                var origin=vents[i%vents.Length];origin.y=height(origin.x,origin.z)+.09f;
                var f=new Flight {Origin=origin,Velocity=new Vector3(Mathf.Sin(angle)*spread,Pick(3.2f,4.8f),Mathf.Cos(angle)*spread*.7f),
                    Axis=new Vector3(Pick(-1,1),Pick(-1,1),Pick(-1,1)).normalized,
                    Offset=Pick(0,Cycle),Size=Pick(spark?.015f:.045f,spark?.028f:.085f),Spin=Pick(170,390)};
                // Find the actual terrain intersection once; falling rocks do
                // not disappear in mid-air or keep glowing beneath a slope.
                float lo=.02f,hi=2.8f;
                for(int n=0;n<22;n++)
                {float t=(lo+hi)*.5f;var p=f.Point(t);if(p.y>height(p.x,p.z)+.015f)lo=t;else hi=t;}
                f.Landing=(lo+hi)*.5f;return f;
            }
            float golden=(1+Mathf.Sqrt(5))*.5f;
            var corners=new[]{new Vector3(-1,golden,0),new Vector3(1,golden,0),new Vector3(-1,-golden,0),new Vector3(1,-golden,0),
                new Vector3(0,-1,golden),new Vector3(0,1,golden),new Vector3(0,-1,-golden),new Vector3(0,1,-golden),
                new Vector3(golden,0,-1),new Vector3(golden,0,1),new Vector3(-golden,0,-1),new Vector3(-golden,0,1)};
            int[] faces={0,11,5,0,5,1,0,1,7,0,7,10,0,10,11,1,5,9,5,11,4,11,10,2,10,7,6,7,1,8,
                3,9,4,3,4,2,3,2,6,3,6,8,3,8,9,4,9,5,2,4,11,6,2,10,8,6,7,9,8,1};
            var uv=new Vector2[rockVertices.Length];var triangles=new int[rockVertices.Length];
            for(int i=0;i<BombCount;i++)
            {
                bombs[i]=Create(i,false);
                var shape=new Vector3[corners.Length];
                for(int n=0;n<shape.Length;n++)shape[n]=Vector3.Scale(corners[n].normalized*Pick(.82f,1.13f),new Vector3(1,.78f,1.14f));
                for(int face=0;face<20;face++)
                {
                    Vector3 a=shape[faces[face*3]],b=shape[faces[face*3+1]],c=shape[faces[face*3+2]];
                    Vector3 normal=Vector3.Cross(b-a,c-a).normalized;
                    for(int v=0;v<3;v++)
                    {
                        int at=i*FaceVertices+face*3+v;var point=shape[faces[face*3+v]];
                        rockShape[at]=point;rockShapeNormals[at]=normal;
                        uv[at]=new Vector2(point.x*.5f+.5f,point.y*.5f+.5f);triangles[at]=at;
                    }
                }
                // Solve contact for the rotating rock's underside, not its
                // center. The same support plane is used after the first touch.
                var f=bombs[i];float lo=.02f,hi=2.8f;
                for(int n=0;n<22;n++)
                {
                    float t=(lo+hi)*.5f;var p=f.Point(t);var normal=GroundNormal(p);
                    float clearance=Support(i,Rotation(f,t,i),normal)/normal.y;
                    if(p.y>height(p.x,p.z)+clearance+.003f)lo=t;else hi=t;
                }
                f.Landing=(lo+hi)*.5f;f.GroundNormal=GroundNormal(f.Point(f.Landing));bombs[i]=f;
            }
            rockMesh=RuntimeResources.Own(parent,new Mesh{name="Cooling tumbling lava fragments"});rockMesh.MarkDynamic();
            rockMesh.vertices=rockVertices;rockMesh.normals=rockNormals;rockMesh.uv=uv;rockMesh.colors=rockColors;rockMesh.triangles=triangles;
            RendererFor(parent,rockMesh,"VolcanicBomb",true);
            for(int i=0;i<SparkCount;i++)sparks[i]=Create(i,true);
            var bounds=new Bounds(bombs[0].Origin,Vector3.one);
            void Include(Flight f)
            {bounds.Encapsulate(f.Origin);bounds.Encapsulate(f.Point(f.Landing));bounds.Encapsulate(f.Point(f.Landing)+Vector3.ProjectOnPlane(f.Velocity,Vector3.up)*.16f);bounds.Encapsulate(f.Point(f.Velocity.y/Gravity));}
            foreach(var f in bombs)Include(f);foreach(var f in sparks)Include(f);
            bounds.Expand(1);rockMesh.bounds=bounds;
            uv=new Vector2[sparkVertices.Length];triangles=new int[(BombCount+SparkCount)*6];
            for(int i=0;i<BombCount+SparkCount;i++)
            {
                int v=i*4,t=i*6;uv[v]=Vector2.zero;uv[v+1]=Vector2.right;uv[v+2]=Vector2.one;uv[v+3]=Vector2.up;
                triangles[t]=v;triangles[t+1]=v+1;triangles[t+2]=v+2;triangles[t+3]=v;triangles[t+4]=v+2;triangles[t+5]=v+3;
            }
            sparkMesh=RuntimeResources.Own(parent,new Mesh{name="Soft ballistic sparks and molten halos"});sparkMesh.MarkDynamic();
            sparkMesh.vertices=sparkVertices;sparkMesh.uv=uv;sparkMesh.colors=sparkColors;sparkMesh.triangles=triangles;sparkMesh.bounds=rockMesh.bounds;
            RendererFor(parent,sparkMesh,"VolcanicSpark",false);
        }
        static void RendererFor(Transform parent,Mesh mesh,string shader,bool solid)
        {
            var obj=new GameObject(mesh.name,typeof(MeshFilter),typeof(MeshRenderer));obj.transform.SetParent(parent,false);
            obj.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=obj.GetComponent<MeshRenderer>();renderer.sharedMaterial=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>(shader)));
            renderer.shadowCastingMode=solid?ShadowCastingMode.On:ShadowCastingMode.Off;renderer.receiveShadows=solid;
        }
        Vector3 GroundNormal(Vector3 point)
        {
            const float step=.035f;
            float dx=(groundHeight(point.x+step,point.z)-groundHeight(point.x-step,point.z))/(2*step);
            float dz=(groundHeight(point.x,point.z+step)-groundHeight(point.x,point.z-step))/(2*step);
            return new Vector3(-dx,1,-dz).normalized;
        }
        static Quaternion Rotation(Flight flight,float age,int index)=>Quaternion.AngleAxis(age*flight.Spin+index*67,flight.Axis);
        float Support(int index,Quaternion rotation,Vector3 normal)
        {
            float minimum=0;var localNormal=Quaternion.Inverse(rotation)*normal;
            for(int i=0;i<FaceVertices;i++)minimum=Mathf.Min(minimum,Vector3.Dot(rockShape[index*FaceVertices+i],localNormal));
            return -minimum*bombs[index].Size;
        }
        void Streak(int index,Vector3 head,Vector3 tail,Vector3 view,float radius,float heat,float opacity)
        {
            Vector3 along=head-tail;
            if(along.sqrMagnitude<.000001f)along=Vector3.up*.002f;
            // Never stretch the billboard by the world-space flight distance:
            // that turns a small ember into a thin red debug ray at 1080p.
            // Keep a fixed, short direction so the shader still has a tapered
            // UV axis while the visible shape stays a rounded spark.
            Vector3 direction=along.normalized;
            Vector3 side=Vector3.Cross(direction,view).normalized*radius*1.65f;
            if(side.sqrMagnitude<.000001f)side=Vector3.right*radius*1.65f;
            int v=index*4;var halfLength=radius*.72f;var center=head;
            var tip=direction*halfLength;
            sparkVertices[v]=center-tip-side;sparkVertices[v+1]=center-tip+side;
            sparkVertices[v+2]=center+tip+side;sparkVertices[v+3]=center+tip-side;
            Color color=Color.Lerp(new Color(.75f,.035f,.002f),new Color(2.4f,.64f,.075f),heat);
            color.a=opacity;for(int j=0;j<4;j++)sparkColors[v+j]=color;
        }
        public void Tick(float time,Camera camera)
        {
            Vector3 view=camera?camera.transform.forward:Vector3.forward;
            ActiveBombs=ActiveSparks=LandedBombs=0;PeakHeat=0;
            for(int i=0;i<BombCount;i++)
            {
                var f=bombs[i];float age=Mathf.Repeat(time+f.Offset,Cycle),life=age/f.Landing;
                float groundAge=Mathf.Max(0,age-f.Landing);
                bool landed=life>=1,alive=age<f.Landing+SettleDuration;
                float size=alive?f.Size*Mathf.SmoothStep(0,1,age/.07f):0;
                float heat=alive?(landed?.34f*Mathf.Exp(-groundAge*3):Mathf.Lerp(1,.34f,Mathf.SmoothStep(0,1,(life-.10f)/.90f))):0;
                var point=f.Point(Mathf.Min(age,f.Landing));var rotation=Rotation(f,age,i);
                if(landed)
                {
                    // A short, damped hop carries the incoming translation and
                    // spin into a stable resting rock. It cannot keep falling
                    // through the terrain while a separate glow remains above it.
                    float settle=.16f*(1-Mathf.Exp(-groundAge/.16f));
                    rotation=Rotation(f,f.Landing+settle,i);
                    point+=Vector3.ProjectOnPlane(f.Velocity,Vector3.up)*settle;
                    float hop=Mathf.Clamp01(groundAge/.28f);
                    point.y=groundHeight(point.x,point.z)+Support(i,rotation,f.GroundNormal)/f.GroundNormal.y+.003f+4*hop*(1-hop)*.065f;
                    if(alive)LandedBombs++;
                }
                float fade=landed?Mathf.SmoothStep(0,1,(groundAge-.66f)/(SettleDuration-.66f)):0;
                Color state=new Color(heat,(i*.618033f)%1,fade,1);
                for(int v=0;v<FaceVertices;v++)
                {int at=i*FaceVertices+v;rockVertices[at]=point+rotation*rockShape[at]*size;rockNormals[at]=rotation*rockShapeNormals[at];rockColors[at]=state;}
                float opacity=alive?Mathf.SmoothStep(0,1,age/.08f)*heat*.6f:0;
                var tail=landed?point-Vector3.up*.002f:f.Point(Mathf.Max(0,age-.032f));
                Streak(i,point,tail,view,f.Size*(landed?1.5f:2.3f),heat,opacity*(1-fade)*(landed?.3f:1));
                if(alive){ActiveBombs++;PeakHeat=Mathf.Max(PeakHeat,heat);}
            }
            for(int i=0;i<SparkCount;i++)
            {
                var f=sparks[i];float age=Mathf.Repeat(time+f.Offset,Cycle),life=age/f.Landing;
                float heat=1-Mathf.Clamp01(life),opacity=life<1?Mathf.SmoothStep(0,1,age/.09f)*(1-Mathf.SmoothStep(0,1,(life-.55f)/.45f))*.75f:0;
                var point=f.Point(Mathf.Min(age,f.Landing));
                Streak(BombCount+i,point,f.Point(Mathf.Max(0,Mathf.Min(age,f.Landing)-.026f)),view,f.Size*1.8f,heat,opacity);
                if(opacity>0)ActiveSparks++;
            }
            rockMesh.vertices=rockVertices;rockMesh.normals=rockNormals;rockMesh.colors=rockColors;
            sparkMesh.vertices=sparkVertices;sparkMesh.colors=sparkColors;
        }
    }
}
