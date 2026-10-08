using UnityEngine;
using UnityEngine.Rendering;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    // Mebium Blade grows from the left wrist, rather than giving every hero a sword.
    public sealed class HeroBlade
    {
        readonly LineRenderer core,halo;
        string heroId="Tiga";
        public bool Visible=>core.enabled;
        public Vector3 Tip {get;private set;}
        public HeroBlade(Transform parent)
        {
            core=Line(parent,"Mebium blade white core",new Color(1,.95f,.60f),.10f);
            halo=Line(parent,"Mebium blade flame edge",new Color(1,.36f,.05f,.65f),.27f);Clear();
        }
        static LineRenderer Line(Transform parent,string name,Color color,float width)
        {
            var line=new GameObject(name).AddComponent<LineRenderer>();line.transform.SetParent(parent,false);
            line.sharedMaterial=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("StrikeRibbon")){color=color});
            line.positionCount=3;line.widthMultiplier=width;line.widthCurve=new AnimationCurve(new Keyframe(0,.5f),new Keyframe(.20f,1),new Keyframe(1,0));
            line.shadowCastingMode=ShadowCastingMode.Off;return line;
        }
        public void SetHero(string id){heroId=id;Clear();}
        public void Clear(){core.enabled=halo.enabled=false;}
        public void Tick(Battle state,Vector3 wrist,Vector3 forward,Vector3 target,bool suppressed)
        {
            if(suppressed||!HeroArsenal.Blade(heroId,state)){Clear();return;}
            float age=state.ActionAge;
            float grow=MonsterRockMotion.Smooth(age/.045f)*(1-MonsterRockMotion.Smooth((age-.29f)/.09f));
            core.enabled=halo.enabled=grow>.02f;
            float angle=age<Battle.PunchHitSeconds?Mathf.Lerp(-65,0,age/Battle.PunchHitSeconds):Mathf.Lerp(0,70,(age-Battle.PunchHitSeconds)/.26f);
            Vector3 direction=Quaternion.AngleAxis(angle,forward)*Vector3.up;
            float contact=1-Mathf.Clamp01(Mathf.Abs(age-Battle.PunchHitSeconds)/.10f);
            direction=Vector3.Slerp(direction,(target-wrist).normalized,contact);
            Tip=wrist+direction*1.16f*grow;
            SetLine(core,wrist,Tip);SetLine(halo,wrist,Tip);
        }
        static void SetLine(LineRenderer line,Vector3 wrist,Vector3 tip)
        {line.SetPosition(0,wrist);line.SetPosition(1,Vector3.Lerp(wrist,tip,.22f));line.SetPosition(2,tip);}
    }
}
