using UnityEngine;
using UnityEngine.Rendering;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    // Samples the shared enemy clock; visual travel reaches the shield on the
    // existing rule contact. No collider, secondary damage or delayed callback.
    public sealed class MonsterRay
    {
        readonly Transform glow,contact;
        readonly Material glowMaterial,rayMaterial,contactMaterial;
        readonly LineRenderer ray;
        readonly Light spill;
        Battle observed;
        int launchedAttack=-1;
        Vector3 hitPoint;
        bool hit,blocked;
        public bool Started {get;private set;}
        public bool Visible=>ray.enabled;
        public float Power {get;private set;}
        public Vector3 Origin {get;private set;}
        public Vector3 Tip {get;private set;}
        public int Launches {get;private set;}
        public MonsterRay(Transform parent)
        {
            glowMaterial=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("BeamChargeVolume")));
            glowMaterial.SetTexture("_Noise",RuntimeResources.Own(parent,VolcanoStage.CreatePlumeNoise()));
            glowMaterial.SetColor("_Tint",new Color(2.1f,.33f,1.10f));
            glow=GameWorld.Primitive("Golza forehead charge",PrimitiveType.Cube,parent,Vector3.zero,Vector3.one,glowMaterial);
            var renderer=glow.GetComponent<Renderer>();renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            rayMaterial=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("MonsterRay")));
            ray=new GameObject("Golza ultrasonic ray").AddComponent<LineRenderer>();ray.transform.SetParent(parent,false);
            ray.sharedMaterial=rayMaterial;ray.positionCount=2;ray.widthMultiplier=.48f;
            ray.widthCurve=new AnimationCurve(new Keyframe(0,.65f),new Keyframe(1,1));
            ray.shadowCastingMode=ShadowCastingMode.Off;ray.receiveShadows=false;
            contactMaterial=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("EnergyFlare")));
            contact=GameWorld.Primitive("Golza ray shield contact",PrimitiveType.Quad,parent,Vector3.zero,Vector3.one,contactMaterial);
            spill=new GameObject("Golza ray spill").AddComponent<Light>();spill.transform.SetParent(parent,false);
            spill.type=LightType.Point;spill.range=2.5f;spill.shadows=LightShadows.None;spill.color=new Color(.85f,.20f,1);
            Clear();
        }
        public void Clear(){Started=false;Power=0;ray.enabled=false;glow.gameObject.SetActive(false);contact.gameObject.SetActive(false);spill.intensity=0;hit=false;}
        public void Impact(Vector3 point,bool wasBlocked){hitPoint=point;hit=true;blocked=wasBlocked;}
        public void Tick(Battle state,Camera camera,Vector3 origin,Vector3 target,bool suppressed)
        {
            Started=false;
            if(!ReferenceEquals(observed,state)){observed=state;launchedAttack=-1;Launches=0;hit=false;}
            if(!MonsterRayMotion.Active(state)||suppressed){Clear();return;}
            bool attack=state.Enemy==EnemyPhase.Attack;
            if(state.Enemy==EnemyPhase.Windup)hit=false;
            Origin=origin;
            Power=attack?MonsterRayMotion.Power(state.EnemyAge):0;
            float gather=state.Enemy==EnemyPhase.Windup?MonsterRayMotion.Prepare(state):Power;
            glow.gameObject.SetActive(gather>.001f);glow.position=origin;
            glow.localScale=Vector3.one*Mathf.Lerp(.30f,.72f,gather);
            glowMaterial.SetFloat("_Power",gather);glowMaterial.SetFloat("_Age",state.EnemyAge);
            spill.transform.position=origin;spill.intensity=gather*.85f;
            ray.enabled=Power>.001f;
            contact.gameObject.SetActive(ray.enabled);
            if(!ray.enabled)return;
            if(launchedAttack!=state.EnemyAttackCount)
            {launchedAttack=state.EnemyAttackCount;Launches++;Started=true;if(Debug.isDebugBuild)Debug.Log($"[MonsterRay] launch attack={launchedAttack} age={state.EnemyAge:F3}");}
            Tip=Vector3.Lerp(origin,hit?hitPoint:target,MonsterRayMotion.Travel(state.EnemyAge));
            ray.SetPosition(0,origin);ray.SetPosition(1,Tip);ray.widthMultiplier=.48f*Mathf.Lerp(.6f,1,Power);
            rayMaterial.SetFloat("_Age",state.EnemyAge);rayMaterial.SetFloat("_Power",Power);rayMaterial.SetFloat("_Length",Vector3.Distance(origin,Tip));
            contact.position=Tip-camera.transform.forward*.02f;contact.rotation=camera.transform.rotation;
            contact.localScale=Vector3.one*(hit?.70f:.28f);
            contactMaterial.color=hit&&blocked?new Color(.2f,.68f,1,Power*.65f):new Color(.85f,.2f,1,Power*.58f);
        }
    }
}
