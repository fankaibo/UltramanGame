using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UltramanGame.Runtime
{
    // Human-scale roads, vehicles and open ruined shells make the fighters
    // read as giants. All geometry is outside their footwork and fall area.
    public sealed class VolcanicOutpost
    {
        sealed class Batch
        {
            public readonly List<Vector3> Vertices=new List<Vector3>();
            public readonly List<Vector2> UV=new List<Vector2>();
            public readonly List<int> Triangles=new List<int>();
            public void Quad(Matrix4x4 frame,Vector3 a,Vector3 b,Vector3 c,Vector3 d)
            {
                int at=Vertices.Count;
                Vertices.Add(frame.MultiplyPoint3x4(a));Vertices.Add(frame.MultiplyPoint3x4(b));
                Vertices.Add(frame.MultiplyPoint3x4(c));Vertices.Add(frame.MultiplyPoint3x4(d));
                UV.AddRange(new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right});
                Triangles.AddRange(new[]{at,at+1,at+2,at,at+2,at+3});
            }
        }
        readonly Batch[] batches={new Batch(),new Batch(),new Batch(),new Batch(),new Batch()};
        readonly Func<float,float,float> height;
        readonly System.Random random=new System.Random(930);
        readonly List<OutpostDamage.Panel> loose=new List<OutpostDamage.Panel>();
        int buildingIndex=-1;
        public OutpostDamage Damage {get;private set;}
        Matrix4x4 frame=Matrix4x4.identity;
        const int Concrete=0,Steel=1,Road=2,Paint=3,Glass=4;
        float Range(float a,float b)=>Mathf.Lerp(a,b,(float)random.NextDouble());
        VolcanicOutpost(Func<float,float,float> ground){height=ground;}
        public static VolcanicOutpost Create(Transform parent,Func<float,float,float> ground)
        {
            var root=new GameObject("Abandoned volcano observation base").transform;root.SetParent(parent,false);
            var site=new VolcanicOutpost(ground);site.Build();site.Upload(root);return site;
        }
        void Site(float x,float z,float rotation=0)
        {frame=Matrix4x4.TRS(new Vector3(x,height(x,z)-.035f,z),Quaternion.Euler(0,rotation,0),Vector3.one);}
        void Quad(int material,Matrix4x4 matrix,Vector3 a,Vector3 b,Vector3 c,Vector3 d)=>batches[material].Quad(matrix,a,b,c,d);
        void Loose(Vector3 center,Vector3 size)
        {loose.Add(new OutpostDamage.Panel{Frame=frame*Matrix4x4.TRS(center,Quaternion.identity,size),Building=buildingIndex});}
        void Box(int material,Vector3 center,Vector3 size,Vector3 angles=default)
        {
            var m=frame*Matrix4x4.TRS(center,Quaternion.Euler(angles),size);
            var p=new[]{new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,.5f,-.5f),new Vector3(-.5f,.5f,-.5f),
                new Vector3(-.5f,-.5f,.5f),new Vector3(.5f,-.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(-.5f,.5f,.5f)};
            Quad(material,m,p[0],p[3],p[2],p[1]);Quad(material,m,p[5],p[6],p[7],p[4]);
            Quad(material,m,p[4],p[7],p[3],p[0]);Quad(material,m,p[1],p[2],p[6],p[5]);
            Quad(material,m,p[3],p[7],p[6],p[2]);Quad(material,m,p[4],p[0],p[1],p[5]);
        }
        void Rod(int material,Vector3 start,Vector3 end,float radius,int sides=6)
        {
            var axis=end-start;if(axis.sqrMagnitude<.000001f)return;
            var m=frame*Matrix4x4.TRS(start,Quaternion.FromToRotation(Vector3.up,axis),Vector3.one);
            for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;
                var p=new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);
                var q=new Vector3(Mathf.Cos(b)*radius,0,Mathf.Sin(b)*radius);
                Quad(material,m,p,p+Vector3.up*axis.magnitude,q+Vector3.up*axis.magnitude,q);
            }
        }
        // A solid jagged cap, not a flat wall texture or a complete cube tower.
        void BrokenPier(float x,float z,float width,float depth,float top)
        {
            float left=top+Range(-.05f,.04f),right=top+Range(-.06f,.05f),back=Range(-.04f,.04f);
            var p=new[]{new Vector3(x-width/2,.06f,z-depth/2),new Vector3(x+width/2,.06f,z-depth/2),
                new Vector3(x+width/2,right,z-depth/2),new Vector3(x-width/2,left,z-depth/2),
                new Vector3(x-width/2,.06f,z+depth/2),new Vector3(x+width/2,.06f,z+depth/2),
                new Vector3(x+width/2,right+back,z+depth/2),new Vector3(x-width/2,left+back,z+depth/2)};
            Quad(Concrete,frame,p[0],p[3],p[2],p[1]);Quad(Concrete,frame,p[5],p[6],p[7],p[4]);
            Quad(Concrete,frame,p[4],p[7],p[3],p[0]);Quad(Concrete,frame,p[1],p[2],p[6],p[5]);
            Quad(Concrete,frame,p[3],p[7],p[6],p[2]);
            if(top>.38f)for(int i=0;i<2;i++)
            {
                var start=new Vector3(x+(i-.5f)*width*.5f,top-.06f,z);
                var bend=start+new Vector3(Range(-.035f,.035f),.15f,Range(-.04f,.04f));
                Rod(Steel,start,bend,.006f);Rod(Steel,bend,bend+new Vector3(.045f,.04f,-.016f),.006f);
            }
        }
        void Building(float x,float z,float yaw,int floors)
        {
            buildingIndex++;
            Site(x,z,yaw);const float width=1.55f,depth=1.06f,storey=.34f;
            Box(Concrete,new Vector3(0,.03f,0),new Vector3(width+.1f,.08f,depth+.1f));
            for(int floor=0;floor<floors;floor++)
            {
                float y=.08f+floor*storey;
                // Slabs stop at the broken rear corner, exposing their thickness.
                float slabWidth=width-.32f-floor*.28f,slabCenter=-.16f-floor*.14f;
                if(floor==0)Box(Concrete,new Vector3(slabCenter,y,0),new Vector3(slabWidth,.042f,depth));
                else
                {
                    // The exposed front half can detach in two sections. The
                    // interior slab stays attached to the surviving rear frame.
                    Box(Concrete,new Vector3(slabCenter,y,depth/4),new Vector3(slabWidth,.042f,depth/2));
                    for(int side=-1;side<=1;side+=2)Loose(new Vector3(slabCenter+side*slabWidth/4,y,-depth/4),new Vector3(slabWidth/2,.042f,depth/2));
                }
                if(floor==0)Box(Concrete,new Vector3(.61f,y,-.28f),new Vector3(.3f,.042f,.5f));
                for(int i=0;i<5;i++)
                {
                    float px=-width/2+i*width/4;
                    float top=.12f+floors*storey-i*.16f;
                    if(floor==0)BrokenPier(px,-depth/2,.065f,.09f,top);
                    if(i<4)
                    {
                        float mid=px+width/8;
                        if(y+.1f<top)Box(Concrete,new Vector3(mid,y+.065f,-depth/2),new Vector3(width/4-.06f,.105f,.074f));
                        if(y+.32f<top-.06f)
                        {
                            Loose(new Vector3(mid,y+.29f,-depth/2),new Vector3(width/4-.05f,.06f,.075f));
                            Rod(Steel,new Vector3(mid-.13f,y+.12f,-depth/2-.009f),new Vector3(mid+.13f,y+.12f,-depth/2-.009f),.009f);
                            // A few surviving panes sit inside real open windows.
                            if((i+floor)%3==0)Box(Glass,new Vector3(mid-.06f,y+.20f,-depth/2+.025f),new Vector3(.09f,.14f,.014f),new Vector3(0,4,3));
                        }
                    }
                }
            }
            for(int side=-1;side<=1;side+=2)
            {
                for(int i=0;i<3;i++)BrokenPier(side*width/2,-.18f+i*.34f,.095f,.11f,Range(.4f,floors*storey+.1f));
                for(int floor=0;floor<(side>0?1:floors);floor++)Box(Concrete,new Vector3(side*width/2,.18f+floor*storey,.08f),new Vector3(.08f,.15f,.80f));
            }
            // Collapsed floor panel and bent reinforcement in the exposed interior.
            Box(Concrete,new Vector3(.07f,.21f,.2f),new Vector3(.85f,.055f,.52f),new Vector3(-24,17,13));
            for(int i=0;i<6;i++)Rod(Steel,new Vector3(-.45f+i*.16f,.11f,.38f),new Vector3(-.32f+i*.16f,.34f,.19f),.008f);
            for(int i=0;i<18;i++)
            {
                float a=Range(-Mathf.PI,Mathf.PI),r=Range(.62f,1.05f);
                Box(Concrete,new Vector3(Mathf.Cos(a)*r,.07f,Mathf.Sin(a)*r*.75f),new Vector3(Range(.07f,.19f),Range(.04f,.09f),Range(.06f,.18f)),new Vector3(Range(-20,20),Range(0,180),Range(-15,15)));
            }
        }
        void Shelter(float x,float z,float yaw)
        {
            Site(x,z,yaw);
            Box(Concrete,new Vector3(0,.055f,0),new Vector3(1.9f,.12f,.9f));
            for(int i=0;i<4;i++)
            {
                float px=-.82f+i*.55f,h=i==3?.37f:.70f;
                Rod(Steel,new Vector3(px,.1f,-.35f),new Vector3(px-.04f,h,-.35f),.026f);
                Rod(Steel,new Vector3(px,.1f,.35f),new Vector3(px,h,.35f),.026f);
                Rod(Steel,new Vector3(px-.04f,h,-.35f),new Vector3(px,h+.10f,0),.022f);
                Rod(Steel,new Vector3(px,h+.10f,0),new Vector3(px,h,.35f),.022f);
                if(i<2)Rod(Steel,new Vector3(px,.15f,.35f),new Vector3(px+.55f,.70f,.35f),.012f);
            }
            for(int i=0;i<14;i++)
                Box(Steel,new Vector3(-.77f+i*.063f,.75f+(i%2)*.016f,.16f),new Vector3(.073f,.024f,.50f),new Vector3(12,0,0));
            Box(Steel,new Vector3(.65f,.26f,0),new Vector3(.58f,.045f,.72f),new Vector3(10,15,-32));
            for(int i=0;i<4;i++)Box(Concrete,new Vector3(-.68f+i*.28f,.22f,.07f),new Vector3(.2f,.27f,.25f));
        }
        void Tank(float x,float z)
        {
            Site(x,z);const int sides=28;const float radius=.32f;
            for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;
                float ha=i>17&&i<24?.37f:.64f+Mathf.Sin(i*7.3f)*.018f;
                float hb=i+1>17&&i+1<24?.37f:.64f+Mathf.Sin((i+1)*7.3f)*.018f;
                Vector3 pa=new Vector3(Mathf.Cos(a)*radius,.09f,Mathf.Sin(a)*radius),pb=new Vector3(Mathf.Cos(b)*radius,.09f,Mathf.Sin(b)*radius);
                Quad(Steel,frame,pa,pa+Vector3.up*ha,pb+Vector3.up*hb,pb);
                Vector3 ia=pa*.94f,ib=pb*.94f;
                Quad(Steel,frame,ib,ib+Vector3.up*hb,ia+Vector3.up*ha,ia);
                Rod(Paint,pa+Vector3.up*.17f,pb+Vector3.up*.17f,.009f);
                if(i<17||i>=24)Rod(Steel,pa+Vector3.up*ha,pb+Vector3.up*hb,.012f);
            }
            for(int i=0;i<3;i++)Rod(Steel,new Vector3(-.30f+i*.035f,.20f,-.12f),new Vector3(-.56f+i*.03f,.09f,-.37f),.018f);
            Box(Concrete,new Vector3(0,.045f,0),new Vector3(.8f,.1f,.8f));
        }
        void Vehicle(float x,float z,float yaw)
        {
            Site(x,z,yaw);
            Box(Steel,new Vector3(0,.055f,0),new Vector3(.115f,.066f,.28f));
            Box(Paint,new Vector3(0,.104f,-.07f),new Vector3(.106f,.077f,.11f),new Vector3(0,0,-5));
            Box(Glass,new Vector3(0,.12f,-.129f),new Vector3(.078f,.041f,.008f));
            Box(Concrete,new Vector3(0,.084f,.071f),new Vector3(.096f,.022f,.135f));
            foreach(int side in new[]{-1,1})foreach(float dz in new[]{-.079f,.086f})
                Rod(Road,new Vector3(side*.049f,.042f,dz),new Vector3(side*.065f,.042f,dz),.03f,10);
        }
        void Avenue()
        {
            frame=Matrix4x4.identity;
            for(int i=0;i<26;i++)
            {
                if(i==8||i==17)continue;
                float z=-4+i*.50f,x=-3.35f+z*.035f;
                // Terrain-conforming strips retain broad fractures instead of
                // hovering as one perfectly straight black rectangle.
                float w=.38f,len=(i==7||i==16)?.42f:.501f,shift=(i==9||i==18)?Range(-.04f,.04f):0;
                var a=new Vector3(x-w+shift,height(x-w,z)+.013f,z);
                var b=new Vector3(x-w,height(x-w,z+len)+.013f,z+len);
                var c=new Vector3(x+w,height(x+w,z+len)+.013f,z+len+(i==7||i==16?-.13f:0));
                var d=new Vector3(x+w+shift,height(x+w,z)+.013f,z);
                Quad(Road,frame,a,b,c,d);
                if(i%2==0)Box(Paint,new Vector3(x,height(x,z+.2f)+.017f,z+.2f),new Vector3(.015f,.008f,.22f));
                if(i<19&&i%3!=1)
                {
                    Box(Concrete,new Vector3(x-.43f,height(x-.43f,z)+.04f,z),new Vector3(.08f,.09f,.38f),new Vector3(0,0,Range(-4,4)));
                    Rod(Steel,new Vector3(x-.46f,height(x-.46f,z),z),new Vector3(x-.46f,height(x-.46f,z)+.17f,z),.01f);
                    Rod(Steel,new Vector3(x-.46f,height(x-.46f,z)+.14f,z-.2f),new Vector3(x-.44f,height(x-.44f,z)+.12f,z+.2f),.018f);
                }
            }
        }
        void Build()
        {
            Avenue();Building(-3.9f,4.5f,12,3);Building(3.8f,6.1f,-22,2);
            Shelter(-.55f,8.1f,-5);Tank(4.8f,4.1f);Tank(5.55f,4.7f);
            Vehicle(-3.22f,-1.55f,14);Vehicle(-3.46f,1.15f,193);
        }
        void Upload(Transform root)
        {
            var colors=new[]{new Color(.44f,.44f,.42f),new Color(.20f,.25f,.28f),new Color(.105f,.115f,.125f),new Color(.70f,.59f,.33f),new Color(.055f,.095f,.13f)};
            var names=new[]{"Fractured concrete shells","Buckled steel and reinforcement","Broken access road","Weathered markings and truck cab","Surviving recessed glass"};
            for(int i=0;i<batches.Length;i++)
            {
                var batch=batches[i];var mesh=RuntimeResources.Own(root,new Mesh{name=names[i],indexFormat=IndexFormat.UInt32});
                mesh.SetVertices(batch.Vertices);mesh.SetUVs(0,batch.UV);mesh.SetTriangles(batch.Triangles,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
                var mat=RuntimeResources.Own(root,new Material(Resources.Load<Shader>("OutpostSurface")));
                mat.name=names[i];mat.SetColor("_Color",colors[i]);mat.SetFloat("_Metal",i==Steel?.6f:i==Glass?.35f:0);mat.SetFloat("_Rough",i==Glass?.28f:.86f);
                mat.SetFloat("_Rust",i==Steel?1:0);
                string prefix=i==Concrete?"Art/Outpost/rebar_reinforced_concrete":"Art/Basalt/rocks_ground_02";
                mat.SetTexture("_Grain",Resources.Load<Texture2D>(prefix+(i==Concrete?"_diff_2k":"_col_2k")));
                mat.SetTexture("_Normal",Resources.Load<Texture2D>(prefix+"_nor_gl_2k"));
                mat.SetTexture("_ARM",Resources.Load<Texture2D>(prefix+"_arm_2k"));
                mat.SetFloat("_Scale",i==Concrete?1.65f:4.0f);
                var obj=new GameObject(names[i],typeof(MeshFilter),typeof(MeshRenderer));obj.transform.SetParent(root,false);
                obj.GetComponent<MeshFilter>().sharedMesh=mesh;obj.GetComponent<MeshRenderer>().sharedMaterial=mat;
                if(i==Concrete)Damage=new OutpostDamage(root,loose,mat,height);
            }
        }
    }
}
