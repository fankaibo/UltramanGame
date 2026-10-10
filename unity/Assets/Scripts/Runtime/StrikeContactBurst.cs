using UnityEngine;

namespace UltramanGame.Runtime
{
    // A short, directional contact flash. Fixed pool; no allocations per hit.
    public sealed class StrikeContactBurst
    {
        sealed class Contact {public Transform Root;public Material Material;public Vector3 Direction;public float Age=10,Life,Size;}
        readonly Contact[] contacts=new Contact[6];int index;
        public int ActiveCount {get;private set;}
        public StrikeContactBurst(Transform parent)
        {
            for(int i=0;i<contacts.Length;i++)
            {
                var mat=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("StrikeContact")));
                var quad=GameWorld.Primitive("Directional punch contact",PrimitiveType.Quad,parent,Vector3.zero,Vector3.one,mat);
                quad.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                quad.gameObject.SetActive(false);contacts[i]=new Contact{Root=quad,Material=mat};
            }
        }
        public void Hit(Vector3 position,Vector3 direction,bool combo)
        {
            var c=contacts[index++%contacts.Length];c.Age=0;c.Life=combo?.29f:.23f;c.Size=combo?1.95f:1.50f;c.Direction=direction;
            c.Root.position=position;c.Material.SetFloat("_Seed",index*.618f);c.Material.SetFloat("_Power",combo?1:.85f);
            c.Root.gameObject.SetActive(true);
        }
        public void Tick(Camera camera,float dt)
        {
            ActiveCount=0;
            foreach(var c in contacts)
            {
                c.Age+=dt;if(c.Age>=c.Life){c.Root.gameObject.SetActive(false);continue;}
                float age=c.Age/c.Life;
                float angle=Mathf.Atan2(Vector3.Dot(c.Direction,camera.transform.up),Vector3.Dot(c.Direction,camera.transform.right))*Mathf.Rad2Deg;
                c.Root.rotation=camera.transform.rotation*Quaternion.Euler(0,0,angle);
                c.Root.localScale=Vector3.one*c.Size*(.55f+.60f*Mathf.Sqrt(age));
                c.Material.SetFloat("_Age",age);ActiveCount++;
            }
        }
        public void Clear(){foreach(var c in contacts){c.Age=10;c.Root.gameObject.SetActive(false);}ActiveCount=0;}
    }
}
