using UnityEngine;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    // Uses Battle's contact clock: a short traveling head, sustained discharge,
    // then a soft power-down. This never decides damage or extends input locks.
    public sealed class BeamStream
    {
        public const float LaunchSeconds=.28f;
        readonly LineRenderer ribbon;
        readonly LineRenderer twinRibbon;
        readonly Material material,headMaterial;
        Color tint=new Color(.25f,.72f,1),accent=new Color(.90f,.98f,1);
        bool dual;
        bool atmos;
        readonly AtmosWave wave;
        public AtmosWave Wave=>wave;
        readonly Transform head;
        public bool Visible=>ribbon.enabled||wave.Visible;
        public Vector3 Tip {get;private set;}
        public float Power {get;private set;}
        public static float Travel(float age)=>Mathf.Clamp01((age-LaunchSeconds)/(Battle.BeamHitSeconds-LaunchSeconds));
        public static float Envelope(float age)=>Mathf.SmoothStep(0,1,(age-LaunchSeconds)/.07f)*
            (1-Mathf.SmoothStep(0,1,(age-(Battle.BeamSeconds-.28f))/.28f));
        public BeamStream(Transform parent)
        {
            wave=new AtmosWave(parent);
            material=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("BeamStream")));
            ribbon=new GameObject("Zeperion traveling stream").AddComponent<LineRenderer>();ribbon.transform.SetParent(parent,false);
            ribbon.sharedMaterial=material;ribbon.positionCount=2;ribbon.numCapVertices=0;ribbon.widthMultiplier=.74f;
            ribbon.widthCurve=new AnimationCurve(new Keyframe(0,.72f),new Keyframe(1,1));
            ribbon.startColor=ribbon.endColor=Color.white;
            twinRibbon=new GameObject("Twin finisher traveling stream").AddComponent<LineRenderer>();twinRibbon.transform.SetParent(parent,false);
            twinRibbon.sharedMaterial=material;twinRibbon.positionCount=2;twinRibbon.numCapVertices=0;twinRibbon.widthMultiplier=.52f;
            twinRibbon.widthCurve=new AnimationCurve(new Keyframe(0,.72f),new Keyframe(1,1));
            twinRibbon.startColor=twinRibbon.endColor=Color.white;
            headMaterial=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("EnergyFlare")));
            head=GameWorld.Primitive("Zeperion contact glow",PrimitiveType.Quad,parent,Vector3.zero,Vector3.one,headMaterial);
            Clear();
        }
        public void SetProfile(Color beamTint,Color beamAccent,bool twin)
        {tint=beamTint;accent=beamAccent;dual=twin;material.SetColor("_BeamTint",tint);material.SetColor("_BeamAccent",accent);}
        public void SetAtmos(bool value){if(atmos!=value)Clear();atmos=value;}
        public void Clear(){ribbon.enabled=false;twinRibbon.enabled=false;head.gameObject.SetActive(false);wave.Clear();Power=0;}
        public void Tick(Camera camera,Vector3 origin,Vector3 target,float age,bool firing,float clock)
        {
            if(!firing){Clear();return;}
            Power=Envelope(age);float travel=Travel(age);
            Tip=Vector3.Lerp(origin,target,travel);ribbon.enabled=!atmos;
            wave.Sample(atmos,origin,target,age,tint,accent);
            ribbon.SetPosition(0,origin);ribbon.SetPosition(1,Tip);
            Vector3 lateral=Vector3.Cross(camera.transform.forward,Tip-origin).normalized*.095f;
            twinRibbon.enabled=dual&&!atmos;
            if(dual){twinRibbon.SetPosition(0,origin+lateral);twinRibbon.SetPosition(1,Tip+lateral);}
            ribbon.widthMultiplier=.74f*Mathf.Lerp(.55f,1,Power);
            twinRibbon.widthMultiplier=.52f*Mathf.Lerp(.55f,1,Power);
            material.SetFloat("_Clock",clock);material.SetFloat("_Length",Vector3.Distance(origin,Tip));material.SetFloat("_Power",Power);
            head.gameObject.SetActive(true);head.position=Tip-camera.transform.forward*.025f;head.rotation=camera.transform.rotation;
            float pulse=1+.06f*Mathf.Sin(clock*21);
            head.localScale=Vector3.one*(travel<1?.48f:.95f)*pulse;
            headMaterial.color=Color.Lerp(tint,accent,.45f)*new Color(1,1,1,Power*(atmos?.20f:travel<1?.65f:.9f));
        }
    }
}
