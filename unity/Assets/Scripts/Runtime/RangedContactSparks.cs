using UnityEngine;
using UnityEngine.Rendering;

namespace UltramanGame.Runtime
{
    // Overlapping contacts retain their own short ballistic wakes. The fixed
    // pool never allocates per shot and uses presentation time, not wall time.
    public sealed class RangedContactSparks
    {
        const int Bursts=4,Count=18,Samples=6;
        sealed class Burst
        {
            public readonly LineRenderer[] Lines=new LineRenderer[Count];
            public readonly Vector3[] Velocities=new Vector3[Count];
            public Vector3 Origin;
            public float Age=1;
        }
        readonly Burst[] pool=new Burst[Bursts];
        int next;
        public bool Visible {get;private set;}
        public RangedContactSparks(Transform parent)
        {
            var material=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("ContactSpark")));
            var width=new AnimationCurve(new Keyframe(0,.12f),new Keyframe(.75f,1),new Keyframe(1,.48f));
            for(int b=0;b<Bursts;b++)
            {
                var burst=pool[b]=new Burst();
                for(int i=0;i<Count;i++)
                {
                    var line=new GameObject("Contact spark "+b+"/"+i).AddComponent<LineRenderer>();line.transform.SetParent(parent,false);
                    line.sharedMaterial=material;line.positionCount=Samples;line.widthMultiplier=.080f+(i%3)*.014f;line.widthCurve=width;
                    line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.enabled=false;burst.Lines[i]=line;
                }
            }
        }
        public void Hit(Vector3 position,Vector3 incoming)
        {
            Vector3 axis=incoming.sqrMagnitude>.0001f?incoming.normalized:Vector3.forward;
            Vector3 side=Vector3.Cross(Vector3.up,axis);
            if(side.sqrMagnitude<.001f)side=Vector3.Cross(Vector3.right,axis);
            side.Normalize();var up=Vector3.Cross(axis,side).normalized;
            var burst=pool[next%Bursts];burst.Age=0;burst.Origin=position-axis*.10f;
            for(int i=0;i<Count;i++)
            {
                float a=i*2.399963f+next*.618f,speed=2.0f+(i%5)*.42f;
                burst.Velocities[i]=(side*Mathf.Cos(a)+up*Mathf.Sin(a))*speed-axis*(.50f+(i%3)*.22f)+Vector3.up*.30f;
            }
            next=(next+1)%Bursts;Tick(0);
        }
        public void Tick(float dt)
        {
            Visible=false;dt=Mathf.Max(0,dt);
            foreach(var burst in pool)
            {
                burst.Age+=dt;
                for(int i=0;i<Count;i++)
                {
                    var line=burst.Lines[i];float life=.40f+(i%4)*.09f,t=burst.Age/life;
                    line.enabled=t<1;if(!line.enabled)continue;Visible=true;
                    float fade=(1-t)*Mathf.Min(1,(1-t)*2.5f);line.startColor=new Color(1,.20f,.018f,0);
                    line.endColor=new Color(1,.78f,.32f,fade);
                    for(int j=0;j<Samples;j++)
                    {
                        float age=Mathf.Max(0,burst.Age-(1-j/(float)(Samples-1))*.11f);
                        line.SetPosition(j,burst.Origin+burst.Velocities[i]*age+Vector3.down*(2.6f*age*age));
                    }
                }
            }
        }
        public void Clear()
        {foreach(var burst in pool){burst.Age=1;foreach(var line in burst.Lines)line.enabled=false;}Visible=false;next=0;}
    }
}
