using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UltramanGame.Runtime
{
    // Loose lintels belong to the real building silhouette. Each can fall only
    // once per round, onto the terrain, using the presentation clock rather
    // than physics. One mesh and a fixed dust pool serve the entire outpost.
    public sealed class OutpostDamage
    {
        public struct Panel { public Matrix4x4 Frame; public int Building; }
        sealed class Piece
        {
            public Vector3 Rest,Origin,Velocity,Axis,ShakeDirection,Landing;
            public Quaternion Initial=Quaternion.identity,Contact=Quaternion.identity,Final=Quaternion.identity;
            public int Building,First;
            public float QuakeAt=float.PositiveInfinity,Strength,BreakAt=float.PositiveInfinity,Flight,Spin;
            public bool Started,Landed;
        }
        sealed class Cloud
        {
            public Transform Root;public Material Material;public Vector3 Origin;
            public float Start=float.PositiveInfinity,Size;
        }
        readonly Piece[] pieces;
        readonly Cloud[] clouds=new Cloud[16];
        readonly Mesh mesh;
        readonly Vector3[] vertices,normals,rest,restNormals;
        readonly Vector4[] tangents,restTangents;
        readonly Func<float,float,float> height;
        float clock;int cloudIndex;
        public int Shocks {get;private set;}
        public int Detached {get;private set;}
        public int ActiveClouds {get;private set;}
        public int Count=>pieces.Length;
        public Mesh Geometry=>mesh;
        public float Clock=>clock;
        public OutpostDamage(Transform parent,IReadOnlyList<Panel> panels,Material concrete,Func<float,float,float> ground)
        {
            height=ground;pieces=new Piece[panels.Count];vertices=new Vector3[panels.Count*24];normals=new Vector3[vertices.Length];
            rest=new Vector3[vertices.Length];restNormals=new Vector3[vertices.Length];var uv=new Vector2[vertices.Length];var indices=new int[panels.Count*36];
            var corners=new[]{new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,.5f,-.5f),new Vector3(-.5f,.5f,-.5f),
                new Vector3(-.5f,-.5f,.5f),new Vector3(.5f,-.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(-.5f,.5f,.5f)};
            int[] faces={0,3,2,1,5,6,7,4,4,7,3,0,1,2,6,5,3,7,6,2,4,0,1,5};
            for(int i=0;i<panels.Count;i++)
            {
                var p=panels[i];var piece=new Piece{Rest=p.Frame.MultiplyPoint3x4(Vector3.zero),Building=p.Building,First=i*24};pieces[i]=piece;
                piece.Axis=new Vector3(.36f,0,(i%2==0?1:-1)*.7f).normalized;piece.Spin=(i%2==0?1:-1)*(135+i%3*25);
                for(int face=0;face<6;face++)
                {
                    int v=i*24+face*4,t=i*36+face*6;
                    for(int n=0;n<4;n++)vertices[v+n]=p.Frame.MultiplyPoint3x4(corners[faces[face*4+n]]);
                    Vector3 normal=Vector3.Cross(vertices[v+1]-vertices[v],vertices[v+2]-vertices[v]).normalized;
                    for(int n=0;n<4;n++){rest[v+n]=vertices[v+n]-piece.Rest;normals[v+n]=restNormals[v+n]=normal;}
                    uv[v]=Vector2.zero;uv[v+1]=Vector2.up;uv[v+2]=Vector2.one;uv[v+3]=Vector2.right;
                    indices[t]=v;indices[t+1]=v+1;indices[t+2]=v+2;indices[t+3]=v;indices[t+4]=v+2;indices[t+5]=v+3;
                }
            }
            mesh=RuntimeResources.Own(parent,new Mesh{name="Loose concrete lintels",vertices=vertices,normals=normals,uv=uv,triangles=indices});mesh.MarkDynamic();mesh.RecalculateBounds();mesh.RecalculateTangents();
            restTangents=mesh.tangents;tangents=new Vector4[restTangents.Length];
            var obj=new GameObject(mesh.name,typeof(MeshFilter),typeof(MeshRenderer));obj.transform.SetParent(parent,false);
            obj.GetComponent<MeshFilter>().sharedMesh=mesh;obj.GetComponent<MeshRenderer>().sharedMaterial=concrete;
            for(int i=0;i<clouds.Length;i++)
            {
                var material=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("GroundDust")));
                material.SetFloat("_Seed",i*3.71f+25);material.SetColor("_Color",new Color(.50f,.48f,.44f,.53f));
                var quad=GameWorld.Primitive("Falling concrete dust",PrimitiveType.Quad,parent,Vector3.zero,Vector3.one,material);
                quad.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;quad.gameObject.SetActive(false);
                clouds[i]=new Cloud{Root=quad,Material=material};
            }
        }
        static float Strength(string cause)
        {
            switch(cause){case "slam":return 1;case "defeat":return 1.15f;case "uppercut-land":return .9f;
                case "hero-land":return .72f;case "beam-brace":return .45f;case "rush":case "stagger":return .35f;default:return 0;}
        }
        float Shake(Piece p,float time)
        {float age=time-p.QuakeAt;return age>=0&&age<.65f?Mathf.Sin(age*40)*Mathf.Exp(-age*8)*p.Strength:0;}
        public void Shock(Vector3 origin,string cause)
        {
            float strength=Strength(cause);if(strength<=0)return;Shocks++;
            foreach(var p in pieces)if(float.IsPositiveInfinity(p.BreakAt))
            {
                var direction=Vector3.ProjectOnPlane(p.Rest-origin,Vector3.up);float distance=direction.magnitude;
                p.ShakeDirection=direction.normalized;p.Strength=strength*Mathf.Clamp01(1-distance/18);
                p.QuakeAt=clock+distance/14;
            }
            int scheduled=0;
            if(strength>=.7f)for(int group=0;group<2;group++)
            {
                Piece selected=null;
                foreach(var p in pieces)if(p.Building==group&&float.IsPositiveInfinity(p.BreakAt)&&(selected==null||p.Rest.y>selected.Rest.y))selected=p;
                if(selected==null)continue;var piece=selected;piece.BreakAt=piece.QuakeAt+.14f;
                float shake=Shake(piece,piece.BreakAt);piece.Origin=piece.Rest+piece.ShakeDirection*(shake*.022f);
                piece.Initial=Quaternion.AngleAxis(shake*2.5f,Vector3.up);
                piece.Velocity=piece.ShakeDirection*(.48f+strength*.35f)+Vector3.up*.24f;
                // Find the first terrain contact of the rotated shape, not its
                // pivot; cache that settled pose so it cannot sink or keep spinning.
                float lo=0,hi=2;
                for(int n=0;n<22;n++)
                {float t=(lo+hi)*.5f;Vector3 center=Fall(piece,t);Quaternion rotation=Rotation(piece,t);if(Clearance(piece,center,rotation)>0)lo=t;else hi=t;}
                piece.Flight=(lo+hi)*.5f;piece.Contact=Rotation(piece,piece.Flight);piece.Landing=Fall(piece,piece.Flight);
                piece.Landing.y-=Clearance(piece,piece.Landing,piece.Contact);
                float x=piece.Landing.x,z=piece.Landing.z;
                Vector3 groundNormal=new Vector3(-(height(x+.025f,z)-height(x-.025f,z))/.05f,1,-(height(x,z+.025f)-height(x,z-.025f))/.05f).normalized;
                piece.Final=Quaternion.FromToRotation(Vector3.up,groundNormal)*Quaternion.AngleAxis(piece.First*1.7f,Vector3.up);scheduled++;
            }
            if(Debug.isDebugBuild)Debug.Log($"[OutpostShock] cause={cause} scheduled={scheduled} total={Shocks}");
        }
        static Vector3 Fall(Piece p,float time)=>p.Origin+p.Velocity*time+Vector3.down*(4.5f*time*time);
        static Quaternion Rotation(Piece p,float time)=>Quaternion.AngleAxis(p.Spin*time,p.Axis)*p.Initial;
        float Clearance(Piece p,Vector3 center,Quaternion rotation)
        {
            float minimum=float.PositiveInfinity;
            for(int v=p.First;v<p.First+24;v++){var point=center+rotation*rest[v];minimum=Mathf.Min(minimum,point.y-height(point.x,point.z)-.009f);}
            return minimum;
        }
        void Dust(Vector3 origin,float at,float size)
        {var c=clouds[cloudIndex++%clouds.Length];c.Origin=origin;c.Start=at;c.Size=size;}
        public void Tick(Camera camera,float dt)
        {
            clock+=Mathf.Max(0,dt);Detached=ActiveClouds=0;
            foreach(var p in pieces)
            {
                float age=clock-p.BreakAt;Vector3 center=p.Rest;Quaternion rotation=Quaternion.identity;
                if(age>=0)
                {
                    Detached++;
                    if(!p.Started){p.Started=true;Dust(p.Origin,p.BreakAt,.84f);}
                    if(age>=p.Flight)
                    {
                        // After first corner contact, roll onto the broad face
                        // instead of leaving a slab balanced upright on one edge.
                        rotation=Quaternion.Slerp(p.Contact,p.Final,Mathf.SmoothStep(0,1,(age-p.Flight)/.24f));
                        center=p.Landing;center.y-=Clearance(p,center,rotation);
                        if(!p.Landed){p.Landed=true;Dust(center+Vector3.up*.07f,p.BreakAt+p.Flight,1.04f);}
                    }
                    else{center=Fall(p,age);rotation=Rotation(p,age);}
                }
                else
                {float shake=Shake(p,clock);center+=p.ShakeDirection*(shake*.022f);rotation=Quaternion.AngleAxis(shake*2.5f,Vector3.up);}
                for(int v=p.First;v<p.First+24;v++)
                {
                    vertices[v]=center+rotation*rest[v];normals[v]=rotation*restNormals[v];
                    var tangent=rotation*(Vector3)restTangents[v];tangents[v]=new Vector4(tangent.x,tangent.y,tangent.z,restTangents[v].w);
                }
            }
            mesh.vertices=vertices;mesh.normals=normals;mesh.tangents=tangents;mesh.RecalculateBounds();
            // Rotating corners can land on a float-rounded min/max boundary.
            // Keep a small culling margin around the moving geometry.
            var bounds=mesh.bounds;bounds.Expand(.02f);mesh.bounds=bounds;
            foreach(var c in clouds)
            {
                float age=clock-c.Start,t=age/1.45f;bool visible=t>=0&&t<1;c.Root.gameObject.SetActive(visible);if(!visible)continue;
                c.Root.position=c.Origin+Vector3.up*(.13f*age);c.Root.rotation=camera?camera.transform.rotation:Quaternion.identity;
                float size=c.Size*Mathf.Lerp(.7f,2.0f,t);c.Root.localScale=new Vector3(size,size*.72f,1);c.Material.SetFloat("_Age",t);ActiveClouds++;
            }
        }
        public void Reset()
        {
            clock=0;cloudIndex=Shocks=Detached=ActiveClouds=0;
            foreach(var p in pieces){p.QuakeAt=p.BreakAt=float.PositiveInfinity;p.Started=p.Landed=false;}
            foreach(var c in clouds){c.Start=float.PositiveInfinity;c.Root.gameObject.SetActive(false);}
            Tick(null,0);
        }
    }
}
