using UnityEngine;
using UnityEngine.Rendering;

namespace UltramanGame.Runtime
{
    // Fixed attack-clock emissions. Each puff leaves the moving stone once;
    // subsequent target/camera motion cannot drag the old trail along with it.
    public sealed class BoulderDust
    {
        const int TrailCount=12,Count=18;
        const float Interval=.045f;
        readonly Transform[] roots=new Transform[Count];
        readonly Renderer[] renderers=new Renderer[Count];
        readonly Vector3[] positions=new Vector3[Count],velocities=new Vector3[Count];
        readonly float[] births=new float[Count];
        readonly bool[] alive=new bool[Count];
        readonly MaterialPropertyBlock properties=new MaterialPropertyBlock();
        int sequence=-1,next;
        float previousAge;
        Vector3 previous;
        bool shattered;
        public int Visible {get;private set;}
        public BoulderDust(Transform parent)
        {
            var material=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("ImpactCloud")));
            for(int i=0;i<Count;i++)
            {
                roots[i]=GameWorld.Primitive("Boulder dust "+i,PrimitiveType.Quad,parent,Vector3.zero,Vector3.one,material);
                renderers[i]=roots[i].GetComponent<Renderer>();renderers[i].shadowCastingMode=ShadowCastingMode.Off;renderers[i].receiveShadows=false;
            }
            Clear();
        }
        public void Clear()
        {sequence=-1;next=0;shattered=false;Visible=0;for(int i=0;i<Count;i++){alive[i]=false;roots[i].gameObject.SetActive(false);}}
        public void Tick(int attack,float age,Vector3 stone,bool impact,Vector3 contact,Camera camera)
        {
            if(sequence!=attack)
            {
                Clear();sequence=attack;previousAge=age;previous=stone;shattered=impact;
                // Do not reconstruct historical particles after pause/reset.
                next=Mathf.Max(0,Mathf.CeilToInt((age-Core.MonsterRockMotion.Launch)/Interval)-1);
            }
            Vector3 travel=stone-previous;var direction=travel.sqrMagnitude>.000001f?travel.normalized:Vector3.up;
            while(next<TrailCount)
            {
                float birth=Core.MonsterRockMotion.Launch+next*Interval;
                if(birth>age)break;
                if(birth>=previousAge-.07f)
                {
                    positions[next]=Vector3.Lerp(previous,stone,Mathf.InverseLerp(previousAge,age,birth));
                    float angle=next*2.39996f;
                    velocities[next]=-direction*.28f+new Vector3(Mathf.Sin(angle)*.16f,-.13f,Mathf.Cos(angle)*.16f);
                    births[next]=birth;alive[next]=true;
                }
                next++;
            }
            if(impact&&!shattered)
            {
                shattered=true;
                for(int i=TrailCount;i<Count;i++)
                {
                    float angle=(i-TrailCount)*Mathf.PI*2/(Count-TrailCount);
                    positions[i]=contact;births[i]=Core.MonsterRockMotion.Contact;alive[i]=true;
                    // Spread across the contact plane, with a little forward
                    // depth so the burst does not read as one flat stamp.
                    velocities[i]=camera.transform.right*Mathf.Cos(angle)*.92f+camera.transform.up*Mathf.Sin(angle)*.75f+camera.transform.forward*((i%2==0?1:-1)*.18f);
                }
            }
            Visible=0;
            for(int i=0;i<Count;i++)
            {
                bool burst=i>=TrailCount;float life=burst?.65f:.52f,t=age-births[i];
                bool show=alive[i]&&t>=0&&t<life;roots[i].gameObject.SetActive(show);if(!show)continue;
                Visible++;float progress=t/life;
                roots[i].position=positions[i]+velocities[i]*t+Vector3.down*(t*t*.22f);
                roots[i].rotation=camera.transform.rotation*Quaternion.Euler(0,0,i*47+t*(i%2==0?22:-18));
                float size=(burst?.48f:.20f)+(burst?.65f:.46f)*Mathf.Sqrt(progress);
                roots[i].localScale=new Vector3(size,size*(.82f+i%3*.12f),size);
                properties.SetColor("_Color",new Color(.61f,.56f,.48f,(burst?.64f:.52f)*Mathf.SmoothStep(0,1,t/.055f)*(1-progress)));
                properties.SetFloat("_Seed",i*13.17f+.8f);properties.SetFloat("_Age",.08f+progress*.4f);properties.SetFloat("_Hot",0);
                renderers[i].SetPropertyBlock(properties);
            }
            previousAge=age;previous=stone;
        }
    }
}
