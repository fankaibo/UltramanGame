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
        readonly LineRenderer[] bladeTrails=new LineRenderer[2];
        readonly RangedSkillAccent accents;
        Vector3 flightAxis,flightSide,contactPoint;
        Quaternion spinBasis;
        ZeroSluggerRig originalSluggers;
        readonly Vector3[] bladeOrigins=new Vector3[2];
        readonly Quaternion[] bladeRotations=new Quaternion[2];
        bool OriginalBlades=>useSluggers&&originalSluggers!=null;
        public void BindSluggers(ZeroSluggerRig rig){Clear();originalSluggers=rig;}
        bool contacted;
        public bool LaunchVisible=>accents.LaunchVisible;
        public bool ImpactVisible=>accents.ImpactVisible;
        public int Impacts=>accents.Impacts;
        public Color Tint=>tint;
        public Vector3 BladePosition(int i)=>sluggers[i].position;
        public Vector3 BladeTrailTip(int i)=>bladeTrails[i].GetPosition(bladeTrails[i].positionCount-1);
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
            core=RangedSkillAccent.Volume(parent,"Light bullet moving energy",out coreMat);
            accents=new RangedSkillAccent(parent);
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
            trail.shadowCastingMode=ShadowCastingMode.Off;trail.receiveShadows=false;
            var bladeTrailMaterial=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("ChargeFilament")));
            for(int i=0;i<2;i++)
            {
                var line=new GameObject("Zero curved wake "+i).AddComponent<LineRenderer>();line.transform.SetParent(parent,false);
                line.sharedMaterial=bladeTrailMaterial;line.positionCount=20;line.widthMultiplier=.16f;
                line.widthCurve=new AnimationCurve(new Keyframe(0,0),new Keyframe(.75f,1),new Keyframe(1,.4f));
                line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;bladeTrails[i]=line;
            }
            Clear();
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
        void HideFlight()
        {originalSluggers?.Restore();Visible=false;core.gameObject.SetActive(false);halo.gameObject.SetActive(false);wave.gameObject.SetActive(false);trail.enabled=false;foreach(var blade in sluggers)blade.gameObject.SetActive(false);foreach(var line in bladeTrails)line.enabled=false;}
        public void Clear(){Started=false;HideFlight();accents.Clear();contacted=false;}
        public void Impact(Vector3 point,Vector3 direction)
        {contacted=true;contactPoint=point;accents.Hit(point,direction,tint);}
        Vector3 BladePoint(float age,int index,Vector3 target,Vector3 head)
        {
            float sign=index==0?-1:1;
            if(age<=AttackTempo.RangedHitSeconds)
            {
                float t=AttackTempo.Travel(age),arc=Mathf.Sin(t*Mathf.PI);
                return Vector3.Lerp(OriginalBlades?bladeOrigins[index]:origin+flightSide*(sign*.18f),target,t)+flightSide*(sign*arc*.44f)+Vector3.up*(arc*.20f);
            }
            float back=Mathf.Clamp01((age-AttackTempo.RangedHitSeconds)/(AttackTempo.RangedSeconds-AttackTempo.RangedHitSeconds));
            float curve=Mathf.Sin(back*Mathf.PI);
            return Vector3.Lerp(target,OriginalBlades?originalSluggers.MountedCenter(index):head+flightSide*(sign*.18f),back)+flightSide*(sign*curve*.65f)+Vector3.up*(curve*.35f);
        }
        public void Tick(Battle state,Camera camera,Vector3 hand,Vector3 target,bool suppressed,Vector3? head=null,float dt=0)
        {
            Started=false;
            if(!ReferenceEquals(observed,state)){observed=state;sequence=-1;Launches=0;Clear();accents.NewRound();}
            if(suppressed||state.Phase!=GamePhase.Battle){Clear();return;}
            accents.TickImpact(dt);
            bool flight=state.Shot.Active;
            bool preparing=state.IsRangedPunch&&state.ActionAge<AttackTempo.RangedLaunchSeconds;
            Vector3 launchPoint=preparing?(OriginalBlades?(originalSluggers.MountedCenter(0)+originalSluggers.MountedCenter(1))*.5f:useSluggers?(head??hand):hand):origin;
            // Pre-release light follows the active hand; after release it stays at the muzzle.
            accents.Launch(preparing||flight,preparing?state.ActionAge:state.Shot.Age,launchPoint,(target-launchPoint).normalized,tint);
            if(!flight){HideFlight();return;}
            if(sequence!=state.Shot.Sequence)
            {
                sequence=state.Shot.Sequence;origin=useSluggers&&head.HasValue?head.Value:hand;
                if(OriginalBlades)
                {
                    for(int i=0;i<2;i++){bladeOrigins[i]=originalSluggers.MountedCenter(i);bladeRotations[i]=originalSluggers.MountedRotation(i);}
                    origin=(bladeOrigins[0]+bladeOrigins[1])*.5f;
                }
                flightAxis=(target-origin).normalized;flightSide=Vector3.Cross(Vector3.up,flightAxis).normalized;
                spinBasis=camera.transform.rotation;contacted=false;Launches++;Started=true;
                accents.Launch(true,state.Shot.Age,origin,flightAxis,tint);
            }
            float age=state.Shot.Age;
            Visible=age<=(useSluggers?AttackTempo.RangedSeconds:AttackTempo.RangedHitSeconds+.055f);
            core.gameObject.SetActive(Visible&&!useSluggers);halo.gameObject.SetActive(Visible&&!useSluggers);wave.gameObject.SetActive(Visible&&!useSluggers);trail.enabled=Visible&&!useSluggers;
            foreach(var blade in sluggers)blade.gameObject.SetActive(Visible&&useSluggers&&!OriginalBlades);
            foreach(var line in bladeTrails)line.enabled=Visible&&useSluggers;
            if(!Visible){originalSluggers?.Restore();return;}
            float t=AttackTempo.Travel(age),fade=1-Mathf.Clamp01((age-AttackTempo.RangedHitSeconds)/.055f);
            Vector3 end=contacted?contactPoint:target;
            Tip=Vector3.Lerp(origin,end,t);
            if(useSluggers)
            {
                var home=head??origin;
                for(int i=0;i<2;i++)
                {
                    sluggers[i].position=BladePoint(age,i,end,home);
                    sluggers[i].rotation=spinBasis*Quaternion.Euler(0,15,age*(i==0?1200:-1200));sluggers[i].localScale=Vector3.one*.64f;
                    if(OriginalBlades)
                    {
                        float back=Mathf.Clamp01((age-AttackTempo.RangedHitSeconds)/(AttackTempo.RangedSeconds-AttackTempo.RangedHitSeconds));
                        Quaternion basis=Quaternion.Slerp(bladeRotations[i],originalSluggers.MountedRotation(i),back);
                        float turn=age<=AttackTempo.RangedHitSeconds?t:back;
                        var rotation=Quaternion.AngleAxis((i==0?360:-360)*turn,flightSide)*basis;
                        originalSluggers.Pose(i,sluggers[i].position,rotation);
                    }
                    var line=bladeTrails[i];line.startColor=new Color(tint.r,tint.g,tint.b,0);line.endColor=new Color(.80f,.94f,1,.92f);
                    for(int j=0;j<line.positionCount;j++)
                    {float sample=Mathf.Max(AttackTempo.RangedLaunchSeconds,age-.075f*(1-j/(float)(line.positionCount-1)));line.SetPosition(j,BladePoint(sample,i,end,home));}
                }
                Tip=(sluggers[0].position+sluggers[1].position)*.5f;
                return;
            }
            core.position=halo.position=wave.position=Tip;
            core.rotation=Quaternion.LookRotation(flightAxis);halo.rotation=camera.transform.rotation;
            wave.rotation=Quaternion.LookRotation(flightAxis)*Quaternion.Euler(0,0,age*320);
            core.localScale=new Vector3(.25f,.25f,.65f);halo.localScale=Vector3.one*.85f;
            wave.localScale=Vector3.one*(.50f+.08f*Mathf.Sin(t*Mathf.PI));
            coreMat.color=new Color(tint.r,tint.g,tint.b,fade);coreMat.SetFloat("_Age",age);
            haloMat.color=new Color(tint.r,tint.g,tint.b,.50f*fade);
            waveMat.color=new Color(tint.r,tint.g,tint.b,.60f*fade);
            trailMat.color=new Color(tint.r,tint.g,tint.b,.70f*fade);
            for(int i=0;i<8;i++)trail.SetPosition(i,Vector3.Lerp(origin,end,Mathf.Max(0,t-.30f+i/7f*.30f)));
        }
    }
}
