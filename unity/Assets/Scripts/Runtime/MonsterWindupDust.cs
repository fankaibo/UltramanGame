using UnityEngine;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    // Two planted-foot pressure beats, sampled on the warning clock. Dust
    // stays where the foot loaded the ground; no independent particle clock.
    public sealed class MonsterWindupDust
    {
        sealed class Cloud {public Transform Root;public Material Material;public Vector3 Origin;public bool Captured;}
        readonly Cloud[] clouds=new Cloud[12];
        Battle observed;int attack=-1;float lastAge=-1;
        public int ActiveCount {get;private set;}
        public MonsterWindupDust(Transform parent)
        {
            for(int i=0;i<clouds.Length;i++)
            {
                var material=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("GroundDust")));
                material.SetFloat("_Seed",i*4.17f+1.3f);material.SetColor("_Color",new Color(.48f,.43f,.37f,.72f));
                var quad=GameWorld.Primitive("Monster windup ground dust",PrimitiveType.Quad,parent,Vector3.zero,Vector3.one,material);
                quad.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                quad.GetComponent<Renderer>().receiveShadows=false;quad.gameObject.SetActive(false);
                clouds[i]=new Cloud{Root=quad,Material=material};
            }
        }
        public void Clear()
        {foreach(var c in clouds){c.Captured=false;c.Root.gameObject.SetActive(false);}ActiveCount=0;lastAge=-1;}
        public void Tick(Battle state,Camera camera,Vector3 left,Vector3 right,Vector3 forward,bool suppressed=false)
        {
            if(!ReferenceEquals(observed,state)||attack!=state.EnemyAttackCount||state.EnemyAge<lastAge)
            {Clear();observed=state;attack=state.EnemyAttackCount;}
            bool active=!suppressed&&state.Phase==GamePhase.Battle&&state.Enemy==EnemyPhase.Windup&&state.Action!=HeroAction.Beam;
            if(!active){Clear();return;}
            lastAge=state.EnemyAge;ActiveCount=0;
            var side=Vector3.Cross(Vector3.up,forward).normalized;
            for(int i=0;i<clouds.Length;i++)
            {
                var c=clouds[i];int lane=i%3;bool late=i>=6,isLeft=(i/3)%2==0;
                float start=(late?Mathf.Max(.8f,state.WarningDuration-.96f):.22f)+lane*.10f;
                float life=Mathf.Min(1.16f,state.WarningDuration-start);
                float age=state.EnemyAge-start;
                if(age<0||age>=life||life<=0){c.Root.gameObject.SetActive(false);continue;}
                if(!c.Captured){c.Origin=isLeft?left:right;c.Origin.y=.025f;c.Captured=true;}
                float t=age/life,swell=Mathf.Sqrt(t),sign=isLeft?-1:1;
                Vector3 drift=side*(sign*(.10f+lane*.07f+t*.22f))-forward*(.05f+t*.13f);
                c.Root.position=c.Origin+drift+Vector3.up*(.08f+.055f*Mathf.Sin(t*Mathf.PI));
                c.Root.rotation=camera.transform.rotation;
                float size=.38f+swell*(late?.76f:.60f);
                c.Root.localScale=new Vector3(size,size*.46f,1);
                c.Material.SetFloat("_Age",t);c.Root.gameObject.SetActive(true);ActiveCount++;
            }
        }
    }
}
