using UnityEngine;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    // Fixed pools and explicit game clocks also make effects reproducible in review captures.
    public sealed class CombatVfx
    {
        sealed class Streak { public LineRenderer Line;public Vector3 Position,Velocity;public float Age,Life,Width; }
        sealed class Flash {public Transform Quad;public Material Material;public Vector3 Position;public float Age=10,Life,Size,Opacity;public bool Ground;}
        sealed class RayFlash {public LineRenderer Line;public Vector3 Origin,Direction;public Color Color;public float Age=10,Life,Width;}
        readonly Streak[] sparks=new Streak[96];
        readonly Flash[] flashes=new Flash[12];
        readonly RayFlash[] hitRays=new RayFlash[24];
        readonly Transform shield,charge;
        readonly Material shieldMaterial,chargeMaterial;
        readonly BeamCharge beamCharge;
        readonly BeamStream beam;
        readonly LineRenderer warningRing,attackRing;
        readonly LineRenderer shieldHex;
        readonly LineRenderer contactWave;
        readonly Light muzzleLight,hitLight;
        readonly Material lineMaterial;
        readonly ImpactAtmosphere atmosphere;
        readonly StrikeContactBurst strikeContact;
        readonly GroundImpact groundImpact;
        readonly BeamImpactVolume beamImpact;
        public bool BeamImpactVisible=>beamImpact.Visible;
        public float BeamImpactAge=>beamImpact.Age;
        public int BeamImpactCount=>beamImpact.Bursts;
        public bool ChargeVisible=>beamCharge.Visible;
        public float ChargePower=>beamCharge.Power;
        public Vector3 ChargeCenter=>beamCharge.Center;
        public int ActiveContactCount=>strikeContact.ActiveCount;
        public int ActiveGroundStones=>groundImpact.ActiveStones;
        public int ActiveGroundDust=>groundImpact.ActiveClouds;
        public float GroundImpactAge=>groundImpact.LastAge;
        public event System.Action<Vector3,string> GroundContact
        {add{groundImpact.Contact+=value;}remove{groundImpact.Contact-=value;}}
        public int GroundContactCount=>groundImpact.Bursts;
        public string GroundContactCause=>groundImpact.LastCause;
        int sparkIndex,flashIndex,hitRayIndex;
        float hitLightAge=10,hitLightPower=3,hitLightDuration=.22f,shieldHitAge=10,clock,beamBurstAge;
        float contactWaveAge=10,contactWaveLife=.22f,contactWaveSize=.8f;
        Vector3 contactWavePosition;
        Color contactWaveColor=Color.white;
        Vector3 shieldHitWorld;
        bool shieldHitPending;
        float previousEnemyAge;
        int previousAttack,previousPunches;
        bool motionInitialized;
        public int ActiveSparkCount {get;private set;}
        public bool BeamVisible => beam.Visible;
        static readonly Color Ice=new Color(.15f,.65f,1),Warm=new Color(1,.48f,.12f);
        Color beamTint=Ice,beamAccent=new Color(.90f,.98f,1);
        bool beamDual;
        bool chestMounted;
        public CombatVfx(Transform parent)
        {
            atmosphere=new ImpactAtmosphere(parent);
            strikeContact=new StrikeContactBurst(parent);
            groundImpact=new GroundImpact(parent);
            beamImpact=new BeamImpactVolume(parent);
            beamCharge=new BeamCharge(parent);
            lineMaterial=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("SoftGlow")){color=Color.white});
            for(int i=0;i<sparks.Length;i++)sparks[i]=new Streak {Line=Line(parent,"Impact streak",2,.03f)};
            for(int i=0;i<flashes.Length;i++)
            {
                var material=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("EnergyFlare")));
                flashes[i]=new Flash {Material=material,Quad=GameWorld.Primitive("Impact flare",PrimitiveType.Quad,parent,Vector3.zero,Vector3.one,material)};
                flashes[i].Quad.gameObject.SetActive(false);
            }
            for(int i=0;i<hitRays.Length;i++)hitRays[i]=new RayFlash {Line=Line(parent,"Arcade impact ray",2,.035f)};
            shieldMaterial=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("EnergyShield")));
            shield=GameWorld.Primitive("Light shield",PrimitiveType.Sphere,parent,Vector3.zero,new Vector3(1.9f,2.15f,.38f),shieldMaterial);
            chargeMaterial=RuntimeResources.Own(parent,new Material(Resources.Load<Shader>("EnergyFlare")));
            charge=GameWorld.Primitive("Beam energy focus",PrimitiveType.Quad,parent,Vector3.zero,Vector3.one,chargeMaterial);
            beam=new BeamStream(parent);
            warningRing=Line(parent,"Monster warning ground ring",64,.035f);
            attackRing=Line(parent,"Monster attack ground ring",64,.05f);
            shieldHex=Line(parent,"Shield six-sided energy edge",7,.035f);
            // A camera-facing contact wave gives a TV-sized hit a readable
            // expansion beat in addition to the point flare. It shares the
            // exact Impact clock, so it cannot outlive a pause or introduce a
            // second damage event.
            contactWave=Line(parent,"Arcade contact shockwave",37,.045f);
            muzzleLight=Point(parent,"Energy spill",Ice,5);hitLight=Point(parent,"Impact spill",Warm,4);
            // This short-lived, small light needs per-pixel falloff on the
            // low-poly skin instead of being demoted to broad vertex lighting.
            hitLight.renderMode=LightRenderMode.ForcePixel;
            beam.SetProfile(beamTint,beamAccent,beamDual);
        }
        public void SetHeroProfile(string heroId)
        {
            var hero=HeroRoster.At(HeroRoster.Index(heroId));
            beamTint=ParseColor(hero.BeamTint,Ice);beamAccent=ParseColor(hero.BeamAccent,Color.white);beamDual=hero.BeamDual;chestMounted=heroId=="Zero";beamCharge.SetMebium(heroId=="Mebius");beamCharge.SetGrigio(heroId=="Grigio");muzzleLight.color=heroId=="Mebius"||heroId=="Grigio"?beamTint:Ice;
            beam.SetProfile(beamTint,beamAccent,beamDual);
        }
        static Color ParseColor(string value,Color fallback)
        {return ColorUtility.TryParseHtmlString(value,out var parsed)?parsed:fallback;}
        LineRenderer Line(Transform parent,string name,int count,float width)
        {
            var line=new GameObject(name).AddComponent<LineRenderer>();line.transform.SetParent(parent,false);line.sharedMaterial=lineMaterial;
            line.positionCount=count;line.widthMultiplier=width;line.numCapVertices=3;line.enabled=false;
            line.widthCurve=new AnimationCurve(new Keyframe(0,.25f),new Keyframe(.18f,1),new Keyframe(.8f,.8f),new Keyframe(1,0));return line;
        }
        static Light Point(Transform parent,string name,Color color,float range)
        {var light=new GameObject(name).AddComponent<Light>();light.transform.SetParent(parent,false);light.type=LightType.Point;light.color=color;light.range=range;light.intensity=0;return light;}
        static void Tint(LineRenderer line,Color color,float alpha)
        {color.a=alpha;line.startColor=color;color.a=0;line.endColor=color;}
        public void Burst(Vector3 position,int count,float force,bool warm)
        {
            for(int i=0;i<count;i++)
            {
                var s=sparks[sparkIndex++%sparks.Length];s.Position=position;s.Age=0;s.Life=Random.Range(.28f,.62f);
                s.Velocity=(Random.onUnitSphere*Random.Range(2,4.5f)+Vector3.up*.8f)*force;s.Width=Random.Range(.012f,.033f);
                s.Line.SetPosition(0,position);s.Line.SetPosition(1,position);s.Line.widthMultiplier=s.Width;
                Tint(s.Line,warm?Warm:Color.Lerp(Ice,Color.white,Random.value*.7f),.85f);s.Line.enabled=true;
            }
        }
        void FlashAt(Vector3 position,float size,float duration,Color color,bool ring=false,bool ground=false)
        {
            var f=flashes[flashIndex++%flashes.Length];f.Age=0;f.Life=duration;f.Position=position;f.Size=size;f.Ground=ground;f.Opacity=color.a;
            f.Quad.position=position;f.Quad.localScale=Vector3.one*size*.55f;
            f.Material.SetFloat("_Ring",ring?1:0);f.Material.color=color;f.Quad.gameObject.SetActive(true);
        }
        public void Impact(Vector3 position,bool special,bool blocked=false,bool hurt=false,bool combo=false,Vector3 direction=default,bool volumetric=false)
        {
            if(blocked)
            {
                shieldHitAge=0;
                // The shield is repositioned and compressed in Tick below. Keep
                // the world-space contact until that transform is final, then
                // project it into the current shield space. This avoids a one
                // frame stale ripple when the incoming claw and camera are
                // moving at the same time.
                shieldHitWorld=position;shieldHitPending=true;
            }
            atmosphere.Hit(position,special,blocked,!volumetric,combo);
            Color color=blocked||special?Ice:hurt?Warm:new Color(1,.75f,.38f);
            contactWaveAge=0;contactWavePosition=position;contactWaveColor=blocked?Ice:hurt?Warm:special?new Color(.34f,.78f,1):new Color(1,.60f,.22f);
            contactWaveLife=special?.36f:blocked?.28f:combo?.25f:hurt?.23f:.20f;
            contactWaveSize=special?2.15f:blocked?1.22f:combo?1.08f:hurt?.92f:.82f;
            Burst(position,special?32:16,special?1.4f:.8f,hurt||(!blocked&&!special));
            bool punch=!special&&!blocked&&!hurt;
            if(punch)
            {
                strikeContact.Hit(position,direction==Vector3.zero?Vector3.right:direction,combo);
                // Ordinary contacts need the same one-frame arcade punctuation
                // as the reference cabinet. Keep the flash short and local so
                // it reads as fist-to-chest impact, not a second finisher.
                // Keep the directional contact card, but add a short local
                // expanding ring. The reference cabinet makes a punch read
                // as a contact event before the sparks disperse; the old
                // point flare had no silhouette against Golza's dark chest.
                // This is presentation-only and remains anchored to the
                // measured fist contact, so it cannot change damage timing.
                FlashAt(position,combo?1.12f:.82f,combo?.20f:.14f,
                    new Color(1,.72f,.26f,combo?.92f:.78f),true);
            }
            else FlashAt(position,special?2.4f:1.25f,special?.3f:.20f,color);
            if(!blocked&&!punch&&!volumetric)FlashAt(position,special?2.7f:1.7f,.38f,color,true);
            int rayCount=punch?(combo?9:6):special?(volumetric?6:14):blocked?9:7;
            for(int i=0;i<rayCount;i++)
            {
                var ray=hitRays[hitRayIndex++%hitRays.Length];
                float a=(i+.5f)*Mathf.PI*2/rayCount+(special?.18f:0);
                ray.Origin=position;
                // Keep the burst mostly in the camera-facing plane; depth-heavy
                // rays disappear inside the monster mesh on a three-quarter shot.
                ray.Direction=(Vector3.right*Mathf.Cos(a)+Vector3.up*Mathf.Sin(a)+Vector3.forward*.12f).normalized;
                // The cabinet read is a short, camera-facing starburst rather than
                // a tiny point spark.  A white core keeps it readable on a dark
                // monster silhouette while the tint still distinguishes block,
                // ordinary hit and the finisher.
                var tint=blocked?Ice:hurt?Warm:(special?new Color(.38f,.78f,1):new Color(1,.72f,.28f));
                ray.Color=Color.Lerp(tint,Color.white,.38f);
                ray.Age=0;ray.Life=punch?(combo?.20f:.15f):special?.34f:blocked?.27f:.22f;
                ray.Width=punch?(combo?.075f:.055f):special?.12f:blocked?.085f:.075f;
                ray.Line.SetPosition(0,position);ray.Line.SetPosition(1,position);ray.Line.enabled=true;
            }
            // Ordinary punches also need a small contact-to-ground cue on a TV;
            // blocks stay clean so the blue shield remains the readable answer.
            if(!blocked&&!hurt&&!volumetric)atmosphere.GroundBurst(position,Vector3.back,special);
            // The spark is bright, but its reflected light must remain near the
            // fist. A four-unit punch light also lit the head, belly and knees,
            // flattening the skin into gold during every ordinary strike.
            hitLight.range=punch?(combo?1.10f:.80f):4;
            hitLightPower=punch?(combo?2.1f:1.65f):3;
            hitLightDuration=punch?(combo?.18f:.16f):.22f;
            // Keep the reflected-light source just in front of the contact,
            // rather than buried in the chest where outward normals reject it.
            hitLight.transform.position=position-(punch?direction.normalized*.22f:Vector3.zero);
            hitLight.color=color;hitLightAge=0;
        }
        public void Combo(Vector3 position)
        {
            // A combo milestone is still one ordinary hit in Battle; this is a
            // presentation layer only. Extra sparks and ground dust give the
            // cabinet a visible cadence without hiding the next pose.
            Burst(position,20,.95f,true);
            atmosphere.GroundBurst(position,Vector3.back,true);
        }
        public void BeamHit(Vector3 position,Vector3 direction)=>beamImpact.Hit(position,direction);
        public void GroundBurst(Vector3 position,Vector3 direction,bool heavy=true,string cause="contact")
        {
            if(heavy)groundImpact.Burst(position,direction,cause);
            else atmosphere.GroundBurst(position,direction,false);
        }
        public void Clear()
        {
            atmosphere.Clear();
            strikeContact.Clear();
            groundImpact.Clear();
            beamImpact.Clear();
            beamCharge.Clear();
            ActiveSparkCount=0;beamBurstAge=0;previousEnemyAge=0;previousAttack=previousPunches=0;motionInitialized=false;hitRayIndex=0;
            foreach(var s in sparks)s.Line.enabled=false;
            foreach(var f in flashes){f.Age=10;f.Quad.gameObject.SetActive(false);}
            foreach(var ray in hitRays){ray.Age=10;ray.Line.enabled=false;}
            beam.Clear();
            warningRing.enabled=attackRing.enabled=shieldHex.enabled=contactWave.enabled=false;
            charge.gameObject.SetActive(false);shield.gameObject.SetActive(false);hitLightAge=shieldHitAge=10;shieldHitWorld=Vector3.zero;shieldHitPending=false;muzzleLight.intensity=hitLight.intensity=0;
        }
        static void GroundRing(LineRenderer line,Vector3 center,float radius,Color color)
        {
            for(int i=0;i<line.positionCount;i++)
            {
                float a=i*Mathf.PI*2/(line.positionCount-1);
                line.SetPosition(i,center+new Vector3(Mathf.Cos(a)*radius,.045f,Mathf.Sin(a)*radius));
            }
            line.startColor=color;line.endColor=new Color(color.r,color.g,color.b,0);
        }
        public void MotionDust(Battle state,AnimatedActor hero,AnimatedActor enemy,Vector3 axis)
        {
            if(state.Phase!=GamePhase.Battle||hero==null||enemy==null)return;
            if(!motionInitialized)
            {
                previousPunches=state.Punches;previousAttack=state.EnemyAttackCount;
                previousEnemyAge=state.EnemyAge;motionInitialized=true;return;
            }
            if(state.Punches>previousPunches)
                atmosphere.GroundBurst(hero.FootPosition(HeroKickMotion.Active(state)?state.Action!=HeroAction.LeftPunch:state.Action==HeroAction.LeftPunch),-axis,false);
            if(state.EnemyAttackCount!=previousAttack)previousEnemyAge=0;
            if(state.Enemy==EnemyPhase.Attack&&!MonsterRayMotion.Variant(state.EnemyAttackCount)&&!MonsterRockMotion.Variant(state.EnemyAttackCount))
            {
                bool left=MonsterStepMotion.LeadLeft(state.EnemyAttackCount);
                bool slam=MonsterSlamMotion.Variant(state.EnemyAttackCount);
                if(slam&&previousEnemyAge<MonsterSlamMotion.GroundSeconds&&state.EnemyAge>=MonsterSlamMotion.GroundSeconds)
                {
                    var start=(enemy.StrikeOrigin(HeroAction.LeftPunch)+enemy.HandPosition)*.5f;start.y=0;
                    var finish=hero.Root.position+axis*.55f;finish.y=0;
                    groundImpact.Burst(start,finish-start,"slam",Vector3.Distance(start,finish));
                }
                if(!slam&&previousEnemyAge<.08f&&state.EnemyAge>=.08f)
                    atmosphere.GroundBurst(enemy.FootPosition(!left),axis,true);
                if(!slam&&previousEnemyAge<MonsterStepMotion.LandingSeconds&&state.EnemyAge>=MonsterStepMotion.LandingSeconds)
                    GroundBurst(enemy.FootPosition(left),axis,true,"rush");
                if(!slam&&previousEnemyAge<MonsterStepMotion.ReturnLandingSeconds&&state.EnemyAge>=MonsterStepMotion.ReturnLandingSeconds)
                    atmosphere.GroundBurst(enemy.FootPosition(left),-axis,false);
            }
            previousEnemyAge=state.EnemyAge;previousAttack=state.EnemyAttackCount;previousPunches=state.Punches;
        }
        public void Tick(Battle state,Camera camera,float dt,Vector3 origin,Vector3 end,Vector3 shieldCenter,Vector3 axis,bool closeup,float focus,bool firing,Vector3 beamTarget,float closeupAge,Vector3 leftHand,Vector3 rightHand,Vector3? brace=null)
        {
            clock+=dt;
            if(state.Phase==GamePhase.Paused||state.Phase==GamePhase.Waiting){Clear();return;}
            atmosphere.Tick(camera,dt);
            strikeContact.Tick(camera,dt);
            groundImpact.Tick(camera,dt);
            beamImpact.Tick(dt);
            ActiveSparkCount=0;
            contactWaveAge+=dt;
            if(contactWaveAge<contactWaveLife)
            {
                float t=Mathf.Clamp01(contactWaveAge/Mathf.Max(.001f,contactWaveLife));
                float radius=contactWaveSize*(.18f+.82f*Mathf.SmoothStep(0,1,t));
                float alpha=(1-Mathf.SmoothStep(0,1,t))*(.82f-.18f*t);
                var right=camera.transform.right;var up=camera.transform.up;
                for(int i=0;i<contactWave.positionCount;i++)
                {
                    float a=i*Mathf.PI*2/(contactWave.positionCount-1);
                    contactWave.SetPosition(i,contactWavePosition-camera.transform.forward*.045f+
                        right*Mathf.Cos(a)*radius+up*Mathf.Sin(a)*radius);
                }
                Tint(contactWave,contactWaveColor,alpha);
                contactWave.widthMultiplier=Mathf.Lerp(.075f,.025f,t);
                contactWave.enabled=true;
            }
            else contactWave.enabled=false;
            foreach(var s in sparks)
            {
                if(!s.Line.enabled)continue;s.Age+=dt;if(s.Age>=s.Life){s.Line.enabled=false;continue;}
                s.Velocity+=Vector3.down*dt*5;s.Position+=s.Velocity*dt;
                if(s.Position.y<.035f){s.Position.y=.035f;s.Velocity.y=Mathf.Abs(s.Velocity.y)*.25f;s.Velocity*=.65f;}
                s.Line.SetPosition(0,s.Position);s.Line.SetPosition(1,s.Position-s.Velocity*.035f);
                s.Line.widthMultiplier=s.Width*(1-s.Age/s.Life);ActiveSparkCount++;
            }
            foreach(var f in flashes)
            {
                f.Age+=dt;if(f.Age>=f.Life){f.Quad.gameObject.SetActive(false);continue;}
                float p=f.Age/f.Life;f.Quad.position=f.Position;f.Quad.rotation=f.Ground?Quaternion.Euler(90,0,0):camera.transform.rotation;
                f.Quad.localScale=Vector3.one*f.Size*Mathf.Lerp(.55f,1.35f,p);
                var color=f.Material.color;color.a=f.Opacity*(1-p)*(1-p);f.Material.color=color;
            }
            foreach(var ray in hitRays)
            {
                if(!ray.Line.enabled)continue;
                ray.Age+=dt;if(ray.Age>=ray.Life){ray.Line.enabled=false;continue;}
                float p=ray.Age/ray.Life;
                float start=.025f+p*.16f,rayEnd=.36f+p*(ray.Life>.30f?1.65f:1.20f);
                ray.Line.SetPosition(0,ray.Origin+ray.Direction*start);
                ray.Line.SetPosition(1,ray.Origin+ray.Direction*rayEnd);
                float alpha=(1-p)*(1-p);var c=ray.Color;c.a=alpha;
                ray.Line.startColor=c;c.a=0;ray.Line.endColor=c;ray.Line.widthMultiplier=ray.Width*(1-.35f*p);
            }
            bool active=state.Phase==GamePhase.Battle;
            shieldHitAge+=dt;
            float compression=shieldHitAge<.32f?Mathf.Sin(Mathf.Clamp01(shieldHitAge/.32f)*Mathf.PI):0;
            shield.gameObject.SetActive(active&&state.Shield);
            shield.position=shieldCenter-axis*(compression*.12f);
            shield.rotation=Quaternion.LookRotation(axis,Vector3.up);
            shield.localScale=new Vector3(1.9f+compression*.12f,2.15f+compression*.07f,.38f-compression*.13f);
            shieldHex.enabled=active&&state.Shield;
            if(shieldHex.enabled)
            {
                // The sphere shader keeps the barrier volumetric, while this
                // short six-sided edge gives the living-room viewer the same
                // unmistakable silhouette as the reference cabinet. Draw it
                // from the final shield transform so impact compression and
                // camera-facing orientation stay in the same clock.
                float radiusX=(1.9f+compression*.12f)*.50f;
                float radiusY=(2.15f+compression*.07f)*.50f;
                Vector3 center=shield.position+axis*.205f;
                for(int i=0;i<shieldHex.positionCount;i++)
                {
                    float angle=Mathf.PI*.5f+i*Mathf.PI*2/(shieldHex.positionCount-1);
                    shieldHex.SetPosition(i,center+shield.right*Mathf.Cos(angle)*radiusX+shield.up*Mathf.Sin(angle)*radiusY);
                }
                float hitPulse=1-Mathf.SmoothStep(0,1,Mathf.Clamp01(shieldHitAge/.58f));
                var edge=new Color(.20f,.78f,1,.30f+.30f*hitPulse+.06f*Mathf.Sin(clock*2.4f));
                shieldHex.startColor=edge;edge.a=0;shieldHex.endColor=edge;
                shieldHex.widthMultiplier=.026f+.020f*hitPulse;
            }
            if(shieldHitPending)
            {
                shieldMaterial.SetVector("_HitPoint",shield.InverseTransformPoint(shieldHitWorld));
                shieldHitPending=false;
            }
            shieldMaterial.SetFloat("_Clock",clock);shieldMaterial.SetFloat("_HitAge",shieldHitAge);
            Vector3 enemyGround=end-Vector3.up*2.6f+axis*AnimatedActor.MonsterAdvance(state);
            bool warning=active&&state.Enemy==EnemyPhase.Windup;
            bool attack=active&&state.Enemy==EnemyPhase.Attack&&!MonsterRayMotion.Variant(state.EnemyAttackCount);
            warningRing.enabled=warning;attackRing.enabled=attack;
            if(warning)
            {
                float p=Mathf.Clamp01(state.EnemyAge/state.WarningDuration);
                GroundRing(warningRing,enemyGround,.45f+p*.85f,new Color(1,.38f,.10f,.16f+p*.28f));
            }
            if(attack)
            {
                float p=Mathf.Clamp01(state.EnemyAge/Battle.EnemyHitSeconds);
                GroundRing(attackRing,enemyGround,.25f+Mathf.SmoothStep(0,1,p)*1.65f,new Color(1,.58f,.20f,(1-p)*.62f));
            }
            beam.Tick(camera,origin,beamTarget,state.ActionAge,firing,clock);
            charge.gameObject.SetActive(firing);charge.position=origin-camera.transform.forward*.03f;charge.rotation=camera.transform.rotation;
            charge.localScale=Vector3.one*.8f;chargeMaterial.color=Color.Lerp(beamTint,beamAccent,.35f)*new Color(1,1,1,beam.Power*.72f);
            bool gathering=active&&state.Action==HeroAction.Beam&&(closeup||state.ActionAge<BeamStream.LaunchSeconds+.1f);
            beamCharge.Sample(gathering,closeup?closeupAge:BeamCloseup.Duration+state.ActionAge,origin,leftHand,rightHand,axis,chestMounted,brace);
            bool contacting=firing&&state.ActionAge>=Battle.BeamHitSeconds;
            if(contacting&&beam.Power>.15f)
            {
                beamBurstAge-=dt;
                if(beamBurstAge<=0)
                {
                    beamBurstAge=.12f;
                    Burst(beamTarget,4,.60f*beam.Power,false);
                }
            }
            else beamBurstAge=0;
            muzzleLight.transform.position=gathering?beamCharge.Center:origin;
            muzzleLight.intensity=Mathf.Max(firing?beam.Power*1.8f:0,beamCharge.Power*.9f);
            hitLightAge+=dt;
            hitLight.intensity=hitLightPower*(1-Mathf.SmoothStep(0,1,hitLightAge/hitLightDuration));
            if(contacting)
            {
                hitLight.transform.position=beamTarget-axis*.3f;hitLight.color=beamTint;
                hitLight.intensity=Mathf.Max(hitLight.intensity,beam.Power*(1.6f+.2f*Mathf.Sin(clock*19)));
            }
        }
    }
}
