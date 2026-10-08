using UnityEngine;
using UnityEngine.Rendering;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    // Samples the actual striking hand, with short world-space histories. No
    // prefab animation or delayed callback can outlive a pause or hero change.
    public sealed class StrikeTrails
    {
        sealed class Ribbon
        {
            const int Capacity=24;
            const int Subdivisions=3,SampleCapacity=(Capacity-1)*Subdivisions+1;
            readonly Mesh mesh;
            readonly MeshRenderer renderer;
            readonly Vector3[] points=new Vector3[Capacity],samples=new Vector3[SampleCapacity],vertices=new Vector3[SampleCapacity*2];
            readonly float[] times=new float[Capacity];
            readonly float[] sampleTimes=new float[SampleCapacity],distances=new float[SampleCapacity];
            readonly Color[] colors=new Color[SampleCapacity*2];
            readonly Vector2[] uv=new Vector2[SampleCapacity*2];
            readonly int[] triangles=new int[(SampleCapacity-1)*6];
            readonly float width,lifetime;
            int count;
            public bool Visible => renderer.enabled;
            public Ribbon(Transform parent,string name,Color color,float width,float lifetime)
            {
                this.width=width;this.lifetime=lifetime;
                var go=new GameObject(name);go.transform.SetParent(parent,false);
                mesh=RuntimeResources.Own(parent,new Mesh{name=name});mesh.MarkDynamic();
                go.AddComponent<MeshFilter>().sharedMesh=mesh;
                renderer=go.AddComponent<MeshRenderer>();renderer.enabled=false;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                renderer.sharedMaterial=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("StrikeRibbon")){color=color});
                for(int i=0;i<SampleCapacity-1;i++)
                {int v=i*2,t=i*6;triangles[t]=v;triangles[t+1]=v+1;triangles[t+2]=v+2;triangles[t+3]=v+1;triangles[t+4]=v+3;triangles[t+5]=v+2;}
            }
            public void Clear(){count=0;renderer.enabled=false;}
            public void Tick(Camera camera,float clock,bool emit,Vector3 point,float opacity=1)
            {
                int expired=0;while(expired<count&&clock-times[expired]>=lifetime)expired++;
                if(expired>0)
                {for(int i=expired;i<count;i++){points[i-expired]=points[i];times[i-expired]=times[i];}count-=expired;}
                // A discontinuous pose or a newly selected actor must never draw
                // a streak across the stage. Still allow a quick extended fist.
                if(emit&&count>0&&(point-points[count-1]).sqrMagnitude>2.25f)Clear();
                if(emit&&(count==0||(point-points[count-1]).sqrMagnitude>.000025f))
                {
                    if(count==Capacity){for(int i=1;i<count;i++){points[i-1]=points[i];times[i-1]=times[i];}count--;}
                    points[count]=point;times[count]=clock;count++;
                }
                renderer.enabled=count>1;
                if(!renderer.enabled)return;
                // Interpolate only recorded motion, never predict the next hand
                // position. Bounded tangents keep a turn from overshooting the fist.
                int sampleCount=(count-1)*Subdivisions+1;
                for(int i=0;i<count-1;i++)
                {
                    float length=Vector3.Distance(points[i],points[i+1]);
                    var start=Vector3.ClampMagnitude((points[i+1]-points[Mathf.Max(0,i-1)])*.5f,length);
                    var end=Vector3.ClampMagnitude((points[Mathf.Min(count-1,i+2)]-points[i])*.5f,length);
                    for(int j=0;j<Subdivisions;j++)
                    {
                        float t=j/(float)Subdivisions,t2=t*t,t3=t2*t;int index=i*Subdivisions+j;
                        samples[index]=(2*t3-3*t2+1)*points[i]+(t3-2*t2+t)*start
                            +(-2*t3+3*t2)*points[i+1]+(t3-t2)*end;
                        sampleTimes[index]=Mathf.Lerp(times[i],times[i+1],t);
                    }
                }
                samples[sampleCount-1]=points[count-1];sampleTimes[sampleCount-1]=times[count-1];
                distances[0]=0;
                for(int i=1;i<sampleCount;i++)distances[i]=distances[i-1]+Vector3.Distance(samples[i-1],samples[i]);
                float trailLength=distances[sampleCount-1];
                var previousAcross=Vector3.zero;
                for(int i=0;i<sampleCount;i++)
                {
                    var direction=samples[Mathf.Min(sampleCount-1,i+1)]-samples[Mathf.Max(0,i-1)];
                    var across=Vector3.Cross(camera.transform.forward,direction).normalized;
                    if(across.sqrMagnitude<.01f)across=previousAcross.sqrMagnitude>.01f?previousAcross:camera.transform.up;
                    // Retraction can reverse the tangent. Keep both ribbon edges
                    // on their own side instead of crossing into a bright triangle.
                    if(Vector3.Dot(across,previousAcross)<0)across=-across;
                    previousAcross=across;
                    float freshness=Mathf.Clamp01(1-(clock-sampleTimes[i])/lifetime);
                    float end=trailLength>.0001f?distances[i]/trailLength:0;
                    float taper=Mathf.SmoothStep(0,1,end*4)*Mathf.SmoothStep(0,1,(1-end)*5);
                    float span=Mathf.Min(width,trailLength*.28f)*taper*(.5f+.5f*freshness);
                    vertices[i*2]=samples[i]-across*span;vertices[i*2+1]=samples[i]+across*span;
                    uv[i*2]=new Vector2(end,0);uv[i*2+1]=new Vector2(end,1);
                    var color=new Color(1,1,1,freshness*freshness*Mathf.SmoothStep(0,1,end*4)*opacity);
                    colors[i*2]=colors[i*2+1]=color;
                }
                // Collapse unused segments instead of allocating a new mesh or
                // variable-sized array on every camera frame.
                for(int i=sampleCount;i<SampleCapacity;i++)
                {vertices[i*2]=vertices[i*2+1]=points[count-1];colors[i*2]=colors[i*2+1]=Color.clear;}
                mesh.vertices=vertices;mesh.colors=colors;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateBounds();
            }
        }
        readonly Ribbon hero;
        // A short secondary ribbon is reserved for combo contacts. The main
        // cyan wake shows the hand path; this warmer echo gives the cabinet
        // style fifth-hit beat a visible afterimage without leaving a trail
        // during ordinary punches or between rounds.
        readonly Ribbon heroComboEcho;
        readonly Ribbon[] claws=new Ribbon[3];
        HeroAction lastAction;
        float clock,lastHeroAge;
        int lastEnemyAttack;
        public bool HeroVisible=>hero.Visible;
        public bool MonsterVisible=>claws[0].Visible||claws[1].Visible||claws[2].Visible;
        public StrikeTrails(Transform parent)
        {
            // The reference cabinet exposes the travel between anticipation
            // and contact as a readable air wake.  Keep it short and tied to
            // the sampled hand, but give the ribbon enough width and lifetime
            // to survive a living-room TV's 16:9 scale.
            hero=new Ribbon(parent,"Hero striking hand wake",new Color(.32f,.74f,1,.96f),.48f,.27f);
            // These are short hand-motion wisps, not a second set of glowing
            // claws. The wider contact sweep takes over near the collision.
            claws[0]=new Ribbon(parent,"Monster moving claw 0",new Color(.94f,.82f,.68f,.24f),.13f,.17f);
            claws[1]=new Ribbon(parent,"Monster moving claw 1",new Color(.94f,.84f,.72f,.62f),.48f,.22f);
            claws[2]=new Ribbon(parent,"Monster moving claw 2",new Color(.94f,.82f,.68f,.22f),.13f,.17f);
            heroComboEcho=new Ribbon(parent,"Hero combo afterimage",new Color(1,.56f,.18f,.78f),.24f,.16f);
        }
        public void Clear()
        {hero.Clear();heroComboEcho.Clear();foreach(var claw in claws)claw.Clear();lastAction=HeroAction.None;lastHeroAge=0;lastEnemyAttack=0;}
        public void Tick(Battle state,Camera camera,float dt,AnimatedActor heroActor,AnimatedActor enemyActor,bool closeup)
        {
            if(state.Phase!=GamePhase.Battle||closeup||heroActor==null||enemyActor==null){Clear();return;}
            if(dt<=0)return;
            clock+=dt;
            bool punch=state.Action==HeroAction.LeftPunch||state.Action==HeroAction.RightPunch;
            if(punch&&(state.Action!=lastAction||state.ActionAge<lastHeroAge))hero.Clear();
            if(state.EnemyAttackCount!=lastEnemyAttack)foreach(var claw in claws)claw.Clear();
            bool emitHero=punch&&state.ActionAge>=.025f&&state.ActionAge<.34f;
            bool combo=ComboStrikeMotion.Active(state);
            bool slam=MonsterSlamMotion.Variant(state.EnemyAttackCount);
            bool emitMonster=state.Enemy==EnemyPhase.Attack&&!MonsterRayMotion.Variant(state.EnemyAttackCount)&&!MonsterRockMotion.Variant(state.EnemyAttackCount)&&state.EnemyAge>=.12f&&state.EnemyAge<(slam?MonsterSlamMotion.GroundSeconds:.66f);
            hero.Tick(camera,clock,emitHero&&(!HeroKickMotion.Active(state)||state.ActionAge<.23f),heroActor.StrikeContact(state));
            // Echo only the contact-facing part of a combo strike. It fades in
            // the same bounded history as the main wake, so a paused frame,
            // input handoff, or photo round cannot leave an orphaned afterimage.
            heroComboEcho.Tick(camera,clock,emitHero&&combo&&state.ActionAge<.24f,
                heroActor.StrikeContact(state),.82f);
            var hand=enemyActor.EnemyStrikeOrigin(state);
            float wakeOpacity=slam?1:1-.75f*Mathf.SmoothStep(0,1,(state.EnemyAge-.27f)/.12f);
            for(int i=0;i<claws.Length;i++)
            {
                float side=i-1;
                claws[i].Tick(camera,clock,emitMonster,
                    (slam?(i==0?enemyActor.StrikeOrigin(HeroAction.LeftPunch):enemyActor.HandPosition):hand)+camera.transform.right*side*(i==1?.025f:.065f)
                        +camera.transform.up*side*(i==1?.012f:.025f),wakeOpacity);
            }
            lastAction=state.Action;lastHeroAge=state.ActionAge;lastEnemyAttack=state.EnemyAttackCount;
        }
    }
}
