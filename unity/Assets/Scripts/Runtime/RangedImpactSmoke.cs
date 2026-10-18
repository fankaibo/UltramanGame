using UnityEngine;
using UnityEngine.Rendering;

namespace UltramanGame.Runtime
{
    // Each hit leaves a short, depth-clipped volume at its contact point.
    // Four reusable slots cover overlapping fast casts without per-hit objects.
    public sealed class RangedImpactSmoke
    {
        public const float Lifetime=.72f;
        const int Count=4;
        sealed class Puff
        {
            public Transform Root;public Renderer Renderer;
            public Vector3 Origin,Axis,Side;public Color Tint;
            public float Age=Lifetime,Seed;
        }
        readonly Puff[] puffs=new Puff[Count];
        readonly MaterialPropertyBlock properties=new MaterialPropertyBlock();
        int sequence;
        public int Visible {get;private set;}
        public RangedImpactSmoke(Transform parent)
        {
            var noise=RuntimeResources.Own(parent,VolcanoStage.CreatePlumeNoise());
            var material=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("RangedImpactSmoke")));
            material.SetTexture("_Noise",noise);
            for(int i=0;i<Count;i++)
            {
                var root=GameWorld.Primitive("Ranged cooling smoke "+i,PrimitiveType.Cube,parent,Vector3.zero,Vector3.one,material);
                var renderer=root.GetComponent<Renderer>();renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                puffs[i]=new Puff{Root=root,Renderer=renderer};
            }
            Clear();
        }
        public void Hit(Vector3 point,Vector3 incoming,Color tint)
        {
            var puff=puffs[sequence%Count];puff.Seed=sequence++*.731f;puff.Age=0;puff.Tint=tint;
            puff.Origin=point;puff.Axis=incoming.sqrMagnitude>.0001f?incoming.normalized:Vector3.forward;
            puff.Side=Vector3.Cross(Vector3.up,puff.Axis);
            if(puff.Side.sqrMagnitude<.001f)puff.Side=Vector3.Cross(Vector3.right,puff.Axis);
            puff.Side.Normalize();puff.Root.rotation=Quaternion.LookRotation(puff.Axis,Vector3.Cross(puff.Axis,puff.Side));
            Tick(0);
        }
        public void Tick(float dt)
        {
            Visible=0;dt=Mathf.Max(0,dt);
            foreach(var puff in puffs)
            {
                puff.Age=Mathf.Min(Lifetime,puff.Age+dt);
                bool visible=puff.Age<Lifetime;puff.Root.gameObject.SetActive(visible);if(!visible)continue;Visible++;
                float t=puff.Age/Lifetime,expand=1-Mathf.Exp(-puff.Age*7);
                float size=Mathf.Lerp(.32f,1.36f,expand);
                puff.Root.position=puff.Origin-puff.Axis*(.12f+.34f*expand)+Vector3.up*(.06f+.23f*t)+puff.Side*(Mathf.Sin(puff.Seed)*.07f*t);
                puff.Root.localScale=new Vector3(size,size*.88f,size*.74f);
                properties.SetFloat("_Age",t);properties.SetFloat("_Seed",puff.Seed);
                properties.SetFloat("_Opacity",Mathf.SmoothStep(0,1,puff.Age/.055f)*(1-Mathf.SmoothStep(0,1,(puff.Age-.30f)/.42f)));
                properties.SetColor("_Tint",puff.Tint);puff.Renderer.SetPropertyBlock(properties);
            }
        }
        public void Clear()
        {Visible=sequence=0;foreach(var puff in puffs){puff.Age=Lifetime;puff.Root.gameObject.SetActive(false);}}
    }
}
