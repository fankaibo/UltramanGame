using UnityEngine;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    public sealed class GameWorld
    {
        public readonly Camera Camera;
        public bool Showcase;
        public readonly BeamCloseup Closeup=new BeamCloseup();
        public bool BeamStarted { get; private set; }
        // Equal X/Z separation stages the duel at 45 degrees: Tiga near-left, Golza far-right.
        public readonly Vector3 HeroHome=new Vector3(-1.65f,0,-1.35f),EnemyHome=new Vector3(1.65f,0,1.95f);
        public Vector3 BattleAxis => (EnemyHome-HeroHome).normalized;
        public Vector3 BeamOrigin => HeroHome+BattleAxis*.72f+Vector3.up*2.72f;
        Vector3 ShieldCenter => HeroHome+BattleAxis*.78f+Vector3.up*1.9f;
        readonly Transform backdrop,beam,beamCore,shield,transformLight;
        readonly LineRenderer chargeRing,shieldRing,beamChargeRing;
        readonly Transform beamCharge;
        readonly Material cyan,gold,enemyGlow;
        readonly MonsterAttackEffects monsterEffects;
        public bool EnemySlashVisible => monsterEffects.SlashVisible;
        readonly Vector3 cameraHome=new Vector3(0,4.2f,-12),lookAt=new Vector3(0,1.22f,.4f);
        struct Spark { public Transform Object;public Vector3 Velocity;public float Life,Total,Size; }
        readonly Spark[] sparks=new Spark[48];
        int sparkIndex;
        float impact,transformAge,celebrateAt;
        float framingFieldOfView=35;
        bool beamWasVisible;
        GamePhase previous;
        public GameWorld()
        {
            var root=new GameObject("City of light").transform;
            Camera=UnityEngine.Camera.main;
            if(!Camera) Camera=new GameObject("Main Camera").AddComponent<Camera>();
            Camera.tag="MainCamera";Camera.transform.position=cameraHome;Camera.transform.LookAt(lookAt);Camera.fieldOfView=39;
            Camera.clearFlags=CameraClearFlags.SolidColor;Camera.backgroundColor=new Color(.015f,.03f,.08f);Camera.farClipPlane=150;
            if(!Object.FindFirstObjectByType<AudioListener>()) Camera.gameObject.AddComponent<AudioListener>();
            var backMat=new Material(Resources.Load<Shader>("Backdrop"));backMat.mainTexture=Resources.Load<Texture2D>("Art/CityDusk");
            backdrop=Primitive(root,PrimitiveType.Quad,Vector3.zero,Vector3.one,backMat);
            backdrop.SetParent(Camera.transform,false);backdrop.localPosition=new Vector3(0,0,80);
            var sun=new GameObject("Warm key light").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.15f;sun.color=new Color(1,.85f,.68f);sun.transform.rotation=Quaternion.Euler(35,-35,0);sun.shadows=LightShadows.Soft;sun.shadowStrength=.7f;sun.shadowBias=.04f;sun.shadowNormalBias=.12f;QualitySettings.shadowDistance=25;QualitySettings.antiAliasing=4;
            var fill=new GameObject("Cool rim light").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.75f;fill.color=new Color(.44f,.68f,1);fill.transform.rotation=Quaternion.Euler(25,135,0);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.24f,.28f,.36f);RenderSettings.fog=false;
            cyan=Glow(new Color(.14f,.77f,1,.55f));gold=Glow(new Color(1,.72f,.27f,.65f));
            enemyGlow=Glow(new Color(1,.43f,.13f,.6f));
            monsterEffects=new MonsterAttackEffects(root,EnemyHome,HeroHome);
            shield=Primitive(root,PrimitiveType.Sphere,ShieldCenter,new Vector3(1.75f,2.0f,.2f),Glow(new Color(.13f,.64f,1,.11f)));
            shield.rotation=Quaternion.LookRotation(BattleAxis,Vector3.up);
            shieldRing=Ring(root,shield.position,.86f,.03f,cyan);shieldRing.transform.rotation=Quaternion.FromToRotation(Vector3.up,BattleAxis);
            beam=Primitive(root,PrimitiveType.Cylinder,Vector3.zero,Vector3.one,cyan);
            beamCore=Primitive(root,PrimitiveType.Cylinder,Vector3.zero,Vector3.one,Glow(new Color(.8f,.94f,1,.8f)));
            beamCharge=Primitive(root,PrimitiveType.Sphere,BeamOrigin,Vector3.one*.16f,Glow(new Color(.65f,.9f,1,.5f)));
            beamChargeRing=Ring(root,BeamOrigin,.36f,.018f,cyan);
            chargeRing=Ring(root,HeroHome+Vector3.up*.025f,1,.04f,gold);
            transformLight=Primitive(root,PrimitiveType.Cylinder,HeroHome+Vector3.up*1.5f,new Vector3(.85f,1.5f,.85f),Glow(new Color(.45f,.66f,1,.11f)));
            for(int i=0;i<sparks.Length;i++)
            {
                var obj=Primitive(root,PrimitiveType.Sphere,Vector3.zero,Vector3.one,i%3==0?gold:cyan);
                obj.gameObject.SetActive(false);sparks[i].Object=obj;
            }
        }
        static Material Glow(Color color) { return new Material(Resources.Load<Shader>("SoftGlow")) { color=color }; }
        static Transform Primitive(Transform parent,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
        {
            var obj=GameObject.CreatePrimitive(type);obj.transform.SetParent(parent,false);obj.transform.position=position;obj.transform.localScale=scale;
            obj.GetComponent<Renderer>().sharedMaterial=material;if(Application.isPlaying)Object.Destroy(obj.GetComponent<Collider>());else Object.DestroyImmediate(obj.GetComponent<Collider>());return obj.transform;
        }
        static LineRenderer Ring(Transform parent,Vector3 position,float radius,float width,Material material)
        {
            var obj=new GameObject("Light ring");obj.transform.SetParent(parent,false);obj.transform.position=position;
            var line=obj.AddComponent<LineRenderer>();line.useWorldSpace=false;line.loop=true;line.positionCount=96;line.widthMultiplier=width;line.sharedMaterial=material;
            for(int i=0;i<96;i++) { float a=i*2*Mathf.PI/96;line.SetPosition(i,new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius)); }
            return line;
        }
        public void Burst(Vector3 position,int count,float force=1,bool enemyEffect=false)
        {
            for(int i=0;i<count;i++)
            {
                int index=sparkIndex++%sparks.Length;var s=sparks[index];
                s.Object.position=position;s.Object.gameObject.SetActive(true);s.Total=s.Life=Random.Range(.3f,.75f);
                s.Object.GetComponent<Renderer>().sharedMaterial=enemyEffect?enemyGlow:index%3==0?gold:cyan;
                s.Velocity=Random.onUnitSphere*Random.Range(.6f,2.2f)*force+Vector3.up*.4f;s.Size=Random.Range(.035f,.10f);sparks[index]=s;
            }
        }
        public void Hit(bool special,Battle state)
        { impact=special?.32f:.17f;Burst(EnemyHome-BattleAxis*AnimatedActor.MonsterAdvance(state)+Vector3.up*1.8f,special?24:10,special?1.8f:1); }
        public void Cue(GameCue cue)
        {
            if(cue==GameCue.EnemyAttack)Burst(EnemyHome+Vector3.up*.2f,12,.8f,true);
            if(cue==GameCue.Block) {Burst(ShieldCenter,16,1.2f);monsterEffects.Impact(true);impact=.12f;}
            if(cue==GameCue.Hurt) {Burst(HeroHome+Vector3.up*2,18,1,true);monsterEffects.Impact(false);impact=.22f;}
            if(cue==GameCue.Transform) Burst(HeroHome+Vector3.up*1.4f,20,.6f);
            if(cue==GameCue.Beam)
            {
                Closeup.Begin();Burst(BeamOrigin,14,.6f);
                if(Debug.isDebugBuild)Debug.Log($"[BeamCloseup] begin duration={BeamCloseup.Duration:F2}");
            }
        }
        static void Ray(Transform obj,Vector3 start,Vector3 end,float radius)
        { obj.position=(start+end)/2;obj.rotation=Quaternion.FromToRotation(Vector3.up,end-start);obj.localScale=new Vector3(radius,Vector3.Distance(start,end)/2,radius); }
        public void Tick(Battle state,float dt,float time)
        {
            bool wasCloseup=Closeup.Active;
            Closeup.Tick(dt,state,Showcase);
            if(wasCloseup&&!Closeup.Active&&Debug.isDebugBuild)
                Debug.Log($"[BeamCloseup] end phase={state.Phase} action={state.Action}");
            float focus=Closeup.Focus;
            bool battleView=state.Phase==GamePhase.Battle||state.Phase==GamePhase.Paused||state.Phase==GamePhase.Victory;
            float fieldOfView=Showcase||state.Phase==GamePhase.Victory?29:battleView?(state.Action==HeroAction.Beam?26:27):35;
            framingFieldOfView=Mathf.Lerp(framingFieldOfView,fieldOfView,dt*4);
            Camera.fieldOfView=Mathf.Lerp(framingFieldOfView,14,focus);
            float h=160*Mathf.Tan(Camera.fieldOfView*Mathf.Deg2Rad*.5f);
            // Frame the existing plaza beneath both actors; preserve aspect and anchor its bottom edge.
            float aspect=backdrop.GetComponent<Renderer>().sharedMaterial.mainTexture.width/(float)backdrop.GetComponent<Renderer>().sharedMaterial.mainTexture.height;
            float scale=Mathf.Max(1,Camera.aspect/aspect)*1.5f;
            backdrop.localScale=new Vector3(h*aspect*scale,h*scale,1);
            backdrop.localPosition=new Vector3(0,h*(scale-1)*.5f,80);
            impact=Mathf.Max(0,impact-dt);
            Camera.transform.position=cameraHome+new Vector3(Mathf.Sin(time*65)*impact*.035f*(1-focus),0,0);
            Camera.transform.LookAt(Vector3.Lerp(lookAt,HeroHome+BattleAxis*.2f+Vector3.up*2.48f,focus));
            bool active=state.Phase==GamePhase.Battle;
            monsterEffects.Tick(state,Camera,dt);
            shield.gameObject.SetActive(active&&state.Shield);shieldRing.gameObject.SetActive(active&&state.Shield);
            shieldRing.transform.Rotate(0,dt*30,0,Space.Self);
            bool firing=active&&!Closeup.Active&&state.Action==HeroAction.Beam&&state.ActionAge>.28f;
            BeamStarted=firing&&!beamWasVisible;beamWasVisible=firing;
            if(BeamStarted&&Debug.isDebugBuild)Debug.Log($"[BeamCloseup] beam-visible actionAge={state.ActionAge:F2}");
            beam.gameObject.SetActive(firing);beamCore.gameObject.SetActive(firing);
            beamCharge.gameObject.SetActive(Closeup.Active);beamChargeRing.gameObject.SetActive(Closeup.Active);
            if(Closeup.Active)
            {
                beamCharge.localScale=Vector3.one*(.15f+focus*.12f);
                beamChargeRing.transform.rotation=Quaternion.FromToRotation(Vector3.up,Camera.transform.forward);
                beamChargeRing.transform.localScale=Vector3.one*(.65f+focus*.45f+Mathf.Sin(time*5)*.06f);
            }
            if(firing)
            {
                Vector3 start=BeamOrigin,end=EnemyHome+Vector3.up*2.15f;
                Ray(beam,start,end,.27f+Mathf.Sin(time*25)*.025f);Ray(beamCore,start,end,.09f);
            }
            bool transforming=state.Phase==GamePhase.Transforming;
            if(state.Phase!=previous) { transformAge=0;previous=state.Phase; }
            transformAge+=dt;
            transformLight.gameObject.SetActive(transforming);chargeRing.gameObject.SetActive(transforming||state.Energy>=Battle.MaxEnergy);
            if(transforming)
            {
                float pulse=1+Mathf.Sin(transformAge*6)*.09f;
                transformLight.localScale=new Vector3(pulse,1.6f,pulse);
                chargeRing.transform.position=HeroHome+Vector3.up*(.06f+(transformAge%1)*2.8f);
                chargeRing.transform.localScale=Vector3.one*(1+transformAge*.22f);
            }
            else { chargeRing.transform.position=HeroHome+Vector3.up*.025f;chargeRing.transform.localScale=Vector3.one*(1.05f+Mathf.Sin(time*3)*.06f); }
            if(state.Phase==GamePhase.Victory&&time>celebrateAt&&transformAge<5)
            { celebrateAt=time+.2f;Burst(HeroHome+new Vector3(Random.Range(-1.3f,1.3f),2.8f,Random.Range(-.7f,.7f)),3,.6f); }
            for(int i=0;i<sparks.Length;i++)
            {
                var s=sparks[i];if(s.Life<=0)continue;
                s.Life-=dt;s.Velocity+=Vector3.down*dt*.55f;s.Object.position+=s.Velocity*dt;
                s.Object.localScale=Vector3.one*s.Size*Mathf.Clamp01(s.Life/s.Total);
                if(s.Life<=0)s.Object.gameObject.SetActive(false);sparks[i]=s;
            }
        }
    }
}
