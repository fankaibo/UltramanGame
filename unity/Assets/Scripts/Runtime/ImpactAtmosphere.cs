using UnityEngine;

namespace UltramanGame.Runtime
{
    // Short-lived, pooled smoke and fragments give hits volume without hiding the next gesture.
    public sealed class ImpactAtmosphere
    {
        sealed class Puff {public Transform Root;public Material Mat;public Vector3 Velocity;public float Age=10,Life,Size,Spin;}
        sealed class Chip {public Transform Root;public Vector3 Velocity,Spin;public float Age=10;}
        readonly Puff[] puffs=new Puff[32];readonly Chip[] chips=new Chip[24];int index,chipIndex;
        public ImpactAtmosphere(Transform parent)
        {
            for(int i=0;i<puffs.Length;i++)
            {
                var mat=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("ImpactCloud")));mat.SetFloat("_Seed",i*7.71f);
                puffs[i]=new Puff{Mat=mat,Root=GameWorld.Primitive("Impact smoke",PrimitiveType.Quad,parent,Vector3.zero,Vector3.one,mat)};
                puffs[i].Root.gameObject.SetActive(false);
            }
            var stone=RuntimeResources.Own(parent,new Material(Resources.Load<Material>("PrototypeSurface")){color=new Color(.14f,.18f,.23f)});
            for(int i=0;i<chips.Length;i++)
            {chips[i]=new Chip{Root=GameWorld.Primitive("Small impact fragment",PrimitiveType.Cube,parent,Vector3.zero,new Vector3(.032f,.023f,.042f),stone)};chips[i].Root.gameObject.SetActive(false);}
        }
        public void Hit(Vector3 position,bool special,bool blocked)
        {
            if(blocked)return;
            int count=special?9:4;
            for(int i=0;i<count;i++)
            {
                var p=puffs[index++%puffs.Length];p.Age=0;p.Life=special?1.25f:.65f;p.Size=special?.65f:.3f;
                p.Velocity=Random.onUnitSphere*(special?.8f:.4f)+Vector3.up*.3f;
                p.Root.position=position+Random.insideUnitSphere*.16f;p.Root.gameObject.SetActive(true);p.Spin=Random.Range(-70,70);
                p.Mat.color=new Color(.48f,.55f,.66f,special?.66f:.4f);p.Mat.SetFloat("_Hot",special?1:.65f);
            }
            for(int i=0;i<(special?10:3);i++)
            {
                var c=chips[chipIndex++%chips.Length];c.Age=0;c.Root.position=new Vector3(position.x,.1f,position.z);
                c.Velocity=Random.onUnitSphere*(special?1.4f:.65f)+Vector3.up*1.3f;c.Spin=Random.onUnitSphere*300;c.Root.gameObject.SetActive(true);
            }
        }
        public void Tick(Camera camera,float dt)
        {
            foreach(var p in puffs)
            {
                p.Age+=dt;if(p.Age>=p.Life){p.Root.gameObject.SetActive(false);continue;}
                float t=p.Age/p.Life;p.Root.position+=p.Velocity*dt;p.Root.rotation=camera.transform.rotation*Quaternion.Euler(0,0,p.Spin*t);
                p.Root.localScale=Vector3.one*p.Size*Mathf.Lerp(.4f,2.8f,t);p.Mat.SetFloat("_Age",t);
            }
            foreach(var c in chips)
            {
                c.Age+=dt;if(c.Age>1.1f){c.Root.gameObject.SetActive(false);continue;}
                c.Velocity+=Vector3.down*3.8f*dt;c.Root.position+=c.Velocity*dt;c.Root.Rotate(c.Spin*dt);
                if(c.Root.position.y<.03f){var p=c.Root.position;p.y=.03f;c.Root.position=p;c.Velocity.y=Mathf.Abs(c.Velocity.y)*.28f;c.Velocity*=.7f;}
            }
        }
        public void Clear(){foreach(var p in puffs){p.Age=10;p.Root.gameObject.SetActive(false);}foreach(var c in chips){c.Age=10;c.Root.gameObject.SetActive(false);}}
    }
}
