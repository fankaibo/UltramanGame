using System.Collections.Generic;
using UnityEngine;

namespace UltramanGame.Runtime
{
    // A deterministic volcanic ruin stage: all motion is generated locally so it stays
    // responsive while the camera service is running and is safe to replay in tests.
    public sealed class VolcanoStage : MonoBehaviour
    {
        readonly List<Material> materials=new List<Material>();
        readonly List<Transform> embers=new List<Transform>();
        readonly List<Vector3> emberVelocity=new List<Vector3>();
        readonly List<float> emberAge=new List<float>();
        readonly LineRenderer[] meteors=new LineRenderer[5];
        readonly LineRenderer[] lavaStreams=new LineRenderer[3];
        Material lava,glow;
        Vector3[] vents={new Vector3(5.3f,2.55f,13.5f),new Vector3(-5.7f,2.0f,16.5f)};
        readonly System.Random random=new System.Random(903);
        public static VolcanoStage Create(Transform parent)
        {
            var stage=new GameObject("Volcanic ruin stage").AddComponent<VolcanoStage>();
            stage.transform.SetParent(parent,false);stage.Build();return stage;
        }
        Material Surface(string name,Color color,float metallic=.1f,float smooth=.3f)
        {
            var m=new Material(Resources.Load<Material>("PrototypeSurface")){name=name,color=color};
            m.SetFloat("_Metallic",metallic);m.SetFloat("_Glossiness",smooth);materials.Add(m);return m;
        }
        void Build()
        {
            var ground=new Material(Resources.Load<Shader>("VolcanoGround"));ground.SetColor("_Color",new Color(.055f,.035f,.045f));materials.Add(ground);
            var basalt=Surface("Scorched basalt",new Color(.075f,.052f,.065f),.08f,.24f);
            var rock=Surface("Broken volcanic rock",new Color(.11f,.065f,.065f),.06f,.2f);
            lava=Surface("Lava fissure",new Color(.75f,.08f,.015f),.05f,.3f);lava.EnableKeyword("_EMISSION");
            lava.SetColor("_EmissionColor",new Color(1,.09f,.012f)*2.4f);
            glow=RuntimeResources.Own(transform,new Material(Resources.Load<Shader>("SoftGlow")){color=Color.white});
            GameWorld.Primitive("Ash plain",PrimitiveType.Cube,transform,new Vector3(0,-.07f,7),new Vector3(70,.14f,60),ground);
            for(int i=0;i<22;i++)
            {
                float x=(float)(random.NextDouble()*23-11.5),z=(float)(random.NextDouble()*18-3),s=.12f+(float)random.NextDouble()*.42f;
                GameWorld.Primitive("Ruin rubble",PrimitiveType.Cube,transform,new Vector3(x,s*.45f,z),new Vector3(s*(.65f+(float)random.NextDouble()),s,s*(.7f+(float)random.NextDouble())),rock).rotation=Quaternion.Euler(random.Next(0,30),random.Next(0,180),random.Next(0,30));
            }
            RuinPillar(new Vector3(-6.5f,.9f,5.7f),basalt,.55f,2.0f);
            RuinPillar(new Vector3(6.8f,.72f,7.4f),rock,.48f,1.65f);
            RuinPillar(new Vector3(-3.9f,.52f,10.1f),rock,.36f,1.25f);
            Volcano(new Vector3(5.3f,0,13.5f),4.1f,2.55f,basalt);
            Volcano(new Vector3(-5.7f,0,16.5f),3.2f,2.0f,rock);
            Volcano(new Vector3(0,0,22f),6.8f,3.5f,basalt);
            for(int i=0;i<lavaStreams.Length;i++)
            {
                lavaStreams[i]=Line("Lava river",18,.055f);lavaStreams[i].enabled=true;
                float sx=i==0?-5.3f:i==1?5.3f:0,sz=i==0?13.5f:i==1?13.5f:21.8f;
                for(int j=0;j<18;j++)lavaStreams[i].SetPosition(j,new Vector3(sx+(i-1)*.12f+Mathf.Sin(j*1.7f+i)*.16f,.025f,sz-j*.48f));
            }
            for(int i=0;i<meteors.Length;i++)meteors[i]=Line("Falling star",2,.022f);
            for(int i=0;i<30;i++)
            {
                var t=GameWorld.Primitive("Eruption ember",PrimitiveType.Sphere,transform,Vector3.zero,Vector3.one*.045f,glow);embers.Add(t);
                emberVelocity.Add(Vector3.up*(2.1f+(float)random.NextDouble()*2.8f)+new Vector3(((float)random.NextDouble()-.5f)*1.3f,0,((float)random.NextDouble()-.5f)*.9f));
                emberAge.Add((float)random.NextDouble()*2.8f);t.gameObject.SetActive(false);
            }
        }
        void RuinPillar(Vector3 p,Material m,float width,float height)
        {var t=GameWorld.Primitive("Collapsed ruin",PrimitiveType.Cube,transform,p+Vector3.up*height*.5f,new Vector3(width,height,width*.75f),m);t.rotation=Quaternion.Euler(0,random.Next(0,180),random.Next(-8,8));}
        void Volcano(Vector3 p,float radius,float height,Material m)
        {
            // A tapered mesh reads as a volcano in the fixed 45-degree lens;
            // cylinders looked like featureless black towers from the arena.
            const int segments=24;
            var mesh=new Mesh{name="Volcano cone mesh"};
            var vertices=new Vector3[segments*2];var triangles=new int[segments*6];
            for(int i=0;i<segments;i++)
            {
                float a=i*Mathf.PI*2/segments,cs=Mathf.Cos(a),sn=Mathf.Sin(a);
                vertices[i]=new Vector3(cs*radius,0,sn*radius);
                vertices[segments+i]=new Vector3(cs*radius*.16f,height,sn*radius*.16f);
                int n=(i+1)%segments;int at=i*6;
                triangles[at]=i;triangles[at+1]=n;triangles[at+2]=segments+i;
                triangles[at+3]=n;triangles[at+4]=segments+n;triangles[at+5]=segments+i;
            }
            mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();RuntimeResources.Own(transform,mesh);
            var obj=new GameObject("Volcano cone");obj.transform.SetParent(transform,false);obj.transform.localPosition=p;obj.transform.localRotation=Quaternion.Euler(0,random.Next(0,360),0);
            obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterial=m;
            GameWorld.Primitive("Crater glow",PrimitiveType.Cylinder,transform,p+Vector3.up*(height+.03f),new Vector3(radius*.34f,.025f,radius*.34f),lava);
        }
        LineRenderer Line(string name,int points,float width)
        {var r=new GameObject(name).AddComponent<LineRenderer>();r.transform.SetParent(transform,false);r.sharedMaterial=glow;r.positionCount=points;r.widthMultiplier=width;r.numCapVertices=3;r.enabled=false;return r;}
        void Update()
        {
            float time=Time.unscaledTime;
            for(int i=0;i<meteors.Length;i++)
            {
                var r=meteors[i];r.enabled=true;float cycle=Mathf.Repeat(time*(.11f+i*.017f)+i*.23f,1);float x=Mathf.Lerp(-10f+i*4.2f,7f-i*1.2f,cycle);float y=Mathf.Lerp(8.4f+i*.38f,3.8f,cycle);float z=11.5f+i*1.6f;
                r.SetPosition(0,new Vector3(x,y,z));r.SetPosition(1,new Vector3(x-.42f,y+.9f,z));ColorLine(r,new Color(.65f,.82f,1),Mathf.Clamp01((cycle<.08f?cycle/.08f:cycle>.9f?(1-cycle)/.1f:1))*.72f);
            }
            for(int i=0;i<lavaStreams.Length;i++)
            {var r=lavaStreams[i];ColorLine(r,new Color(1,.10f,.015f),.48f+.20f*Mathf.Sin(time*3.2f+i));}
            for(int i=0;i<embers.Count;i++)
            {
                float age=Mathf.Repeat(time+emberAge[i],2.9f);int vent=i%vents.Length;Vector3 p=vents[vent]+emberVelocity[i]*age+Vector3.down*1.15f*age*age;
                bool active=age<2.45f;embers[i].gameObject.SetActive(active);if(active)embers[i].position=p;
            }
        }
        static void ColorLine(LineRenderer line,Color c,float a){c.a=a;line.startColor=c;line.endColor=c;}
        void OnDestroy(){foreach(var m in materials)if(m)Destroy(m);}
    }
}
