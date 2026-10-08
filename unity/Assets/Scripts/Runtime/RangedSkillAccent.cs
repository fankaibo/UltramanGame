using UnityEngine;
using UnityEngine.Rendering;

namespace UltramanGame.Runtime
{
    // Fixed, depth-tested volumes. No world Update, particle simulation or hit logic.
    public sealed class RangedSkillAccent
    {
        readonly Transform muzzle,impact;
        readonly Material muzzleMaterial,impactMaterial;
        readonly LineRenderer[] muzzleArcs=new LineRenderer[2],rays=new LineRenderer[10];
        readonly LineRenderer ring;
        readonly Light spill;
        Vector3 position,axis,side,up;
        Color tint;
        float hitAge=10;
        public bool LaunchVisible=>muzzle.gameObject.activeSelf;
        public bool ImpactVisible=>impact.gameObject.activeSelf;
        public int Impacts {get;private set;}
        public Vector3 ImpactPosition=>position;
        public RangedSkillAccent(Transform parent)
        {
            muzzle=Volume(parent,"Ranged launch volume",out muzzleMaterial);
            impact=Volume(parent,"Directional ranged impact",out impactMaterial);
            var material=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("ChargeFilament")));
            for(int i=0;i<muzzleArcs.Length;i++)muzzleArcs[i]=Line(parent,material,"Launch curl "+i,20,.04f);
            for(int i=0;i<rays.Length;i++)rays[i]=Line(parent,material,"Directional energy fragment "+i,8,.035f);
            ring=Line(parent,material,"Ranged contact wave",49,.055f);
            spill=new GameObject("Ranged contact spill").AddComponent<Light>();spill.transform.SetParent(parent,false);spill.type=LightType.Point;spill.range=1.8f;spill.shadows=LightShadows.None;
            Clear();
        }
        public static Transform Volume(Transform parent,string name,out Material material)
        {
            material=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("SkillEnergy")));
            var result=GameWorld.Primitive(name,PrimitiveType.Sphere,parent,Vector3.zero,Vector3.one,material);
            result.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;result.GetComponent<Renderer>().receiveShadows=false;return result;
        }
        static LineRenderer Line(Transform parent,Material material,string name,int count,float width)
        {
            var line=new GameObject(name).AddComponent<LineRenderer>();line.transform.SetParent(parent,false);line.sharedMaterial=material;line.positionCount=count;line.widthMultiplier=width;
            line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.enabled=false;return line;
        }
        static void Tint(LineRenderer line,Color color,float alpha)
        {line.startColor=new Color(color.r,color.g,color.b,0);line.endColor=new Color(color.r,color.g,color.b,alpha);}
        public void Clear()
        {muzzle.gameObject.SetActive(false);impact.gameObject.SetActive(false);foreach(var line in muzzleArcs)line.enabled=false;foreach(var line in rays)line.enabled=false;ring.enabled=false;spill.intensity=0;hitAge=10;}
        public void NewRound(){Clear();Impacts=0;}
        public void Launch(bool active,float age,Vector3 origin,Vector3 direction,Color color)
        {
            float appear=Mathf.SmoothStep(0,1,age/.065f),fade=1-Mathf.SmoothStep(0,1,(age-.12f)/.12f);
            float power=active?appear*fade:0;muzzle.gameObject.SetActive(power>.005f);
            foreach(var line in muzzleArcs)line.enabled=power>.005f;
            if(power<=.005f)return;
            Vector3 lateral=Vector3.Cross(Vector3.up,direction).normalized,vertical=Vector3.Cross(direction,lateral).normalized;
            muzzle.position=origin;muzzle.rotation=Quaternion.LookRotation(direction);muzzle.localScale=new Vector3(.42f,.42f,.65f)*(.4f+.6f*power);
            muzzleMaterial.color=new Color(color.r,color.g,color.b,power*.65f);muzzleMaterial.SetFloat("_Age",age);
            for(int i=0;i<2;i++)
            {
                var line=muzzleArcs[i];Tint(line,Color.Lerp(color,Color.white,.35f),power*.85f);
                for(int j=0;j<line.positionCount;j++)
                {float t=j/(float)(line.positionCount-1),a=t*Mathf.PI*1.5f+i*Mathf.PI+age*18;float r=.18f+.26f*(1-t);line.SetPosition(j,origin+(lateral*Mathf.Cos(a)+vertical*Mathf.Sin(a))*r-direction*(1-t)*.2f);}
            }
        }
        public void Hit(Vector3 point,Vector3 incoming,Color color)
        {position=point;axis=incoming.normalized;side=Vector3.Cross(Vector3.up,axis).normalized;up=Vector3.Cross(axis,side).normalized;tint=color;hitAge=0;Impacts++;}
        public void TickImpact(float dt)
        {
            hitAge+=Mathf.Max(0,dt);float t=Mathf.Clamp01(hitAge/.30f),fade=1-t;
            bool visible=hitAge<.30f;impact.gameObject.SetActive(visible);ring.enabled=visible;foreach(var line in rays)line.enabled=visible;
            spill.intensity=visible?fade*fade*1.8f:0;if(!visible)return;
            // Face the actual incoming path, then blow fragments back out of the skin.
            impact.position=position-axis*.10f;impact.rotation=Quaternion.LookRotation(axis);
            float size=.28f+.95f*Mathf.Sqrt(t);impact.localScale=new Vector3(size,size,.22f+.16f*t);
            impactMaterial.color=new Color(tint.r,tint.g,tint.b,fade*fade*.75f);impactMaterial.SetFloat("_Age",hitAge);
            spill.transform.position=position-axis*.28f;spill.color=tint;
            float radius=.12f+t*.95f;Tint(ring,Color.Lerp(tint,Color.white,.35f),fade*fade*.75f);
            for(int j=0;j<ring.positionCount;j++)
            {float angle=j/(float)(ring.positionCount-1)*Mathf.PI*2;ring.SetPosition(j,position-axis*.13f+(side*Mathf.Cos(angle)+up*Mathf.Sin(angle))*radius);}
            for(int i=0;i<rays.Length;i++)
            {
                float angle=i*2.399963f;var radial=side*Mathf.Cos(angle)+up*Mathf.Sin(angle);
                var line=rays[i];Tint(line,Color.Lerp(tint,Color.white,.5f),fade*.8f);line.widthMultiplier=.025f+fade*.03f;
                for(int j=0;j<line.positionCount;j++)
                {float u=j/(float)(line.positionCount-1),p=Mathf.Max(0,t-.17f+u*.17f);line.SetPosition(j,position+radial*(.10f+p*(1.1f+i%3*.26f))-axis*(.14f+p*.55f)+up*(-p*p*.18f));}
            }
        }
    }
}
