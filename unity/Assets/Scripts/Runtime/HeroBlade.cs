using UnityEngine;
using UnityEngine.Rendering;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    // The Mebium Blade is a rigid light blade emitted by the left brace.
    public sealed class HeroBlade
    {
        public const float Length=1.32f;
        readonly Transform blade,heart;
        readonly MeshRenderer bladeRenderer,heartRenderer;
        readonly Material bladeMaterial,heartMaterial;
        readonly Mesh sweep;
        readonly MeshRenderer sweepRenderer;
        const int Capacity=12;
        readonly Vector3[] roots=new Vector3[Capacity],tips=new Vector3[Capacity],vertices=new Vector3[Capacity*2];
        readonly Color[] colors=new Color[Capacity*2];
        readonly Vector2[] uv=new Vector2[Capacity*2];
        readonly float[] ages=new float[Capacity];
        int count,sequence=-1;float lastAge=-1;
        string heroId="Tiga";AnimatedActor actor;Battle observed;
        public bool Visible=>bladeRenderer.enabled;
        public bool SweepVisible=>sweepRenderer.enabled;
        public Vector3 Tip {get;private set;}
        public Vector3 Origin {get;private set;}
        public Vector3 Direction {get;private set;}
        public HeroBlade(Transform parent)
        {
            var mesh=Shape(parent);
            blade=Surface(parent,"Mebium blade amber volume",mesh,out bladeRenderer,out bladeMaterial);
            heart=Surface(parent,"Mebium blade hot heart",mesh,out heartRenderer,out heartMaterial);heartMaterial.SetFloat("_Heart",1);
            sweep=RuntimeResources.Own(parent,new Mesh{name="Mebium sampled slash"});sweep.MarkDynamic();
            var go=new GameObject("Mebium short slash wake",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh=sweep;sweepRenderer=go.GetComponent<MeshRenderer>();
            sweepRenderer.sharedMaterial=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("MebiumBladeSweep")));
            sweepRenderer.shadowCastingMode=ShadowCastingMode.Off;sweepRenderer.receiveShadows=false;
            int[] triangles=new int[(Capacity-1)*6];
            for(int i=0;i<Capacity-1;i++){int v=i*2,t=i*6;triangles[t]=v;triangles[t+1]=v+1;triangles[t+2]=v+2;triangles[t+3]=v+1;triangles[t+4]=v+3;triangles[t+5]=v+2;}
            sweep.vertices=vertices;sweep.triangles=triangles;Clear();
        }
        static Transform Surface(Transform parent,string name,Mesh mesh,out MeshRenderer renderer,out Material material)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;renderer=go.GetComponent<MeshRenderer>();
            material=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("MebiumBlade")));renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return go.transform;
        }
        static Mesh Shape(Transform parent)
        {
            const int rings=18,sides=8;var vertices=new Vector3[(rings+1)*sides];var uv=new Vector2[vertices.Length];var indices=new int[rings*sides*6];
            for(int ring=0;ring<=rings;ring++)
            {
                float t=ring/(float)rings;
                float width=(.045f+.145f*Mathf.Sin(Mathf.Pow(t,.65f)*Mathf.PI))*(1-t);
                for(int side=0;side<sides;side++)
                {
                    float angle=side/(float)sides*Mathf.PI*2;int v=ring*sides+side;
                    vertices[v]=new Vector3(Mathf.Cos(angle)*width,Mathf.Sin(angle)*width*.33f,t);
                    uv[v]=new Vector2(t,side/(float)sides);
                    if(ring==rings)continue;int n=ring*sides+(side+1)%sides,k=(ring*sides+side)*6;
                    indices[k]=v;indices[k+1]=n;indices[k+2]=n+sides;indices[k+3]=v;indices[k+4]=n+sides;indices[k+5]=v+sides;
                }
            }
            var mesh=RuntimeResources.Own(parent,new Mesh{name="Mebium tapered solid light blade",vertices=vertices,uv=uv,triangles=indices});mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        public void BindActor(AnimatedActor value){Clear();actor=value;}
        public void SetHero(string id){heroId=id;Clear();}
        public void Clear(){bladeRenderer.enabled=heartRenderer.enabled=sweepRenderer.enabled=false;count=0;sequence=-1;lastAge=-1;}
        public void Tick(Battle state,Vector3 wrist,Vector3 forward,Vector3 target,bool suppressed)
        {
            if(!ReferenceEquals(observed,state)){Clear();observed=state;}
            if(suppressed||!HeroArsenal.Blade(heroId,state)){Clear();return;}
            float age=state.ActionAge;
            if(sequence!=state.AttackSequence||age<lastAge){count=0;lastAge=-1;sequence=state.AttackSequence;}
            float grow=MonsterRockMotion.Smooth(age/.045f)*(1-MonsterRockMotion.Smooth((age-.28f)/.10f));
            Origin=actor!=null?actor.BladeOrigin:wrist;
            Direction=actor!=null?actor.BladeDirection:(target-wrist).normalized;
            Tip=Origin+Direction*(Length*grow);
            bladeRenderer.enabled=heartRenderer.enabled=grow>.02f;
            blade.position=heart.position=Origin;
            Vector3 up=actor!=null?actor.BladeNormal:Vector3.ProjectOnPlane(Vector3.up,Direction).normalized;
            if(up.sqrMagnitude<.01f)up=Vector3.Cross(Direction,forward).normalized;
            if(up.sqrMagnitude<.01f)up=Vector3.up;
            blade.rotation=heart.rotation=Quaternion.LookRotation(Direction,up);
            blade.localScale=new Vector3(1,1,Length*grow);heart.localScale=new Vector3(.38f,.70f,Length*grow*.985f);
            bladeMaterial.SetFloat("_Age",age);heartMaterial.SetFloat("_Age",age);
            bladeMaterial.SetFloat("_Opacity",grow);heartMaterial.SetFloat("_Opacity",grow);
            // Use the real socket and tip history. The wake lies on the swept
            // blade surface, never on a camera-facing substitute slash.
            if(age>lastAge+.000001f)
            {
                int expired=0;while(expired<count&&age-ages[expired]>.065f)expired++;
                if(expired>0){for(int i=expired;i<count;i++){roots[i-expired]=roots[i];tips[i-expired]=tips[i];ages[i-expired]=ages[i];}count-=expired;}
                if(Visible&&age>=.055f&&age<.25f)
                {
                    if(count==Capacity){for(int i=1;i<count;i++){roots[i-1]=roots[i];tips[i-1]=tips[i];ages[i-1]=ages[i];}count--;}
                    roots[count]=Vector3.Lerp(Origin,Tip,.12f);tips[count]=Tip;ages[count]=age;count++;
                }
            }
            lastAge=age;sweepRenderer.enabled=count>1;
            for(int i=0;i<Capacity;i++)
            {
                if(i>=count){vertices[i*2]=vertices[i*2+1]=Tip;colors[i*2]=colors[i*2+1]=Color.clear;continue;}
                vertices[i*2]=roots[i];vertices[i*2+1]=tips[i];
                float freshness=Mathf.Clamp01(1-(age-ages[i])/.065f);
                colors[i*2]=colors[i*2+1]=new Color(1,1,1,freshness*freshness*.30f);
                uv[i*2]=new Vector2(i/(float)Mathf.Max(1,count-1),0);uv[i*2+1]=new Vector2(i/(float)Mathf.Max(1,count-1),1);
            }
            sweep.vertices=vertices;sweep.colors=colors;sweep.uv=uv;sweep.RecalculateBounds();
        }
    }
}
