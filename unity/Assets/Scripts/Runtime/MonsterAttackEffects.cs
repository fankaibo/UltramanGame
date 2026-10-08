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
                float length=Mathf.Lerp(.28f,index==1?1.12f:.76f,growth);
                float width=index==1?.32f:.065f;
                float bow=.24f+(index-1)*.04f;
                tip+=normal*(index-1)*.12f-camera.transform.forward*.045f;
                for(int i=0;i<=Segments;i++)
                {
                    float u=i/(float)Segments;
                    var center=tip-direction*((1-u)*length)+normal*(Mathf.Sin(u*Mathf.PI)*bow);
                    var tangent=direction*length+normal*(Mathf.Cos(u*Mathf.PI)*Mathf.PI*bow);
                    var across=Vector3.Cross(camera.transform.forward,tangent).normalized;
                    float span=width*Mathf.Pow(Mathf.Max(0,Mathf.Sin(u*Mathf.PI)),.7f);
                    vertices[i*2]=center-across*span;vertices[i*2+1]=center+across*span;
                    var color=tint;color.a=fade*(index==1?.9f:.15f);colors[i*2]=colors[i*2+1]=color;
                }
                mesh.vertices=vertices;mesh.colors=colors;mesh.RecalculateBounds();
            }
        }
        sealed class RushDust
        {
            public readonly Transform Root;
            public readonly Material Material;
            readonly float lane, phase, size, spin;
            public RushDust(Transform parent,int index)
            {
                lane=(index%3-1)*.24f + ((index/3)%2==0?-.055f:.055f);
                phase=(index%4)*.075f;
                size=.42f+(index%3)*.09f;
                spin=-18f+index*11f;
                Material=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("ImpactCloud")));
                Material.SetFloat("_Seed",index*13.17f+.8f);
                Root=GameWorld.Primitive("Monster rush dust",PrimitiveType.Quad,parent,Vector3.zero,Vector3.one,Material);
                Root.gameObject.SetActive(false);
            }
            public void Draw(Camera camera,Vector3 monster,Vector3 forward,Vector3 side,float progress,float fade)
            {
                float local=progress-phase;
                if(local<0||local>.82f){Root.gameObject.SetActive(false);return;}
                float age=Mathf.Clamp01(local/.82f);
                float swell=Mathf.SmoothStep(0,1,Mathf.Clamp01(local/.20f));
                float drift=Mathf.SmoothStep(0,1,age);
                Root.gameObject.SetActive(true);
                Root.position=monster-forward*(.18f+drift*(.62f+(lane<0?.16f:.08f)))
                    +side*(lane*(.78f+.18f*drift))+Vector3.up*(.075f+size*.13f*swell+Mathf.Sin(age*Mathf.PI)*.07f);
                Root.rotation=camera.transform.rotation*Quaternion.Euler(0,0,spin+age*34f);
                Root.localScale=Vector3.one*size*(.32f+1.22f*swell);
                Material.color=new Color(.34f,.29f,.25f,fade*(1-age)*(.52f+.25f*swell));
                Material.SetFloat("_Age",Mathf.Lerp(.04f,.30f,age));
                Material.SetFloat("_Hot",.14f*(1-age));
            }
            public void Hide(){Root.gameObject.SetActive(false);}
        }
        readonly Vector3 home,target,forward;
        readonly LineRenderer charge,shock;
        readonly LineRenderer rushRing;
        readonly LineRenderer[] rushFootprints=new LineRenderer[2];
        readonly LineRenderer[] rushWakes=new LineRenderer[3];
        readonly RushDust[] rushDust=new RushDust[8];
        readonly ClawSweep[] claws=new ClawSweep[3];
        readonly Material glow;
        static readonly Color Amber=new Color(1,.40f,.10f),Ice=new Color(.25f,.86f,1);
        float hitAge=10;
        Color hitColor;
        bool blockedImpact,pendingImpact;
        Vector3 hitPosition,previousHand,sweepDirection;
        int attackNumber=-1,impactAttack=-1;
        public bool SlashVisible => claws[0].Visible;
        public bool RushVisible => rushWakes[0].enabled||rushWakes[1].enabled||rushWakes[2].enabled||rushDust[0].Root.gameObject.activeSelf||rushRing.enabled||rushFootprints[0].enabled||rushFootprints[1].enabled;
        public MonsterAttackEffects(Transform parent,Vector3 monsterHome,Vector3 heroHome)
        {
            home=monsterHome;target=heroHome;forward=(target-home).normalized;
            glow=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("SoftGlow")) {color=Color.white});
            charge=Line(parent,"Monster charge",48,.035f,true);
            shock=Line(parent,"Monster contact",48,.07f,true);
            // A ground-plane pressure ring gives the body rush a readable
            // volume cue on a 16:9 television. It expands with the same
            // EnemyAge as the feet and dust, so it cannot get out of sync or
            // remain after pause/restart.
            rushRing=Line(parent,"Monster rush pressure ring",49,.045f,true);
            for(int i=0;i<rushFootprints.Length;i++)
                rushFootprints[i]=Line(parent,"Monster rush foot pressure "+i,40,.028f,true);
            for(int i=0;i<rushWakes.Length;i++)
                rushWakes[i]=Line(parent,"Monster body rush wake "+i,7,.06f);
            for(int i=0;i<rushDust.Length;i++)rushDust[i]=new RushDust(parent,i);
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
        {hitAge=10;attackNumber=impactAttack=-1;pendingImpact=false;charge.enabled=shock.enabled=rushRing.enabled=false;foreach(var footprint in rushFootprints)footprint.enabled=false;foreach(var wake in rushWakes)wake.enabled=false;foreach(var dust in rushDust)dust.Hide();foreach(var claw in claws)claw.Visible=false;}
        public void Impact(bool blocked,Vector3 position,int attack)
        {hitAge=0;impactAttack=attack;pendingImpact=attack<0;hitPosition=position;blockedImpact=blocked;hitColor=blocked?Ice:Amber;}
        public void Tick(Battle state,Camera camera,float dt,Vector3? hand=null,Vector3? heroStance=null)
        {
            bool active=state.Phase==GamePhase.Battle;
            if(!active){hitAge=10;impactAttack=-1;pendingImpact=false;}else hitAge+=dt;
            // Older preview callers send the cue without Battle. Bind that cue
            // on its next sample rather than letting its contact follow the hand.
            if(active&&pendingImpact){impactAttack=state.EnemyAttackCount;pendingImpact=false;}
            bool warning=active&&state.Enemy==EnemyPhase.Windup&&!MonsterRayMotion.Active(state)&&!MonsterRockMotion.Active(state);
            bool attack=active&&state.Enemy==EnemyPhase.Attack;
            Vector3 destination=heroStance??target;
            float approach=CombatSpacing.Approach(Vector3.Distance(home,destination));
            Vector3 monster=home+forward*AnimatedActor.MonsterAdvance(state,approach);
            Vector3 contact=destination-forward*.7f+Vector3.up*2.15f-camera.transform.forward*.3f;
            Vector3 clawPosition=hand??contact-forward*Mathf.Max(0,AnimatedActor.EnemyAdvance-AnimatedActor.MonsterAdvance(state,approach));
            if(attackNumber!=state.EnemyAttackCount)
            {
                attackNumber=state.EnemyAttackCount;previousHand=clawPosition;
                float side=MonsterStepMotion.ClawLeft(attackNumber)?-1:1;
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
            // The reference arcade shot makes the enemy's whole body travel as
            // one readable beat.  A short, camera-facing heat wake connects the
            // planted body to its forward step; claws keep their own brighter
            // ribbons below.  Everything follows EnemyAge, so pause/restart
            // cannot leave a delayed streak behind.
            bool bodyRush=attack&&!MonsterRayMotion.Variant(state.EnemyAttackCount)&&!MonsterRockMotion.Variant(state.EnemyAttackCount)
                &&state.EnemyAge>=.045f&&state.EnemyAge<.59f;
            if(bodyRush)
            {
                float age=state.EnemyAge;
                float launch=Mathf.SmoothStep(0,1,Mathf.Clamp01((age-.045f)/.14f));
                float settle=Mathf.Pow(Mathf.Clamp01((.63f-age)/.20f),1.35f);
                float progress=Mathf.SmoothStep(0,1,Mathf.Clamp01(age/Battle.EnemyHitSeconds));
                var side=Vector3.Cross(Vector3.up,forward).normalized;
                for(int i=0;i<rushWakes.Length;i++)
                {
                    var line=rushWakes[i];line.enabled=true;
                    float sideOffset=(i-1)*.22f;
                    float height=i==1?1.25f:(i==0?.58f:2.05f);
                    // A long saturated ribbon reads like a red debug ray when
                    // the side camera catches the original monster position.
                    // Let dust and the bright claw carry the speed instead.
                    float length=Mathf.Lerp(.08f,.22f,progress);
                    for(int point=0;point<line.positionCount;point++)
                    {
                        float u=point/(float)(line.positionCount-1);
                        float tail=length*(1-u);
                        float bow=Mathf.Sin(u*Mathf.PI)*(.035f+.08f*progress);
                        line.SetPosition(point,monster-forward*(.08f+tail)+side*(sideOffset+bow)+Vector3.up*(height+Mathf.Sin(u*Mathf.PI)*.06f));
                    }
                    Color tint=i==1?new Color(1,.78f,.45f):new Color(1,.50f,.25f);
                    ColorLine(line,tint,launch*settle*(i==1?.22f:.10f));
                    line.widthMultiplier=(i==1?.065f:.036f)*(0.80f+.32f*progress);
                }
                float dustFade=launch*settle;
                var dustSide=Vector3.Cross(Vector3.up,forward).normalized;
                foreach(var dust in rushDust)dust.Draw(camera,monster,forward,dustSide,progress,dustFade);

                // The reference arcade shot sells a lunge with a widening
                // pressure footprint before the claw reaches the hero. Keep
                // it low and translucent so it supports the character rather
                // than becoming another bright line across the hands.
                float ringT=Mathf.Clamp01((age-.12f)/.42f);
                float ringFade=launch*settle*(1-Mathf.SmoothStep(.72f,1,ringT));
                rushRing.enabled=ringFade>.001f;
                if(rushRing.enabled)
                {
                    float radius=Mathf.Lerp(.20f,1.08f,Mathf.SmoothStep(0,1,ringT));
                    Vector3 center=monster+forward*(.10f+.20f*progress);center.y=.065f;
                    for(int point=0;point<rushRing.positionCount;point++)
                    {
                        float angle=point/(float)(rushRing.positionCount-1)*Mathf.PI*2;
                        rushRing.SetPosition(point,center+forward*(Mathf.Cos(angle)*radius)+dustSide*(Mathf.Sin(angle)*radius*.62f));
                    }
                    ColorLine(rushRing,new Color(1,.47f,.12f),ringFade*.46f);
                    rushRing.widthMultiplier=.035f+.035f*Mathf.Clamp01(ringT);
                }
                // Give each planted step a quiet, short-lived pressure mark.
                // It is deliberately dimmer than the claw and ring: the foot
                // contact should explain the body's weight without becoming a
                // second neon outline or a red debug ray on the TV.
                for(int foot=0;foot<rushFootprints.Length;foot++)
                {
                    float start=foot==0?.13f:.235f;
                    bool footActive=age>=start&&age<start+.30f;
                    float footT=Mathf.Clamp01((age-start)/.23f);
                    float footFade=launch*settle*(1-Mathf.SmoothStep(.60f,1,footT));
                    var mark=rushFootprints[foot];mark.enabled=footActive&&footFade>.001f&&age<.56f;
                    if(!mark.enabled)continue;
                    float radius=Mathf.Lerp(.16f,.62f,Mathf.SmoothStep(0,1,footT));
                    Vector3 center=monster+side*((foot==0?-1:1)*.37f)+forward*(.12f+.17f*progress);
                    center.y=.055f;
                    for(int point=0;point<mark.positionCount;point++)
                    {
                        float angle=point/(float)(mark.positionCount-1)*Mathf.PI*2;
                        mark.SetPosition(point,center+forward*(Mathf.Cos(angle)*radius)+side*(Mathf.Sin(angle)*radius*.58f));
                    }
                    ColorLine(mark,new Color(1,.73f,.36f),footFade*.26f);
                    mark.widthMultiplier=.022f+.018f*Mathf.Clamp01(footT);
                }
            }
            else {foreach(var wake in rushWakes)wake.enabled=false;foreach(var dust in rushDust)dust.Hide();rushRing.enabled=false;foreach(var footprint in rushFootprints)footprint.enabled=false;}
            for(int i=0;i<3;i++)
            {
                var claw=claws[i];float age=state.EnemyAge;
                claw.Visible=attack&&!MonsterRayMotion.Variant(state.EnemyAttackCount)&&!MonsterRockMotion.Variant(state.EnemyAttackCount)&&!MonsterSlamMotion.Variant(state.EnemyAttackCount)&&age>=.30f&&age<.64f;
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
                    // A pale air sheet gives the physical claw speed. Reserve
                    // the saturated orange/blue for the actual impact signal.
                    var air=new Color(.96f,.86f,.73f);
                    var tint=landed&&blockedImpact?Color.Lerp(air,Ice,Mathf.Clamp01(contactAge/.06f)):air;
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
