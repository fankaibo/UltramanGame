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
        readonly LineRenderer[] rays=new LineRenderer[7],orbits=new LineRenderer[3];
        readonly LineRenderer warningRing,attackRing;
        readonly Light muzzleLight,hitLight;
        readonly Material lineMaterial;
        readonly ImpactAtmosphere atmosphere;
        int sparkIndex,flashIndex,hitRayIndex;
        float hitLightAge=10,clock,beamBurstAge;
        float previousEnemyAge;
        int previousAttack,previousPunches;
        bool motionInitialized;
        public int ActiveSparkCount {get;private set;}
        public bool BeamVisible => rays[0].enabled;
        static readonly Color Ice=new Color(.15f,.65f,1),Warm=new Color(1,.48f,.12f);
        public CombatVfx(Transform parent)
        {
            atmosphere=new ImpactAtmosphere(parent);
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
            for(int i=0;i<rays.Length;i++)rays[i]=Line(parent,"Zeperion beam layer",32,i==0?.66f:i==1?.23f:.028f);
            for(int i=0;i<orbits.Length;i++)orbits[i]=Line(parent,"Charging arc",48,.016f);
            warningRing=Line(parent,"Monster warning ground ring",64,.035f);
            attackRing=Line(parent,"Monster attack ground ring",64,.05f);
            muzzleLight=Point(parent,"Energy spill",Ice,5);hitLight=Point(parent,"Impact spill",Warm,4);
        }
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
        public void Impact(Vector3 position,bool special,bool blocked=false,bool hurt=false)
        {
            atmosphere.Hit(position,special,blocked);
            Color color=blocked?Ice:hurt?Warm:new Color(1,.75f,.38f);
            Burst(position,special?32:16,special?1.4f:.8f,hurt||!blocked);
            FlashAt(position,special?2.4f:1.25f,special?.3f:.20f,color);
            FlashAt(position,special?2.7f:1.7f,.38f,color,true);
            int rayCount=special?14:blocked?9:7;
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
                ray.Age=0;ray.Life=special?.34f:blocked?.27f:.22f;ray.Width=special?.12f:blocked?.085f:.075f;
                ray.Line.SetPosition(0,position);ray.Line.SetPosition(1,position);ray.Line.enabled=true;
            }
            if(special||hurt)atmosphere.GroundBurst(position,Vector3.back,true);
            hitLight.transform.position=position;hitLight.color=color;hitLightAge=0;
        }
        public void Clear()
        {
            atmosphere.Clear();
            ActiveSparkCount=0;beamBurstAge=0;previousEnemyAge=0;previousAttack=previousPunches=0;motionInitialized=false;hitRayIndex=0;
            foreach(var s in sparks)s.Line.enabled=false;
            foreach(var f in flashes){f.Age=10;f.Quad.gameObject.SetActive(false);}
            foreach(var ray in hitRays){ray.Age=10;ray.Line.enabled=false;}
            foreach(var r in rays)r.enabled=false;
            foreach(var r in orbits)r.enabled=false;
            warningRing.enabled=attackRing.enabled=false;
            charge.gameObject.SetActive(false);shield.gameObject.SetActive(false);hitLightAge=10;muzzleLight.intensity=hitLight.intensity=0;
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
                atmosphere.GroundBurst(hero.FootPosition(state.Action==HeroAction.LeftPunch),-axis,false);
            if(state.EnemyAttackCount!=previousAttack)previousEnemyAge=0;
            if(state.Enemy==EnemyPhase.Attack)
            {
                bool left=state.EnemyAttackCount%2==0;
                if(previousEnemyAge<.08f&&state.EnemyAge>=.08f)
                    atmosphere.GroundBurst(enemy.FootPosition(!left),axis,true);
                if(previousEnemyAge<.36f&&state.EnemyAge>=.36f)
                    atmosphere.GroundBurst(enemy.FootPosition(left),axis,true);
                if(previousEnemyAge<.8f&&state.EnemyAge>=.8f)
                    atmosphere.GroundBurst(enemy.FootPosition(!left),-axis,false);
            }
            previousEnemyAge=state.EnemyAge;previousAttack=state.EnemyAttackCount;previousPunches=state.Punches;
        }
        public void Tick(Battle state,Camera camera,float dt,Vector3 origin,Vector3 end,Vector3 shieldCenter,Vector3 axis,bool closeup,float focus,bool firing)
        {
            clock+=dt;
            if(state.Phase==GamePhase.Paused||state.Phase==GamePhase.Waiting){Clear();return;}
            atmosphere.Tick(camera,dt);
            ActiveSparkCount=0;
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
            shield.gameObject.SetActive(active&&state.Shield);shield.position=shieldCenter;shield.rotation=Quaternion.LookRotation(axis,Vector3.up);shieldMaterial.SetFloat("_Clock",clock);
            Vector3 enemyGround=end-Vector3.up*2.6f+axis*AnimatedActor.MonsterAdvance(state);
            bool warning=active&&state.Enemy==EnemyPhase.Windup;
            bool attack=active&&state.Enemy==EnemyPhase.Attack;
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
            bool charging=closeup||firing;charge.gameObject.SetActive(charging);charge.position=origin-camera.transform.forward*.03f;charge.rotation=camera.transform.rotation;
            charge.localScale=Vector3.one*(firing?1.4f:.45f+focus*.85f);chargeMaterial.color=new Color(.5f,.8f,1,.85f);
            for(int i=0;i<orbits.Length;i++)
            {
                var line=orbits[i];line.enabled=closeup;
                if(!closeup)continue;float radius=.18f+focus*.18f+i*.065f;
                for(int j=0;j<line.positionCount;j++)
                {float a=j/(float)(line.positionCount-1)*Mathf.PI*1.45f+clock*(i%2==0?5:-4)+i*2;
                 line.SetPosition(j,origin+camera.transform.right*Mathf.Cos(a)*radius+camera.transform.up*Mathf.Sin(a)*radius*.55f+axis*Mathf.Sin(a)*radius*.5f);}
                Tint(line,Ice,.55f);
            }
            for(int i=0;i<rays.Length;i++)
            {
                var line=rays[i];line.enabled=firing;if(!firing)continue;
                for(int j=0;j<line.positionCount;j++)
                {
                    float t=j/(float)(line.positionCount-1);Vector3 p=Vector3.Lerp(origin,end,t);
                    if(i>1){float a=t*12-clock*16+i*1.7f;float radius=.065f+Mathf.Sin(t*9+clock*7+i)*.02f;
                        p+=camera.transform.right*Mathf.Sin(a)*radius+camera.transform.up*Mathf.Cos(a*1.1f)*radius;}
                    line.SetPosition(j,p);
                }
                line.startColor=line.endColor=i==0?new Color(.08f,.42f,1,.22f):i==1?new Color(.8f,.96f,1,.95f):new Color(.32f,.72f,1,.65f);
            }
            if(firing)
            {
                beamBurstAge-=dt;
                if(beamBurstAge<=0)
                {
                    beamBurstAge=.075f;
                    Burst(end,5,.78f,false);
                    FlashAt(end,1.65f,.14f,new Color(.3f,.7f,1));
                    FlashAt(end,1.85f,.20f,new Color(.35f,.82f,1),true);
                }
            }
            else beamBurstAge=0;
            muzzleLight.transform.position=origin;muzzleLight.intensity=firing?2.1f:closeup?focus*.65f:0;
            hitLightAge+=dt;hitLight.intensity=Mathf.Max(0,1-hitLightAge/.22f)*3;
        }
    }
}
