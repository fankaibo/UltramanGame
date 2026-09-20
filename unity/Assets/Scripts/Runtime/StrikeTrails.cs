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
            readonly Mesh mesh;
            readonly MeshRenderer renderer;
            readonly Vector3[] points=new Vector3[Capacity],vertices=new Vector3[Capacity*2];
            readonly float[] times=new float[Capacity];
            readonly Color[] colors=new Color[Capacity*2];
            readonly Vector2[] uv=new Vector2[Capacity*2];
            readonly int[] triangles=new int[(Capacity-1)*6];
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
                for(int i=0;i<Capacity-1;i++)
                {int v=i*2,t=i*6;triangles[t]=v;triangles[t+1]=v+1;triangles[t+2]=v+2;triangles[t+3]=v+1;triangles[t+4]=v+3;triangles[t+5]=v+2;}
            }
            public void Clear(){count=0;renderer.enabled=false;}
            public void Tick(Camera camera,float clock,bool emit,Vector3 point)
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
                for(int i=0;i<count;i++)
                {
                    var direction=points[Mathf.Min(count-1,i+1)]-points[Mathf.Max(0,i-1)];
                    var across=Vector3.Cross(camera.transform.forward,direction).normalized;
                    float freshness=Mathf.Clamp01(1-(clock-times[i])/lifetime);
                    float end=i/(float)(count-1);
                    float span=width*(.2f+.8f*Mathf.Sin(end*Mathf.PI))*(.5f+.5f*freshness);
                    vertices[i*2]=points[i]-across*span;vertices[i*2+1]=points[i]+across*span;
                    uv[i*2]=new Vector2(end,0);uv[i*2+1]=new Vector2(end,1);
                    var color=new Color(1,1,1,freshness*freshness*Mathf.SmoothStep(0,1,end*4));
                    colors[i*2]=colors[i*2+1]=color;
                }
                // Collapse unused segments instead of allocating a new mesh or
                // variable-sized array on every camera frame.
                for(int i=count;i<Capacity;i++)
                {vertices[i*2]=vertices[i*2+1]=points[count-1];colors[i*2]=colors[i*2+1]=Color.clear;}
                mesh.vertices=vertices;mesh.colors=colors;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateBounds();
            }
        }
        readonly Ribbon hero;
        readonly Ribbon[] claws=new Ribbon[3];
        HeroAction lastAction;
        float clock,lastHeroAge;
        int lastEnemyAttack;
        public bool HeroVisible=>hero.Visible;
        public bool MonsterVisible=>claws[0].Visible||claws[1].Visible||claws[2].Visible;
        public StrikeTrails(Transform parent)
        {
            hero=new Ribbon(parent,"Hero striking hand wake",new Color(.32f,.74f,1,1f),.72f,.30f);
            // One lead claw carries the readable contact streak. Two narrower,
            // shorter echoes add speed without making the attack look like three
            // identical debug lines.
            claws[0]=new Ribbon(parent,"Monster moving claw 0",new Color(1,.31f,.08f,.58f),.17f,.22f);
            claws[1]=new Ribbon(parent,"Monster moving claw 1",new Color(1,.52f,.16f,.96f),.46f,.32f);
            claws[2]=new Ribbon(parent,"Monster moving claw 2",new Color(1,.37f,.10f,.62f),.20f,.24f);
        }
        public void Clear()
        {hero.Clear();foreach(var claw in claws)claw.Clear();lastAction=HeroAction.None;lastHeroAge=0;lastEnemyAttack=0;}
        public void Tick(Battle state,Camera camera,float dt,AnimatedActor heroActor,AnimatedActor enemyActor,bool closeup)
        {
            if(state.Phase!=GamePhase.Battle||closeup||heroActor==null||enemyActor==null){Clear();return;}
            if(dt<=0)return;
            clock+=dt;
            bool punch=state.Action==HeroAction.LeftPunch||state.Action==HeroAction.RightPunch;
            if(punch&&(state.Action!=lastAction||state.ActionAge<lastHeroAge))hero.Clear();
            if(state.EnemyAttackCount!=lastEnemyAttack)foreach(var claw in claws)claw.Clear();
            bool emitHero=punch&&state.ActionAge>=.025f&&state.ActionAge<.34f;
            bool emitMonster=state.Enemy==EnemyPhase.Attack&&state.EnemyAge>=.12f&&state.EnemyAge<.66f;
            hero.Tick(camera,clock,emitHero,heroActor.StrikeOrigin(state.Action));
            var hand=enemyActor.EnemyStrikeOrigin(state);
            for(int i=0;i<claws.Length;i++)
            {
                float side=i-1;
                claws[i].Tick(camera,clock,emitMonster,
                    hand+camera.transform.right*side*(i==1?.025f:.065f)
                        +camera.transform.up*side*(i==1?.012f:.025f));
            }
            lastAction=state.Action;lastHeroAge=state.ActionAge;lastEnemyAttack=state.EnemyAttackCount;
        }
    }
}
