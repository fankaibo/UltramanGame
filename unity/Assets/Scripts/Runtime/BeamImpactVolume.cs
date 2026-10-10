using UnityEngine;

namespace UltramanGame.Runtime
{
    // One contact event owns both volumes. Explicit time keeps capture, pause,
    // reset and the rendered result in agreement without particle simulation.
    public sealed class BeamImpactVolume
    {
        public const float Lifetime=1.85f;
        readonly Transform cloud,ash;
        readonly Material cloudMaterial,ashMaterial;
        readonly LineRenderer shockRing;
        readonly Material ringMaterial;
        Vector3 contact,axis;
        public float Age {get;private set;}=Lifetime;
        public bool Visible=>cloud.gameObject.activeSelf||ash.gameObject.activeSelf;
        public int Bursts {get;private set;}
        public BeamImpactVolume(Transform parent)
        {
            var noise=RuntimeResources.Own(parent,VolcanoStage.CreatePlumeNoise());
            var shader=Resources.Load<Shader>("BeamImpactVolume");
            cloudMaterial=RuntimeResources.Own(parent,new Material(shader));
            ashMaterial=RuntimeResources.Own(parent,new Material(shader));
            cloudMaterial.SetTexture("_Noise",noise);ashMaterial.SetTexture("_Noise",noise);ashMaterial.SetFloat("_Ground",1);
            cloud=Make(parent,"Beam contact billows",cloudMaterial);
            ash=Make(parent,"Beam rolling ground ash",ashMaterial);
            ringMaterial=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("SoftGlow")));
            shockRing=new GameObject("Beam contact shock ring").AddComponent<LineRenderer>();
            shockRing.transform.SetParent(parent,false);shockRing.sharedMaterial=ringMaterial;
            shockRing.positionCount=33;shockRing.numCapVertices=3;shockRing.widthMultiplier=.08f;
            shockRing.widthCurve=new AnimationCurve(new Keyframe(0,.32f),new Keyframe(.18f,1),new Keyframe(.82f,.72f),new Keyframe(1,0));
            shockRing.enabled=false;
            Clear();
        }
        static Transform Make(Transform parent,string name,Material material)
        {
            var volume=GameWorld.Primitive(name,PrimitiveType.Cube,parent,Vector3.zero,Vector3.one,material);
            var renderer=volume.GetComponent<Renderer>();renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            return volume;
        }
        public void Hit(Vector3 position,Vector3 direction)
        {
            contact=position;axis=direction.sqrMagnitude>.0001f?direction.normalized:Vector3.forward;Age=0;Bursts++;
            cloud.rotation=Quaternion.LookRotation(axis,Vector3.up);ash.rotation=Quaternion.identity;
            Sample();
            if(Debug.isDebugBuild)Debug.Log("[BeamImpactVolume] begin");
        }
        public void Tick(float dt)
        {
            if(Age>=Lifetime)return;
            Age=Mathf.Min(Lifetime,Age+Mathf.Max(0,dt));Sample();
        }
        void Sample()
        {
            bool live=Age<Lifetime;cloud.gameObject.SetActive(live);ash.gameObject.SetActive(live&&Age>.08f);
            bool ringLive=live&&Age<.52f;shockRing.enabled=ringLive;
            if(!live)return;
            float expansion=1-Mathf.Exp(-Age*5.2f);
            cloud.position=contact-axis*(.22f+expansion*.15f)+Vector3.up*(.04f+Age*.22f);
            cloud.localScale=Vector3.Lerp(new Vector3(.65f,.7f,.6f),new Vector3(2.65f,2.2f,2.1f),expansion);
            float radius=.55f+2.4f*(1-Mathf.Exp(-Mathf.Max(0,Age-.08f)*2.6f));
            ash.position=new Vector3(contact.x,.31f,contact.z);ash.localScale=new Vector3(radius*2,.6f,radius*2);
            cloudMaterial.SetFloat("_Age",Age);ashMaterial.SetFloat("_Age",Age);
            if(ringLive)
            {
                float t=Mathf.Clamp01(Age/.52f);
                float ringRadius=.22f+1.05f*Mathf.SmoothStep(0,1,t);
                Vector3 tangent=Vector3.Cross(Vector3.up,axis);
                if(tangent.sqrMagnitude<.001f)tangent=Vector3.Cross(Vector3.forward,axis);
                tangent.Normalize();Vector3 bitangent=Vector3.Cross(axis,tangent).normalized;
                for(int i=0;i<shockRing.positionCount;i++)
                {
                    float a=i*Mathf.PI*2/(shockRing.positionCount-1);
                    shockRing.SetPosition(i,contact-axis*.045f+
                        tangent*Mathf.Cos(a)*ringRadius+bitangent*Mathf.Sin(a)*ringRadius*.82f);
                }
                Color ring=new Color(.34f,.78f,1, (1-Mathf.SmoothStep(0,1,t))*.86f);
                shockRing.startColor=ring;shockRing.endColor=new Color(ring.r,ring.g,ring.b,0);
                shockRing.widthMultiplier=Mathf.Lerp(.105f,.028f,t);
            }
        }
        public void Clear(){Age=Lifetime;Bursts=0;cloud.gameObject.SetActive(false);ash.gameObject.SetActive(false);shockRing.enabled=false;}
    }
}
