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
        public bool HeroShot=>Closeup.Active&&Closeup.Focus>.18f;
        public float EnemyOpacity=>HeroShot?0:1;
        public readonly Vector3 HeroHome=new Vector3(-.955f,0,-.555f),EnemyHome=new Vector3(.955f,0,1.355f);
        public Vector3 BattleAxis => (EnemyHome-HeroHome).normalized;
        AnimatedActor hero,enemy;
        public void BindActors(AnimatedActor heroActor,AnimatedActor enemyActor){hero=heroActor;enemy=enemyActor;}
        public Vector3 BeamOrigin => hero!=null&&hero.IsRigged?hero.BeamOrigin:HeroHome+BattleAxis*.72f+Vector3.up*2.72f;
        Vector3 ShieldCenter => HeroHome+BattleAxis*.78f+Vector3.up*1.9f;
        readonly Transform backdrop;
        readonly CinematicCamera cinematic;
        readonly MonsterAttackEffects monsterEffects;
        readonly CombatVfx effects;
        readonly ArcadeStageFx arcade;
        public bool EnemySlashVisible => monsterEffects.SlashVisible;
        public bool BeamVisible => effects.BeamVisible;
        public int ActiveSparkCount => effects.ActiveSparkCount;
        readonly Vector3 cameraHome=new Vector3(-.25f,2.9f,-10.5f),lookAt=new Vector3(0,1.65f,.4f);
        float impact,impactAge=10,transformAge,celebrateAt,clock;
        readonly ImpactTiming hitTiming=new ImpactTiming();
        float framingFieldOfView=35;
        bool beamWasVisible;
        GamePhase previous;
        public GameWorld()
        {
            var root=new GameObject("City of light").transform;
            Camera=UnityEngine.Camera.main;
            if(!Camera)Camera=new GameObject("Main Camera").AddComponent<Camera>();
            Camera.tag="MainCamera";Camera.transform.position=cameraHome;Camera.transform.LookAt(lookAt);Camera.fieldOfView=39;
            Camera.clearFlags=CameraClearFlags.SolidColor;Camera.backgroundColor=new Color(.015f,.03f,.08f);Camera.farClipPlane=150;Camera.allowHDR=true;
            if(!Camera.GetComponent<ContactShadows>())Camera.gameObject.AddComponent<ContactShadows>();
            cinematic=Camera.GetComponent<CinematicCamera>();
            if(!cinematic)cinematic=Camera.gameObject.AddComponent<CinematicCamera>();
            if(!Object.FindFirstObjectByType<AudioListener>())Camera.gameObject.AddComponent<AudioListener>();
            var backMat=RuntimeResources.Own(root,new Material(Resources.Load<Shader>("Backdrop")));backMat.mainTexture=Resources.Load<Texture2D>("Art/CityDusk");
            backdrop=Primitive("City skyline",PrimitiveType.Quad,root,Vector3.zero,Vector3.one,backMat);
            backdrop.rotation=Camera.transform.rotation;
            var key=Directional(root,"Warm city key",new Color(1,.87f,.73f),1.05f,new Vector3(38,-38,0));
            key.shadows=LightShadows.Soft;key.shadowStrength=.78f;key.shadowBias=.025f;key.shadowNormalBias=.06f;
            Directional(root,"Sky fill",new Color(.35f,.56f,1),.38f,new Vector3(25,130,0));
            Directional(root,"Waterfront rim",new Color(.28f,.62f,1),.85f,new Vector3(18,155,0));
            QualitySettings.shadowDistance=30;QualitySettings.antiAliasing=4;QualitySettings.shadows=ShadowQuality.All;
            QualitySettings.shadowResolution=UnityEngine.ShadowResolution.High;QualitySettings.pixelLightCount=6;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.22f,.30f,.48f);RenderSettings.ambientEquatorColor=new Color(.12f,.17f,.26f);
            RenderSettings.ambientGroundColor=new Color(.07f,.085f,.12f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=14;RenderSettings.fogEndDistance=37;
            RenderSettings.fogColor=new Color(.065f,.15f,.26f);
            CityStage.Create(root);
            monsterEffects=new MonsterAttackEffects(root,EnemyHome,HeroHome);effects=new CombatVfx(root);arcade=new ArcadeStageFx(root,HeroHome);
        }
        static Light Directional(Transform parent,string name,Color color,float intensity,Vector3 angles)
        {var light=new GameObject(name).AddComponent<Light>();light.transform.SetParent(parent,false);light.type=LightType.Directional;light.color=color;light.intensity=intensity;light.transform.eulerAngles=angles;return light;}
        internal static Transform Primitive(string name,PrimitiveType type,Transform parent,Vector3 position,Vector3 scale,Material material)
        {
            var obj=GameObject.CreatePrimitive(type);obj.name=name;obj.transform.SetParent(parent,false);obj.transform.position=position;obj.transform.localScale=scale;
            obj.GetComponent<Renderer>().sharedMaterial=material;
            if(Application.isPlaying)Object.Destroy(obj.GetComponent<Collider>());else Object.DestroyImmediate(obj.GetComponent<Collider>());return obj.transform;
        }
        public float BattleDelta(float dt,Battle state) => Closeup.Active?0:hitTiming.Delta(dt,state.Phase);
        public void ResetPresentation()
        {Closeup.Cancel();hitTiming.Clear();arcade.Clear();effects.Clear();monsterEffects.Clear();impact=0;impactAge=10;beamWasVisible=BeamStarted=false;}
        public void Burst(Vector3 position,int count,float force=1,bool enemyEffect=false) => effects.Burst(position,count,force,enemyEffect);
        void Kick(float strength,bool special=false)
        {impact=strength;impactAge=0;hitTiming.Hit(special);}
        public void Hit(bool special,Battle state)
        {
            Kick(special?.10f:.045f,special);
            cinematic.Pulse(special?new Color(.25f,.68f,1):new Color(1,.48f,.16f),special?.72f:.22f);
            // Use the monster's position at this contact, including its own forward step.
            var position=special||hero==null?EnemyHome+Vector3.up*2.15f:hero.StrikeOrigin(state.Action);
            effects.Impact(position,special);
        }
        public void Cue(GameCue cue)
        {
            if(cue==GameCue.EnemyAttack)Burst(EnemyHome+Vector3.up*.1f,14,.5f,true);
            if(cue==GameCue.Block){effects.Impact(ShieldCenter,false,true);monsterEffects.Impact(true);Kick(.04f);}
            if(cue==GameCue.Hurt){effects.Impact(HeroHome+Vector3.up*2,false,false,true);monsterEffects.Impact(false);Kick(.055f);}
            if(cue==GameCue.Transform)Burst(HeroHome+Vector3.up*1.4f,30,.5f);
            if(cue==GameCue.Victory){effects.Impact(EnemyHome+Vector3.up*1.7f,true);Burst(EnemyHome+Vector3.up*2.2f,48,1.3f);}
            if(cue==GameCue.Beam)
            {
                Closeup.Begin();Burst(BeamOrigin,12,.4f);
                if(Debug.isDebugBuild)Debug.Log($"[BeamCloseup] begin duration={BeamCloseup.Duration:F2}");
            }
        }
        public void Tick(Battle state,float dt,float time)
        {
            clock+=dt;hitTiming.Tick(dt,state.Phase);arcade.Tick(state,dt,clock);
            bool wasCloseup=Closeup.Active;Closeup.Tick(dt,state,Showcase);
            if(wasCloseup&&!Closeup.Active&&Debug.isDebugBuild)Debug.Log($"[BeamCloseup] end phase={state.Phase} action={state.Action}");
            float focus=Closeup.Focus;
            bool battleView=state.Phase==GamePhase.Battle||state.Phase==GamePhase.Paused||state.Phase==GamePhase.Victory;
            float fieldOfView=Showcase||state.Phase==GamePhase.Victory?32:battleView?(state.Action==HeroAction.Beam?30:31):37;
            framingFieldOfView=Mathf.Lerp(framingFieldOfView,fieldOfView,dt*4);
            Camera.fieldOfView=Mathf.Lerp(framingFieldOfView,14,focus);
            float h=160*Mathf.Tan(27*Mathf.Deg2Rad*.5f);
            float aspect=backdrop.GetComponent<Renderer>().sharedMaterial.mainTexture.width/(float)backdrop.GetComponent<Renderer>().sharedMaterial.mainTexture.height;
            float scale=Mathf.Max(1,Camera.aspect/aspect)*1.5f;
            backdrop.localScale=new Vector3(h*aspect*scale,h*scale,1);
            var backgroundRotation=Quaternion.LookRotation(lookAt-cameraHome);
            backdrop.rotation=backgroundRotation;
            backdrop.position=cameraHome+backgroundRotation*new Vector3(0,h*(scale-1)*.5f,80);
            impactAge+=dt;
            if(state.Phase==GamePhase.Paused||state.Phase==GamePhase.Waiting){impact=0;effects.Clear();}
            float kick=impact*Mathf.Exp(-impactAge*14)*(1-focus);
            // One damped recoil, with a restrained camera displacement for a young player.
            Camera.transform.position=cameraHome+new Vector3(Mathf.Sin(impactAge*47)*kick,Mathf.Sin(impactAge*31)*kick*.35f,-kick*.4f);
            Vector3 target=lookAt;
            if(!Showcase&&state.Phase==GamePhase.Battle&&!Closeup.Active)
            {
                float rush=state.Enemy==EnemyPhase.Attack?Mathf.Sin(Mathf.Clamp01(state.EnemyAge/Battle.EnemyAttackSeconds)*Mathf.PI):0;
                Camera.transform.position+=new Vector3(-.22f*rush,-.13f*rush,.28f*rush);
                target+=new Vector3(-.1f*rush,0,0);

                // A short arcade lens move makes each exchange readable on a TV.
                // It is intentionally small and uses the same deterministic battle clock
                // as the actors, so it never changes gesture timing or gameplay state.
                bool heroStrike=state.Action==HeroAction.LeftPunch||state.Action==HeroAction.RightPunch;
                float strike=Mathf.Sin(Mathf.Clamp01(state.ActionAge/.42f)*Mathf.PI);
                if(heroStrike)
                {
                    float side=state.Action==HeroAction.LeftPunch?-1:1;
                    Camera.transform.position+=BattleAxis*(.16f*strike)+Camera.transform.right*(side*.06f*strike);
                    target+=BattleAxis*(.10f*strike)+Vector3.up*(.035f*strike);
                }
                if(rush>.01f)
                {
                    Camera.transform.position+=BattleAxis*(.11f*rush);
                    target+=BattleAxis*(.08f*rush);
                }
            }
            if(!Showcase&&state.Phase==GamePhase.Transforming)
            {
                float t=Mathf.Clamp01(arcade.PhaseAge/2.2f),sweep=Mathf.Sin(t*Mathf.PI);
                Camera.transform.position+=new Vector3(-sweep*.65f,-sweep*.6f,sweep*.2f);
                target=Vector3.Lerp(lookAt,HeroHome+Vector3.up*1.65f,sweep*.35f);
            }
            if(!Showcase&&state.Phase==GamePhase.Victory)
                target=Vector3.Lerp(lookAt,HeroHome+Vector3.up*1.65f,Mathf.SmoothStep(0,1,(arcade.PhaseAge-1)/3)*.6f);
            Camera.transform.LookAt(Vector3.Lerp(target,HeroHome+BattleAxis*.2f+Vector3.up*2.60f,focus));
            if(HeroShot)
            {
                // Cut to a front three-quarter lens. Moving the arena lens through a city block would occlude the actor.
                Camera.transform.position=HeroHome+new Vector3(3.8f,2.82f,.9f);
                Camera.transform.LookAt(HeroHome+BattleAxis*.26f+Vector3.up*2.93f);
                Camera.fieldOfView=32;
                // Reproject the distant city matte for this dedicated lens; the foreground stays three-dimensional.
                backdrop.rotation=Camera.transform.rotation;
                backdrop.position=Camera.transform.position+Camera.transform.rotation*new Vector3(0,h*(scale-1)*.5f,80);
            }
            bool active=state.Phase==GamePhase.Battle;
            monsterEffects.Tick(state,Camera,dt,enemy!=null&&enemy.IsRigged?(Vector3?)enemy.HandPosition:null);
            bool firing=active&&!Closeup.Active&&state.Action==HeroAction.Beam&&state.ActionAge>.28f;
            BeamStarted=firing&&!beamWasVisible;beamWasVisible=firing;
            if(BeamStarted&&Debug.isDebugBuild)Debug.Log($"[BeamCloseup] beam-visible actionAge={state.ActionAge:F2}");
            effects.Tick(state,Camera,dt,BeamOrigin,EnemyHome+Vector3.up*2.6f,ShieldCenter,BattleAxis,Closeup.Active,focus,firing);
            if(state.Phase!=previous){transformAge=0;previous=state.Phase;}
            transformAge+=dt;
            if(state.Phase==GamePhase.Transforming&&clock>celebrateAt)
            {celebrateAt=clock+.09f;Burst(HeroHome+new Vector3(Random.Range(-.55f,.55f),transformAge%1*3.0f,Random.Range(-.3f,.3f)),3,.25f);}
            if(state.Phase==GamePhase.Victory&&clock>celebrateAt&&transformAge<3)
            {celebrateAt=clock+.25f;Burst(HeroHome+new Vector3(Random.Range(-1.3f,1.3f),2.8f,Random.Range(-.7f,.7f)),4,.45f);}
        }
    }
}
