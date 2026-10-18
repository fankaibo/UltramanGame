using UnityEngine;

namespace UltramanGame.Runtime
{
    // One bounded volume carries the six timed dust fronts. It does not face
    // the camera, and owns no per-impact objects or autonomous particle clock.
    public sealed class SlamDustVolume
    {
        public const float Lifetime=1.65f;
        readonly Transform volume;
        readonly Material material;
        public float Age {get;private set;}=Lifetime;
        public bool Visible=>volume.gameObject.activeSelf;
        public SlamDustVolume(Transform parent)
        {
            material=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("SlamDustVolume")));
            material.SetTexture("_Noise",RuntimeResources.Own(parent,VolcanoStage.CreatePlumeNoise()));
            volume=GameWorld.Primitive("Slam rolling dust volume",PrimitiveType.Cube,parent,Vector3.zero,Vector3.one,material);
            var renderer=volume.GetComponent<Renderer>();renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            Clear();
        }
        public void Hit(Vector3 origin,Vector3 direction,float length)
        {
            var axis=Vector3.ProjectOnPlane(direction,Vector3.up).normalized;if(axis.sqrMagnitude<.001f)axis=Vector3.forward;
            float span=Mathf.Clamp(length,0,12);var size=new Vector3(4,2.4f,span+3);
            volume.SetPositionAndRotation(new Vector3(origin.x,1.2f,origin.z)+axis*(span*.5f),Quaternion.LookRotation(axis));
            volume.localScale=size;material.SetVector("_Extent",size);material.SetFloat("_Span",span);
            Age=0;Sample();
        }
        public void Tick(float dt){if(Age>=Lifetime)return;Age=Mathf.Min(Lifetime,Age+Mathf.Max(0,dt));Sample();}
        void Sample(){volume.gameObject.SetActive(Age<Lifetime);material.SetFloat("_Age",Age);}
        public void Clear(){Age=Lifetime;volume.gameObject.SetActive(false);}
    }
}
