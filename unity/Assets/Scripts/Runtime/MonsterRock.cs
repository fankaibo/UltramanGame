using UnityEngine;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    // Pooled scanned stone, ballistic flight and fragments share the attack clock.
    public sealed class MonsterRock
    {
        readonly Transform stone;
        readonly Transform[] pieces=new Transform[7];
        Battle observed;
        Vector3 origin,destination,contact;
        int sequence=-1;
        bool impacted;
        public bool Started {get;private set;}
        public bool Flying {get;private set;}
        public bool Holding {get;private set;}
        public bool Fragments {get;private set;}
        public int Launches {get;private set;}
        public Vector3 Position=>stone.position;
        public MonsterRock(Transform parent)
        {
            var prefab=Resources.Load<GameObject>("Environment/ScannedRocks/rock_07_2k");
            Mesh source=null;
            if(prefab)foreach(var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                if(filter.sharedMesh&&(!source||filter.sharedMesh.vertexCount>source.vertexCount))source=filter.sharedMesh;
            var material=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("ScannedRock")));
            material.mainTexture=Resources.Load<Texture2D>("Environment/ScannedRocks/rock_07_diff_2k");
            material.SetTexture("_Normal",Resources.Load<Texture2D>("Environment/ScannedRocks/rock_07_nor_gl_2k"));
            material.SetTexture("_ARM",Resources.Load<Texture2D>("Environment/ScannedRocks/rock_07_arm_2k"));
            stone=Rock(parent,"Golza thrown boulder",source,material,.70f);
            for(int i=0;i<pieces.Length;i++)pieces[i]=Rock(parent,"Broken boulder "+i,source,material,.10f+i%3*.045f);
            Clear();
        }
        static Transform Rock(Transform parent,string name,Mesh mesh,Material material,float size)
        {
            var root=new GameObject(name).transform;root.SetParent(parent,false);
            if(!mesh)return root;
            var surface=new GameObject("Scanned surface",typeof(MeshFilter),typeof(MeshRenderer)).transform;surface.SetParent(root,false);
            surface.GetComponent<MeshFilter>().sharedMesh=mesh;
            var mapped=new Material[mesh.subMeshCount];for(int i=0;i<mapped.Length;i++)mapped[i]=material;
            surface.GetComponent<MeshRenderer>().sharedMaterials=mapped;
            float scale=size/Mathf.Max(.001f,Mathf.Max(mesh.bounds.size.x,Mathf.Max(mesh.bounds.size.y,mesh.bounds.size.z)));
            surface.localScale=Vector3.one*scale;surface.localPosition=-mesh.bounds.center*scale;return root;
        }
        public void Clear(){Started=Flying=Holding=Fragments=false;stone.gameObject.SetActive(false);foreach(var p in pieces)p.gameObject.SetActive(false);}
        public void Impact(Vector3 position){contact=position;impacted=true;}
        public void Tick(Battle state,Vector3 hand,Vector3 target,bool suppressed)
        {
            Started=false;
            if(!ReferenceEquals(observed,state)){observed=state;sequence=-1;Launches=0;impacted=false;}
            if(suppressed||!MonsterRockMotion.Active(state)){Clear();return;}
            bool attack=state.Enemy==EnemyPhase.Attack;
            float age=state.EnemyAge;
            if(state.Enemy==EnemyPhase.Windup)impacted=false;
            Holding=state.Enemy==EnemyPhase.Windup&&MonsterRockMotion.Prepare(state)>.08f||attack&&age<MonsterRockMotion.Launch;
            Flying=attack&&age>=MonsterRockMotion.Launch&&age<MonsterRockMotion.Contact;
            Fragments=attack&&impacted&&age>=MonsterRockMotion.Contact;
            stone.gameObject.SetActive(Holding||Flying);
            if(Holding){stone.position=hand+Vector3.up*.16f;stone.localScale=Vector3.one*Mathf.SmoothStep(0,1,MonsterRockMotion.Prepare(state)*3);}
            if(attack&&age>=MonsterRockMotion.Launch&&sequence!=state.EnemyAttackCount)
            {sequence=state.EnemyAttackCount;Launches++;Started=true;origin=hand+Vector3.up*.16f;destination=target;stone.localScale=Vector3.one;}
            if(Flying)
            {
                destination=target;
                stone.position=Vector3.Lerp(origin,destination,MonsterRockMotion.Travel(age))+Vector3.up*MonsterRockMotion.Arc(age);
                stone.rotation=Quaternion.Euler(age*470,age*230,age*140);
            }
            for(int i=0;i<pieces.Length;i++)
            {
                var p=pieces[i];p.gameObject.SetActive(Fragments);if(!Fragments)continue;
                float t=age-MonsterRockMotion.Contact;float angle=i*2.39996f;
                p.position=contact+new Vector3(Mathf.Sin(angle)*2.1f*t,1.4f*t-4.9f*t*t,Mathf.Cos(angle)*1.5f*t);
                p.localScale=Vector3.one*(1-Mathf.Clamp01(t/.60f));p.rotation=Quaternion.Euler(i*43+t*380,i*31+t*530,t*190);
            }
        }
    }
}
