using UnityEngine;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    // Converging trails and a depth-tested energy volume share the closeup
    // clock. No particle simulation, random state or independent Update loop.
    public sealed class BeamCharge
    {
        readonly Transform root,volume;
        readonly Material volumeMaterial;
        readonly LineRenderer[] trails=new LineRenderer[24];
        public bool Visible=>root.gameObject.activeSelf;
        public float Power {get;private set;}
        public Vector3 Center=>volume.position;
        public float Age {get;private set;}
        public BeamCharge(Transform parent)
        {
            root=new GameObject("Hero converging energy").transform;root.SetParent(parent,false);
            var noise=RuntimeResources.Own(root,VolcanoStage.CreatePlumeNoise());
            volumeMaterial=RuntimeResources.Own(root,new Material(Resources.Load<Shader>("BeamChargeVolume")));
            volumeMaterial.SetTexture("_Noise",noise);
            volume=GameWorld.Primitive("Gathered forearm energy",PrimitiveType.Cube,root,Vector3.zero,Vector3.one,volumeMaterial);
            var surface=volume.GetComponent<Renderer>();surface.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;surface.receiveShadows=false;
            var material=RuntimeResources.Own(root,new Material(Resources.Load<Shader>("ChargeFilament")));
            for(int i=0;i<trails.Length;i++)
            {
                var line=new GameObject("Inward energy filament").AddComponent<LineRenderer>();line.transform.SetParent(root,false);
                line.sharedMaterial=material;line.positionCount=12;line.numCapVertices=2;
                line.widthCurve=new AnimationCurve(new Keyframe(0,0),new Keyframe(.3f,.3f),new Keyframe(.85f,1),new Keyframe(1,.35f));
                line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;
                trails[i]=line;
            }
            Clear();
        }
        public void Clear(){root.gameObject.SetActive(false);Power=Age=0;}
        public void Sample(bool active,float age,Vector3 origin,Vector3 leftHand,Vector3 rightHand,Vector3 axis)
        {
            if(!active){Clear();return;}
            Age=Mathf.Max(0,age);
            float progress=Mathf.Clamp01(Age/BeamCloseup.Duration);
            float appear=Mathf.SmoothStep(0,1,Age/.18f);
            float release=1-Mathf.SmoothStep(0,1,(Age-BeamCloseup.Duration-BeamStream.LaunchSeconds+.025f)/.125f);
            Power=appear*release*Mathf.Lerp(.18f,1,Mathf.SmoothStep(0,1,progress));
            root.gameObject.SetActive(Power>.001f);
            if(!Visible)return;
            // The cloud starts between the hands, then contracts into the same
            // forearm origin used by the outgoing beam. It stays lit through
            // the short return-to-battle shot before the beam launches.
            var hands=(leftHand+rightHand)*.5f;
            // The vertical-hand pose puts the hand midpoint beside the face.
            // Gather below it at the upper chest, keeping the eyes readable.
            var gathered=Vector3.Lerp(hands,origin,.15f)-Vector3.up*.28f;
            volume.position=Vector3.Lerp(gathered,origin,Mathf.SmoothStep(0,1,(Age-BeamCloseup.Duration+.20f)/.20f));
            volume.rotation=Quaternion.LookRotation(axis,Vector3.up);
            float gathering=Mathf.Sin(Mathf.Clamp01(progress/.8f)*Mathf.PI*.5f);
            float collapse=Mathf.SmoothStep(0,1,(progress-.66f)/.34f);
            float diameter=Mathf.Lerp(.65f+gathering*.48f,.64f,collapse);
            volume.localScale=new Vector3(diameter,diameter*.8f,diameter);
            volumeMaterial.SetFloat("_Age",Age);volumeMaterial.SetFloat("_Power",Power);
            var side=Vector3.Cross(Vector3.up,axis).normalized;
            float streamFade=appear*release*(1-Mathf.SmoothStep(0,1,(progress-.77f)/.23f));
            for(int i=0;i<trails.Length;i++)
            {
                var line=trails[i];line.enabled=streamFade>.001f;
                if(!line.enabled)continue;
                float seed=i*2.399963f;
                float tip=Mathf.Repeat(Age*(1.05f+i%4*.14f)+i/(float)trails.Length,1);
                float tail=Mathf.Max(0,tip-.19f);
                for(int j=0;j<line.positionCount;j++)
                {
                    float t=Mathf.Lerp(tail,tip,j/(float)(line.positionCount-1));
                    float radius=(1-t)*(1-t)*(1.05f+i%5*.10f);
                    float angle=seed+(1-t)*2.6f;
                    var radial=side*Mathf.Cos(angle)+Vector3.up*(Mathf.Sin(angle)*.48f-.1f);
                    line.SetPosition(j,volume.position+radial*radius+axis*(Mathf.Sin(seed*1.7f)*radius*.40f));
                }
                float life=Mathf.Sin(tip*Mathf.PI);
                line.widthMultiplier=(.018f+i%3*.007f)*(.65f+progress*.6f);
                line.startColor=new Color(.07f,.42f,1,0);
                line.endColor=new Color(.60f,.87f,1.2f,streamFade*life*.88f);
            }
        }
    }
}
