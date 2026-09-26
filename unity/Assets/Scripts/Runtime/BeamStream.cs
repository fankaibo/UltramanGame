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
        readonly Material material,headMaterial;
        readonly Transform head;
        public bool Visible=>ribbon.enabled;
        public Vector3 Tip {get;private set;}
        public float Power {get;private set;}
        public static float Travel(float age)=>Mathf.Clamp01((age-LaunchSeconds)/(Battle.BeamHitSeconds-LaunchSeconds));
        public static float Envelope(float age)=>Mathf.SmoothStep(0,1,(age-LaunchSeconds)/.07f)*
            (1-Mathf.SmoothStep(0,1,(age-(Battle.BeamSeconds-.28f))/.28f));
        public BeamStream(Transform parent)
        {
            material=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("BeamStream")));
            ribbon=new GameObject("Zeperion traveling stream").AddComponent<LineRenderer>();ribbon.transform.SetParent(parent,false);
            ribbon.sharedMaterial=material;ribbon.positionCount=2;ribbon.numCapVertices=0;ribbon.widthMultiplier=.52f;
            ribbon.widthCurve=new AnimationCurve(new Keyframe(0,.65f),new Keyframe(.28f,1),new Keyframe(1,.8f));
            ribbon.startColor=ribbon.endColor=Color.white;
            headMaterial=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("EnergyFlare")));
            head=GameWorld.Primitive("Zeperion contact glow",PrimitiveType.Quad,parent,Vector3.zero,Vector3.one,headMaterial);
            Clear();
        }
        public void Clear(){ribbon.enabled=false;head.gameObject.SetActive(false);Power=0;}
        public void Tick(Camera camera,Vector3 origin,Vector3 target,float age,bool firing,float clock)
        {
            if(!firing){Clear();return;}
            Power=Envelope(age);float travel=Travel(age);
            Tip=Vector3.Lerp(origin,target,travel);ribbon.enabled=true;
            ribbon.SetPosition(0,origin);ribbon.SetPosition(1,Tip);
            ribbon.widthMultiplier=.52f*Mathf.Lerp(.55f,1,Power);
            material.SetFloat("_Clock",clock);material.SetFloat("_Length",Vector3.Distance(origin,Tip));material.SetFloat("_Power",Power);
            head.gameObject.SetActive(true);head.position=Tip-camera.transform.forward*.025f;head.rotation=camera.transform.rotation;
            float pulse=1+.06f*Mathf.Sin(clock*21);
            head.localScale=Vector3.one*(travel<1?.48f:.95f)*pulse;
            headMaterial.color=new Color(.3f,.70f,1,Power*(travel<1?.65f:.9f));
        }
    }
}
