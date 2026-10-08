using UnityEngine;
using UnityEngine.Rendering;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    // One accepted strike owns one flight. Damage stays in Battle, with the
    // same tempo clock used here; cancellation/pause cannot leave ghost hits.
    public sealed class HeroProjectile
    {
        readonly Transform core,halo,wave;
        readonly Material coreMat,haloMat,waveMat,trailMat;
        readonly LineRenderer trail;
        Battle observed;
        int sequence=-1;
        Color tint=new Color(.18f,.7f,1);
        Vector3 origin;
        public bool Visible {get;private set;}
        public bool Started {get;private set;}
        public int Launches {get;private set;}
        public Vector3 Origin=>origin;
        public Vector3 Tip {get;private set;}
        public HeroProjectile(Transform parent)
        {
            core=Quad(parent,"Light bullet core",out coreMat);
            halo=Quad(parent,"Light bullet aura",out haloMat);
            wave=Quad(parent,"Light bullet travelling wave",out waveMat);waveMat.SetFloat("_Ring",1);
            trailMat=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("StrikeRibbon")));
            trail=new GameObject("Light bullet short trail").AddComponent<LineRenderer>();trail.transform.SetParent(parent,false);
            trail.sharedMaterial=trailMat;trail.positionCount=8;trail.widthMultiplier=.30f;
            trail.widthCurve=new AnimationCurve(new Keyframe(0,0),new Keyframe(.8f,1),new Keyframe(1,.35f));
            trail.shadowCastingMode=ShadowCastingMode.Off;trail.receiveShadows=false;Clear();
        }
        static Transform Quad(Transform parent,string name,out Material material)
        {
            material=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("EnergyFlare")));
            var q=GameWorld.Primitive(name,PrimitiveType.Quad,parent,Vector3.zero,Vector3.one,material);
            q.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;q.GetComponent<Renderer>().receiveShadows=false;return q;
        }
        public void SetHero(string id)
        {if(ColorUtility.TryParseHtmlString(HeroRoster.At(HeroRoster.Index(id)).BeamTint,out var color))tint=color;}
        public void Clear(){Visible=Started=false;core.gameObject.SetActive(false);halo.gameObject.SetActive(false);wave.gameObject.SetActive(false);trail.enabled=false;}
        public void Tick(Battle state,Camera camera,Vector3 hand,Vector3 target,bool suppressed)
        {
            Started=false;
            if(!ReferenceEquals(observed,state)){observed=state;sequence=-1;Launches=0;Clear();}
            if(suppressed||state.Phase!=GamePhase.Battle||!state.Shot.Active)
            {Clear();return;}
            if(sequence!=state.Shot.Sequence)
            {sequence=state.Shot.Sequence;origin=hand;Launches++;Started=true;}
            float age=state.Shot.Age;
            Visible=age<=AttackTempo.RangedHitSeconds+.055f;
            core.gameObject.SetActive(Visible);halo.gameObject.SetActive(Visible);wave.gameObject.SetActive(Visible);trail.enabled=Visible;
            if(!Visible)return;
            float t=AttackTempo.Travel(age),fade=1-Mathf.Clamp01((age-AttackTempo.RangedHitSeconds)/.055f);
            Tip=Vector3.Lerp(origin,target,t);
            core.position=halo.position=wave.position=Tip;
            core.rotation=halo.rotation=camera.transform.rotation;
            wave.rotation=camera.transform.rotation*Quaternion.Euler(0,25,age*320);
            core.localScale=Vector3.one*.38f;halo.localScale=Vector3.one*.85f;
            wave.localScale=new Vector3(.72f,.50f,1)*(1+.12f*Mathf.Sin(t*Mathf.PI));
            coreMat.color=new Color(1,.97f,.85f,fade);
            haloMat.color=new Color(tint.r,tint.g,tint.b,.72f*fade);
            waveMat.color=new Color(tint.r,tint.g,tint.b,.58f*fade);
            trailMat.color=new Color(tint.r,tint.g,tint.b,.65f*fade);
            for(int i=0;i<8;i++)trail.SetPosition(i,Vector3.Lerp(origin,target,Mathf.Max(0,t-.28f+i/7f*.28f)));
        }
    }
}
