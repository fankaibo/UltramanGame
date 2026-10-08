using UnityEngine;
using UnityEngine.Rendering;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    // One accepted strike owns one flight. Damage stays in Battle, with the
    // same tempo clock used here; cancellation/pause cannot leave ghost hits.
    public sealed class HeroProjectile
    {
        readonly Transform core,halo,wave;
        readonly Material coreMat,haloMat,waveMat,trailMat;
        readonly LineRenderer trail;
        readonly Transform[] sluggers=new Transform[2];
        bool useSluggers;
        Battle observed;
        int sequence=-1;
        Color tint=new Color(.18f,.7f,1);
        Vector3 origin;
        public bool Visible {get;private set;}
        public bool Started {get;private set;}
        public int Launches {get;private set;}
        public Vector3 Origin=>origin;
        public Vector3 Tip {get;private set;}
        public HeroProjectile(Transform parent)
        {
            core=Quad(parent,"Light bullet core",out coreMat);
            halo=Quad(parent,"Light bullet aura",out haloMat);
            wave=Quad(parent,"Light bullet travelling wave",out waveMat);waveMat.SetFloat("_Ring",1);
            var bladeMaterial=RuntimeResources.Own(parent,new Material(Shader.Find("Standard")){color=new Color(.75f,.88f,1)});
            bladeMaterial.SetFloat("_Metallic",.78f);bladeMaterial.SetFloat("_Glossiness",.8f);
            bladeMaterial.EnableKeyword("_EMISSION");bladeMaterial.SetColor("_EmissionColor",new Color(.12f,.42f,.65f));
            var bladeMesh=SluggerMesh(parent);
            for(int i=0;i<2;i++)
            {
                var go=new GameObject("Zero Slugger "+i,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
                go.GetComponent<MeshFilter>().sharedMesh=bladeMesh;go.GetComponent<MeshRenderer>().sharedMaterial=bladeMaterial;sluggers[i]=go.transform;
            }
            trailMat=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("StrikeRibbon")));
            trail=new GameObject("Light bullet short trail").AddComponent<LineRenderer>();trail.transform.SetParent(parent,false);
            trail.sharedMaterial=trailMat;trail.positionCount=8;trail.widthMultiplier=.30f;
            trail.widthCurve=new AnimationCurve(new Keyframe(0,0),new Keyframe(.8f,1),new Keyframe(1,.35f));
            trail.shadowCastingMode=ShadowCastingMode.Off;trail.receiveShadows=false;Clear();
        }
        static Transform Quad(Transform parent,string name,out Material material)
        {
            material=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("EnergyFlare")));
            var q=GameWorld.Primitive(name,PrimitiveType.Quad,parent,Vector3.zero,Vector3.one,material);
            q.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;q.GetComponent<Renderer>().receiveShadows=false;return q;
        }
        static Mesh SluggerMesh(Transform parent)
        {
            const int n=24;var vertices=new Vector3[(n+1)*4];var triangles=new int[n*24];
            for(int i=0;i<=n;i++)
            {
                float t=i/(float)n,angle=Mathf.Lerp(-115,115,t)*Mathf.Deg2Rad;
                float thickness=.17f*Mathf.Sin(t*Mathf.PI);float x=Mathf.Cos(angle),y=Mathf.Sin(angle);
                vertices[i*4]=new Vector3(x*.52f,y*.70f,.035f);vertices[i*4+1]=new Vector3(x*(.52f-thickness)-thickness*.5f,y*(.70f-thickness),.035f);
                vertices[i*4+2]=vertices[i*4]-Vector3.forward*.07f;vertices[i*4+3]=vertices[i*4+1]-Vector3.forward*.07f;
                if(i==n)continue;int a=i*4,k=i*24;
                int[] face={a,a+4,a+1,a+1,a+4,a+5,a+2,a+3,a+6,a+3,a+7,a+6,a,a+2,a+4,a+2,a+6,a+4,a+1,a+5,a+3,a+3,a+5,a+7};
                for(int j=0;j<24;j++)triangles[k+j]=face[j];
            }
            var mesh=RuntimeResources.Own(parent,new Mesh{name="Zero crescent blade",vertices=vertices,triangles=triangles});mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        public void SetHero(string id)
        {useSluggers=HeroArsenal.Sluggers(id);if(ColorUtility.TryParseHtmlString(HeroRoster.At(HeroRoster.Index(id)).BeamTint,out var color))tint=color;Clear();}
        public void Clear(){Visible=Started=false;core.gameObject.SetActive(false);halo.gameObject.SetActive(false);wave.gameObject.SetActive(false);trail.enabled=false;foreach(var blade in sluggers)blade.gameObject.SetActive(false);}
        public void Tick(Battle state,Camera camera,Vector3 hand,Vector3 target,bool suppressed,Vector3? head=null)
        {
            Started=false;
            if(!ReferenceEquals(observed,state)){observed=state;sequence=-1;Launches=0;Clear();}
            if(suppressed||state.Phase!=GamePhase.Battle||!state.Shot.Active)
            {Clear();return;}
            if(sequence!=state.Shot.Sequence)
            {sequence=state.Shot.Sequence;origin=useSluggers&&head.HasValue?head.Value:hand;Launches++;Started=true;}
            float age=state.Shot.Age;
            Visible=age<=(useSluggers?AttackTempo.RangedSeconds:AttackTempo.RangedHitSeconds+.055f);
            core.gameObject.SetActive(Visible&&!useSluggers);halo.gameObject.SetActive(Visible);wave.gameObject.SetActive(Visible&&!useSluggers);trail.enabled=Visible;
            foreach(var blade in sluggers)blade.gameObject.SetActive(Visible&&useSluggers);
            if(!Visible)return;
            float t=AttackTempo.Travel(age),fade=1-Mathf.Clamp01((age-AttackTempo.RangedHitSeconds)/.055f);
            Tip=Vector3.Lerp(origin,target,t);
            Vector3 from=origin,to=target;
            if(useSluggers)
            {
                bool returning=age>AttackTempo.RangedHitSeconds;fade=1;
                if(returning){from=target;to=head??origin;t=Mathf.Clamp01((age-AttackTempo.RangedHitSeconds)/(AttackTempo.RangedSeconds-AttackTempo.RangedHitSeconds));}
                Tip=Vector3.Lerp(from,to,t)+camera.transform.up*(Mathf.Sin(t*Mathf.PI)*(returning?.35f:.20f));
                for(int i=0;i<2;i++)
                {sluggers[i].position=Tip+camera.transform.right*((i==0?-1:1)*.22f);sluggers[i].rotation=camera.transform.rotation*Quaternion.Euler(0,15,age*(i==0?1200:-1200));sluggers[i].localScale=Vector3.one*.64f;}
            }
            core.position=halo.position=wave.position=Tip;
            core.rotation=halo.rotation=camera.transform.rotation;
            wave.rotation=camera.transform.rotation*Quaternion.Euler(0,25,age*320);
            core.localScale=Vector3.one*.38f;halo.localScale=Vector3.one*.85f;
            wave.localScale=new Vector3(.72f,.50f,1)*(1+.12f*Mathf.Sin(t*Mathf.PI));
            coreMat.color=new Color(1,.97f,.85f,fade);
            haloMat.color=new Color(tint.r,tint.g,tint.b,.72f*fade);
            waveMat.color=new Color(tint.r,tint.g,tint.b,.58f*fade);
            trailMat.color=new Color(tint.r,tint.g,tint.b,.65f*fade);
            for(int i=0;i<8;i++)trail.SetPosition(i,Vector3.Lerp(from,to,Mathf.Max(0,t-.28f+i/7f*.28f)));
        }
    }
}
