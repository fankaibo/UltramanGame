using UnityEngine;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    // Pooled scanned stone, ballistic flight and fragments share the attack clock.
    public sealed class MonsterRock
    {
        readonly Transform stone;
        readonly BoulderDust dust;
        public int ActiveDust=>dust.Visible;
        readonly Transform[] pieces=new Transform[7];
        Battle observed;
        Vector3 origin,destination,contact;
        Quaternion launchRotation;
        public Pose GroundPose {get;private set;}
        public bool Grounded {get;private set;}
        float groundOffset;
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
            var surface=stone.GetComponentInChildren<MeshFilter>();
            if(surface)foreach(var v in surface.sharedMesh.vertices)
                groundOffset=Mathf.Max(groundOffset,-stone.InverseTransformPoint(surface.transform.TransformPoint(v)).y);
            for(int i=0;i<pieces.Length;i++)pieces[i]=Rock(parent,"Broken boulder "+i,source,material,.10f+i%3*.045f);
            dust=new BoulderDust(parent);Clear();
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
            Vector3 sizeAxis=mesh.bounds.size;
            Vector3 longest=sizeAxis.x>=sizeAxis.y&&sizeAxis.x>=sizeAxis.z?Vector3.right:sizeAxis.y>=sizeAxis.z?Vector3.up:Vector3.forward;
            // Lay the scanned stone along the fingers instead of leaving its
            // import-space tall axis as a vertical slab through the wrist.
            surface.localRotation=Quaternion.FromToRotation(longest,Vector3.forward);
            surface.localScale=Vector3.one*scale;surface.localPosition=surface.localRotation*(-mesh.bounds.center*scale);return root;
        }
        public void SetGroundPose(Vector3 floor,Quaternion facing)
        {GroundPose=new Pose(floor+Vector3.up*(groundOffset+.015f),facing);}
        public void Clear(){dust.Clear();Started=Flying=Holding=Fragments=Grounded=false;stone.gameObject.SetActive(false);foreach(var p in pieces)p.gameObject.SetActive(false);}
        public void Impact(Vector3 position){contact=position;impacted=true;}
        public void Tick(Battle state,Pose grip,Vector3 target,bool suppressed,Camera camera,float pickupAge=-1)
        {
            Started=false;
            if(!ReferenceEquals(observed,state)){observed=state;sequence=-1;Launches=0;impacted=false;}
            if(suppressed||!MonsterRockMotion.Active(state)){Clear();return;}
            bool attack=state.Enemy==EnemyPhase.Attack;
            float age=state.EnemyAge;
            if(state.Enemy==EnemyPhase.Windup)impacted=false;
            float pickup=pickupAge<0?MonsterRockMotion.PickupAge(state):pickupAge;
            Grounded=state.Enemy==EnemyPhase.Windup&&pickup<MonsterRockMotion.GraspAge;
            Holding=state.Enemy==EnemyPhase.Windup&&!Grounded||attack&&age<MonsterRockMotion.Launch;
            Flying=attack&&age>=MonsterRockMotion.Launch&&age<MonsterRockMotion.Contact;
            Fragments=attack&&impacted&&age>=MonsterRockMotion.Contact;
            stone.gameObject.SetActive(Grounded||Holding||Flying);
            if(Grounded){stone.SetPositionAndRotation(GroundPose.position,GroundPose.rotation);stone.localScale=Vector3.one;}
            if(Holding)
            {
                float grasp=attack?1:Mathf.SmoothStep(0,1,(pickup-MonsterRockMotion.GraspAge)/.12f);
                stone.SetPositionAndRotation(Vector3.Lerp(GroundPose.position,grip.position,grasp),Quaternion.Slerp(GroundPose.rotation,grip.rotation,grasp));
                stone.localScale=Vector3.one;
            }
            if(attack&&age>=MonsterRockMotion.Launch&&sequence!=state.EnemyAttackCount)
            {sequence=state.EnemyAttackCount;Launches++;Started=true;origin=grip.position;launchRotation=grip.rotation;destination=target;stone.localScale=Vector3.one;}
            if(Flying)
            {
                destination=target;
                stone.position=Vector3.Lerp(origin,destination,MonsterRockMotion.Travel(age))+Vector3.up*MonsterRockMotion.Arc(age);
                float spinAge=age-MonsterRockMotion.Launch;
                stone.rotation=launchRotation*Quaternion.Euler(spinAge*470,spinAge*230,spinAge*140);
            }
            if(state.Enemy==EnemyPhase.Attack||state.Enemy==EnemyPhase.Recover)
                dust.Tick(state.EnemyAttackCount,state.Enemy==EnemyPhase.Recover?Battle.EnemyAttackSeconds+age:age,stone.position,impacted,contact,camera);
            else dust.Clear();
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
