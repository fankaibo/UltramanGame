using UnityEngine;

namespace UltramanGame.Runtime
{
    // The landing owns one pooled, depth-tested burst. Explicit time keeps
    // its light, dust and replay lifetime independent of particle simulation.
    public sealed class DefeatImpact
    {
        public const float Lifetime=2.4f;
        readonly Transform billows,ground;
        readonly Material billowMaterial,groundMaterial;
        readonly Light light;
        Vector3 contact;
        public float Age {get;private set;}=Lifetime;
        public int Starts {get;private set;}
        public bool Visible=>billows.gameObject.activeSelf||ground.gameObject.activeSelf;
        public float LightIntensity=>light.intensity;
        public DefeatImpact(Transform parent)
        {
            var noise=RuntimeResources.Own(parent,VolcanoStage.CreatePlumeNoise());
            var shader=Resources.Load<Shader>("DefeatImpact");
            billowMaterial=RuntimeResources.Own(parent,new Material(shader));
            groundMaterial=RuntimeResources.Own(parent,new Material(shader));
            billowMaterial.SetTexture("_Noise",noise);groundMaterial.SetTexture("_Noise",noise);groundMaterial.SetFloat("_Ground",1);
            billows=Volume(parent,"Defeat rising dust",billowMaterial);
            ground=Volume(parent,"Defeat rolling dust",groundMaterial);
            light=new GameObject("Defeat contact light").AddComponent<Light>();
            light.transform.SetParent(parent,false);light.type=LightType.Point;light.color=new Color(1,.57f,.22f);
            light.range=6;light.shadows=UnityEngine.LightShadows.None;
            Clear();
        }
        static Transform Volume(Transform parent,string name,Material material)
        {
            var node=GameWorld.Primitive(name,PrimitiveType.Cube,parent,Vector3.zero,Vector3.one,material);
            var renderer=node.GetComponent<Renderer>();renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            return node;
        }
        public void Begin(Vector3 position,bool trace=true)
        {
            contact=new Vector3(position.x,0,position.z);Age=0;Starts++;Sample();
            if(trace&&Debug.isDebugBuild)Debug.Log("[DefeatImpact] begin landing=True");
        }
        public void Tick(float dt)
        {
            if(Age>=Lifetime)return;
            Age=Mathf.Min(Lifetime,Age+Mathf.Max(0,dt));Sample();
        }
        void Sample()
        {
            bool live=Age<Lifetime;billows.gameObject.SetActive(live);ground.gameObject.SetActive(live);
            light.intensity=live?4*Mathf.Exp(-Age*9):0;light.enabled=live&&light.intensity>.01f;
            if(!live)return;
            float expansion=1-Mathf.Exp(-Age*5);
            var size=Vector3.Lerp(new Vector3(.7f,.65f,.7f),new Vector3(3.8f,2.8f,3.1f),expansion);
            billows.position=contact+Vector3.up*(size.y*.36f+Age*.10f);billows.localScale=size;
            float radius=.55f+2.25f*(1-Mathf.Exp(-Age*3.5f));
            ground.position=contact+Vector3.up*.32f;ground.localScale=new Vector3(radius*2,.68f,radius*2);
            light.transform.position=contact+Vector3.up*.75f;
            billowMaterial.SetFloat("_Age",Age);groundMaterial.SetFloat("_Age",Age);
        }
        public void Clear()
        {Age=Lifetime;Starts=0;billows.gameObject.SetActive(false);ground.gameObject.SetActive(false);light.intensity=0;light.enabled=false;}
    }
}
