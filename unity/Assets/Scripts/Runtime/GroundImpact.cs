using UnityEngine;

namespace UltramanGame.Runtime
{
    // Ground contact is a separate event from a hit at chest height. Fragments
    // use a bounded ballistic path and two diminishing bounces; dust stays low
    // so the child can still read the hands and the next guard instruction.
    public sealed class GroundImpact
    {
        sealed class Stone
        {
            public Transform Root;public Vector3[] Vertices;public Vector3 Origin,Velocity,Spin,Scale;
            public Quaternion Rotation;public float Age=10,Life;
        }
        sealed class Cloud
        {
            public Transform Root;public Material Material;public Vector3 Origin,Velocity;
            public float Age=10,Life,Width,Height,Spin;
        }
        const float Gravity=9f,Floor=.008f;
        readonly Stone[] stones=new Stone[72];
        readonly Cloud[] clouds=new Cloud[48];
        readonly System.Random random=new System.Random(934);
        int stoneIndex,cloudIndex;
        public int ActiveStones {get;private set;}
        public int ActiveClouds {get;private set;}
        public int Bursts {get;private set;}
        public string LastCause {get;private set;}
        public float LastAge {get;private set;}=10;
        public Vector3 LastOrigin {get;private set;}
        float Range(float a,float b)=>Mathf.Lerp(a,b,(float)random.NextDouble());
        public GroundImpact(Transform parent)
        {
            var material=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("GroundStone")));
            var meshes=new Mesh[4];var vertices=new Vector3[4][];
            for(int i=0;i<meshes.Length;i++){meshes[i]=RuntimeResources.Own(parent,Shard(i));vertices[i]=meshes[i].vertices;}
            for(int i=0;i<stones.Length;i++)
            {
                var obj=new GameObject("Ground basalt fragment",typeof(MeshFilter),typeof(MeshRenderer));obj.transform.SetParent(parent,false);
                obj.GetComponent<MeshFilter>().sharedMesh=meshes[i%4];obj.GetComponent<MeshRenderer>().sharedMaterial=material;
                stones[i]=new Stone{Root=obj.transform,Vertices=vertices[i%4]};obj.SetActive(false);
            }
            for(int i=0;i<clouds.Length;i++)
            {
                var dust=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("GroundDust")));
                dust.SetFloat("_Seed",i*3.71f);dust.SetColor("_Color",new Color(.46f,.43f,.39f,.66f));
                var quad=GameWorld.Primitive("Ground contact dust",PrimitiveType.Quad,parent,Vector3.zero,Vector3.one,dust);
                quad.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                clouds[i]=new Cloud{Root=quad,Material=dust};quad.gameObject.SetActive(false);
            }
        }
        static Mesh Shard(int seed)
        {
            float t=(1+Mathf.Sqrt(5))/2;
            var p=new[]{new Vector3(-1,t,0),new Vector3(1,t,0),new Vector3(-1,-t,0),new Vector3(1,-t,0),
                new Vector3(0,-1,t),new Vector3(0,1,t),new Vector3(0,-1,-t),new Vector3(0,1,-t),
                new Vector3(t,0,-1),new Vector3(t,0,1),new Vector3(-t,0,-1),new Vector3(-t,0,1)};
            int[] faces={0,11,5,0,5,1,0,1,7,0,7,10,0,10,11,1,5,9,5,11,4,11,10,2,10,7,6,7,1,8,
                3,9,4,3,4,2,3,2,6,3,6,8,3,8,9,4,9,5,2,4,11,6,2,10,8,6,7,9,8,1};
            var rng=new System.Random(1201+seed);
            for(int i=0;i<p.Length;i++)p[i]=p[i].normalized*(.7f+(float)rng.NextDouble()*.3f);
            var v=new Vector3[faces.Length];var indices=new int[v.Length];
            for(int i=0;i<v.Length;i++){v[i]=p[faces[i]];indices[i]=i;}
            var mesh=new Mesh{name="Fractured basalt "+seed,vertices=v,triangles=indices};mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        public void Burst(Vector3 origin,Vector3 direction,string cause)
        {
            origin.y=Floor;LastOrigin=origin;LastAge=0;LastCause=cause;Bursts++;
            Vector3 axis=Vector3.ProjectOnPlane(direction,Vector3.up).normalized;if(axis==Vector3.zero)axis=Vector3.forward;
            Vector3 side=Vector3.Cross(Vector3.up,axis);
            for(int i=0;i<18;i++)
            {
                var s=stones[stoneIndex++%stones.Length];float a=Range(-Mathf.PI,Mathf.PI);
                Vector3 spread=axis*Mathf.Cos(a)+side*Mathf.Sin(a);
                s.Age=0;s.Life=Range(1.25f,1.70f);s.Origin=origin+spread*Range(.08f,.23f);
                s.Velocity=spread*Range(.8f,1.75f)+axis*.35f+Vector3.up*Range(1.9f,3.3f);
                float size=Range(.045f,.095f)*(i%6==0?1.4f:1);
                s.Scale=new Vector3(size*Range(1.0f,1.7f),size*Range(.55f,.9f),size);
                s.Spin=new Vector3(Range(-270,270),Range(-270,270),Range(-270,270));s.Rotation=Quaternion.Euler(Range(0,180),Range(0,180),Range(0,180));
                s.Root.gameObject.SetActive(true);Pose(s);
            }
            for(int i=0;i<12;i++)
            {
                var c=clouds[cloudIndex++%clouds.Length];float angle=i*Mathf.PI*2/12+Range(-.12f,.12f);
                Vector3 spread=axis*Mathf.Cos(angle)+side*Mathf.Sin(angle);
                c.Age=0;c.Life=Range(.85f,1.4f);c.Width=Range(.68f,1.10f);c.Height=Range(.42f,.64f);c.Spin=Range(-18,18);
                c.Origin=origin+spread*.13f+Vector3.up*.11f;c.Velocity=spread*Range(.6f,1.35f)+axis*.22f+Vector3.up*Range(.10f,.26f);
                c.Root.gameObject.SetActive(true);c.Material.SetFloat("_Age",0);
            }
            if(Debug.isDebugBuild)Debug.Log($"[GroundImpact] cause={cause} origin={origin.ToString("F3")} stones=18 dust=12");
        }
        static void Pose(Stone s)
        {
            float remaining=s.Age,up=s.Velocity.y,height=0,travel=0,speed=1,spinAge=0;
            // An analytic arc keeps collisions stable across render intervals.
            for(int bounce=0;bounce<3;bounce++)
            {
                float flight=2*up/Gravity,part=Mathf.Min(remaining,flight);
                travel+=part*speed;spinAge+=part*speed;
                height=up*part-.5f*Gravity*part*part;remaining-=part;
                if(remaining<=0)break;
                up*=.25f;speed*=.48f;
            }
            if(remaining>0)height=0;
            float fade=1-Mathf.SmoothStep(0,1,(s.Age-(s.Life-.28f))/.28f);
            s.Root.localScale=s.Scale*fade;s.Root.rotation=s.Rotation*Quaternion.Euler(s.Spin*spinAge);
            float bottom=0;foreach(var v in s.Vertices)bottom=Mathf.Min(bottom,(s.Root.rotation*Vector3.Scale(v,s.Root.localScale)).y);
            s.Root.position=s.Origin+Vector3.ProjectOnPlane(s.Velocity,Vector3.up)*travel+Vector3.up*(Mathf.Max(0,height)-bottom);
        }
        public void Tick(Camera camera,float dt)
        {
            dt=Mathf.Max(0,dt);LastAge+=dt;ActiveStones=ActiveClouds=0;
            foreach(var s in stones)
            {
                s.Age+=dt;if(s.Age>=s.Life){s.Root.gameObject.SetActive(false);continue;}
                Pose(s);ActiveStones++;
            }
            foreach(var c in clouds)
            {
                c.Age+=dt;if(c.Age>=c.Life){c.Root.gameObject.SetActive(false);continue;}
                float t=c.Age/c.Life;
                c.Root.position=c.Origin+c.Velocity*(1-Mathf.Exp(-c.Age*1.8f))/.90f;
                c.Root.rotation=camera.transform.rotation*Quaternion.Euler(0,0,c.Spin*t);
                c.Root.localScale=new Vector3(c.Width*Mathf.Lerp(.50f,2.2f,t),c.Height*Mathf.Lerp(.7f,1.7f,t),1);
                c.Material.SetFloat("_Age",t);ActiveClouds++;
            }
        }
        public void Clear()
        {
            foreach(var s in stones){s.Age=10;s.Root.gameObject.SetActive(false);}
            foreach(var c in clouds){c.Age=10;c.Root.gameObject.SetActive(false);}
            ActiveStones=ActiveClouds=0;LastAge=10;
        }
    }
}
