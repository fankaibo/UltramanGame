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
            contact=position;axis=direction.normalized;Age=0;Bursts++;
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
            if(!live)return;
            float expansion=1-Mathf.Exp(-Age*5.2f);
            cloud.position=contact-axis*(.22f+expansion*.15f)+Vector3.up*(.04f+Age*.22f);
            cloud.localScale=Vector3.Lerp(new Vector3(.65f,.7f,.6f),new Vector3(2.65f,2.2f,2.1f),expansion);
            float radius=.55f+2.4f*(1-Mathf.Exp(-Mathf.Max(0,Age-.08f)*2.6f));
            ash.position=new Vector3(contact.x,.31f,contact.z);ash.localScale=new Vector3(radius*2,.6f,radius*2);
            cloudMaterial.SetFloat("_Age",Age);ashMaterial.SetFloat("_Age",Age);
        }
        public void Clear(){Age=Lifetime;Bursts=0;cloud.gameObject.SetActive(false);ash.gameObject.SetActive(false);}
    }
}
