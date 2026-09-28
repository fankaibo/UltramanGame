using UnityEngine;
using UnityEngine.Rendering;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    // All effects share Battle's attack clock; no delayed callbacks survive pause or restart.
    public sealed class MonsterAttackEffects
    {
        sealed class ClawSweep
        {
            const int Segments=32;
            readonly Mesh mesh;
            readonly MeshRenderer renderer;
            readonly Vector3[] vertices=new Vector3[(Segments+1)*2];
            readonly Color[] colors=new Color[(Segments+1)*2];
            public bool Visible {get=>renderer.enabled;set=>renderer.enabled=value;}
            public ClawSweep(Transform parent,Material material,int index)
            {
                var go=new GameObject("Monster claw sweep "+index);go.transform.SetParent(parent,false);
                mesh=RuntimeResources.Own(parent,new Mesh{name=go.name});mesh.MarkDynamic();
                go.AddComponent<MeshFilter>().sharedMesh=mesh;
                renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.enabled=false;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                var uv=new Vector2[vertices.Length];var triangles=new int[Segments*6];
                for(int i=0;i<=Segments;i++)
                {
                    uv[i*2]=new Vector2(i/(float)Segments,0);uv[i*2+1]=new Vector2(i/(float)Segments,1);
                    if(i==Segments)continue;
                    int v=i*2,t=i*6;triangles[t]=v;triangles[t+1]=v+1;triangles[t+2]=v+2;
                    triangles[t+3]=v+1;triangles[t+4]=v+3;triangles[t+5]=v+2;
                }
                mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;
            }
            public void Draw(Camera camera,Vector3 tip,Vector3 direction,int index,float growth,float fade,Color tint)
            {
                var normal=Vector3.Cross(camera.transform.forward,direction).normalized;
                float length=Mathf.Lerp(.28f,index==1?1.0f:.76f,growth);
                float width=index==1?.17f:.085f;
                float bow=.16f+(index-1)*.04f;
                tip+=normal*(index-1)*.12f-camera.transform.forward*.045f;
                for(int i=0;i<=Segments;i++)
                {
                    float u=i/(float)Segments;
                    var center=tip-direction*((1-u)*length)+normal*(Mathf.Sin(u*Mathf.PI)*bow);
                    var tangent=direction*length+normal*(Mathf.Cos(u*Mathf.PI)*Mathf.PI*bow);
                    var across=Vector3.Cross(camera.transform.forward,tangent).normalized;
                    float span=width*Mathf.Pow(Mathf.Max(0,Mathf.Sin(u*Mathf.PI)),.8f);
                    vertices[i*2]=center-across*span;vertices[i*2+1]=center+across*span;
                    var color=tint;color.a=fade*(index==1?1:.48f);colors[i*2]=colors[i*2+1]=color;
                }
                mesh.vertices=vertices;mesh.colors=colors;mesh.RecalculateBounds();
            }
        }
        readonly Vector3 home,target,forward;
        readonly LineRenderer charge,shock;
        readonly ClawSweep[] claws=new ClawSweep[3];
        readonly Material glow;
        static readonly Color Amber=new Color(1,.40f,.10f),Ice=new Color(.25f,.86f,1);
        float hitAge=10;
        Color hitColor;
        bool blockedImpact,pendingImpact;
        Vector3 hitPosition,previousHand,sweepDirection;
        int attackNumber=-1,impactAttack=-1;
        public bool SlashVisible => claws[0].Visible;
        public MonsterAttackEffects(Transform parent,Vector3 monsterHome,Vector3 heroHome)
        {
            home=monsterHome;target=heroHome;forward=(target-home).normalized;
            glow=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("SoftGlow")) {color=Color.white});
            charge=Line(parent,"Monster charge",48,.035f,true);
            shock=Line(parent,"Monster contact",48,.07f,true);
            var sweep=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("ClawSweep")));
            for(int i=0;i<3;i++)claws[i]=new ClawSweep(parent,sweep,i);
        }
        LineRenderer Line(Transform parent,string name,int points,float width,bool loop=false)
        {
            var line=new GameObject(name).AddComponent<LineRenderer>();line.transform.SetParent(parent,false);
            line.sharedMaterial=glow;line.useWorldSpace=true;line.positionCount=points;line.widthMultiplier=width;
            line.loop=loop;line.numCapVertices=4;line.enabled=false;return line;
        }
        static void ColorLine(LineRenderer line,Color color,float alpha)
        {color.a=alpha;line.startColor=line.endColor=color;}
        static void Circle(LineRenderer line,Vector3 center,Camera camera,float radius)
        {
            for(int i=0;i<line.positionCount;i++)
            {
                float angle=i*Mathf.PI*2/line.positionCount;
                line.SetPosition(i,center+camera.transform.right*Mathf.Cos(angle)*radius+camera.transform.up*Mathf.Sin(angle)*radius);
            }
        }
        public void Clear()
        {hitAge=10;attackNumber=impactAttack=-1;pendingImpact=false;charge.enabled=shock.enabled=false;foreach(var claw in claws)claw.Visible=false;}
        public void Impact(bool blocked,Vector3 position,int attack)
        {hitAge=0;impactAttack=attack;pendingImpact=attack<0;hitPosition=position;blockedImpact=blocked;hitColor=blocked?Ice:Amber;}
        public void Tick(Battle state,Camera camera,float dt,Vector3? hand=null)
        {
            bool active=state.Phase==GamePhase.Battle;
            if(!active){hitAge=10;impactAttack=-1;pendingImpact=false;}else hitAge+=dt;
            // Older preview callers send the cue without Battle. Bind that cue
            // on its next sample rather than letting its contact follow the hand.
            if(active&&pendingImpact){impactAttack=state.EnemyAttackCount;pendingImpact=false;}
            bool warning=active&&state.Enemy==EnemyPhase.Windup;
            bool attack=active&&state.Enemy==EnemyPhase.Attack;
            Vector3 monster=home+forward*AnimatedActor.MonsterAdvance(state);
            Vector3 contact=target-forward*.7f+Vector3.up*2.15f-camera.transform.forward*.3f;
            Vector3 clawPosition=hand??contact-forward*Mathf.Max(0,AnimatedActor.EnemyAdvance-AnimatedActor.MonsterAdvance(state));
            if(attackNumber!=state.EnemyAttackCount)
            {
                attackNumber=state.EnemyAttackCount;previousHand=clawPosition;
                float side=attackNumber%2==0?-1:1;
                sweepDirection=(camera.transform.right*(side*.65f)-camera.transform.up*.76f).normalized;
            }
            if(attack&&dt>0)
            {
                var motion=Vector3.ProjectOnPlane(clawPosition-previousHand,camera.transform.forward);
                if(state.EnemyAge<Battle.EnemyHitSeconds&&motion.sqrMagnitude>.00001f)
                    sweepDirection=Vector3.Lerp(sweepDirection,motion.normalized,1-Mathf.Exp(-dt*28)).normalized;
                previousHand=clawPosition;
            }
            charge.enabled=warning;
            if(warning)
            {
                float p=Mathf.Clamp01(state.EnemyAge/state.WarningDuration);
                Circle(charge,monster+Vector3.up*3.1f-camera.transform.forward*.2f,camera,.35f+p*.24f);
                ColorLine(charge,Amber,.20f+p*.45f);
            }
            for(int i=0;i<3;i++)
            {
                var claw=claws[i];float age=state.EnemyAge;
                claw.Visible=attack&&!MonsterSlamMotion.Variant(state.EnemyAttackCount)&&age>=.30f&&age<.64f;
                if(claw.Visible)
                {
                    float growth=Mathf.SmoothStep(0,1,Mathf.Clamp01((age-.30f)/.10f));
                    float fade=Mathf.SmoothStep(0,1,Mathf.Clamp01((age-.30f)/.04f))
                        *Mathf.Pow(Mathf.Clamp01((.64f-age)/.24f),2);
                    // At impact the light belongs to the contact surface, not
                    // the retreating claw. It disperses there while the arm recoils.
                    bool landed=age>=Battle.EnemyHitSeconds&&impactAttack==state.EnemyAttackCount;
                    float contactAge=Mathf.Max(0,age-Battle.EnemyHitSeconds);
                    var tip=landed?hitPosition+sweepDirection*(contactAge*.35f):clawPosition;
                    var tint=landed&&blockedImpact?Color.Lerp(Amber,Ice,Mathf.Clamp01(contactAge/.06f)):Amber;
                    claw.Draw(camera,tip,sweepDirection,i,growth,fade,tint);
                }
            }
            // Blue ripples now live on the shield surface at the claw contact.
            // Keep this free-standing shock ring only for an unblocked hit.
            shock.enabled=active&&hitAge<.38f&&!blockedImpact;
            if(shock.enabled)
            {Circle(shock,hitPosition,camera,Mathf.Lerp(.13f,1.05f,hitAge/.38f));ColorLine(shock,hitColor,(1-hitAge/.38f)*.85f);}
        }
    }
}
