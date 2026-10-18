using UnityEngine;
using UnityEngine.Rendering;

namespace UltramanGame.Runtime
{
    // A world-space discharge, shared by all views. The existing beam clock
    // owns its traveling endpoint and power; this layer cannot deliver damage.
    public sealed class ZeperionVolume
    {
        readonly Transform body;
        readonly Material material;
        public bool Visible=>body.gameObject.activeSelf;
        public ZeperionVolume(Transform parent)
        {
            material=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("ZeperionVolume")));
            material.SetTexture("_Noise",RuntimeResources.Own(parent,VolcanoStage.CreatePlumeNoise()));
            body=GameWorld.Primitive("Zeperion spatial discharge",PrimitiveType.Cube,parent,Vector3.zero,Vector3.one,material);
            var renderer=body.GetComponent<Renderer>();renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            Clear();
        }
        public void Clear()=>body.gameObject.SetActive(false);
        public void Tick(Vector3 origin,Vector3 tip,float power,float clock,Color tint,Color accent)
        {
            Vector3 axis=tip-origin;float length=axis.magnitude;
            bool live=length>.001f&&power>.0001f;body.gameObject.SetActive(live);if(!live)return;
            body.position=(origin+tip)*.5f;body.rotation=Quaternion.LookRotation(axis/length);
            float diameter=.74f*Mathf.Lerp(.55f,1,power);body.localScale=new Vector3(diameter,diameter,length);
            material.SetFloat("_Length",length);material.SetFloat("_Clock",clock);material.SetFloat("_Power",power);
            material.SetColor("_BeamTint",tint);material.SetColor("_BeamAccent",accent);
        }
    }
}
