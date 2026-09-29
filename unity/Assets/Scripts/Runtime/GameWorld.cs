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
        public bool MonsterLanded { get; private set; }
        public bool MonsterStaggerLanded {get;private set;}
        public bool MonsterDissolving {get;private set;}
        int dissolveStarts;
        int staggerLandings,launchLandings,beamLandings;
        public float VictoryAge => previous==GamePhase.Victory?arcade.PhaseAge:0;
        public float EntranceAge {get;private set;}
        public float ThreatFocus {get;private set;}
        readonly EnemyExchangeMotion exchangeCamera=new EnemyExchangeMotion();
        public float ExchangeFocus=>exchangeCamera.Focus;
        readonly ComboCameraMotion comboCamera=new ComboCameraMotion();
        public float ComboFocus=>comboCamera.Focus;
        public float ComboCameraAge=>comboCamera.Age;
        public bool TransformationCloseup {get;private set;}
        float lastEntranceAge;
        public bool HeroShot=>Closeup.Active&&Closeup.Focus>.18f;
        public float EnemyOpacity=>HeroShot||TransformationCloseup?0:1;
        public readonly Vector3 HeroHome=new Vector3(-.955f,0,-.555f),EnemyHome=new Vector3(.955f,0,1.355f);
        public Vector3 BattleAxis => (EnemyHome-HeroHome).normalized;
        AnimatedActor hero,enemy;
        public void BindActors(AnimatedActor heroActor,AnimatedActor enemyActor){hero=heroActor;enemy=enemyActor;hero?.SetOpponent(enemy);staggerLandings=enemy?.StaggerLandings??0;launchLandings=enemy?.LaunchLandings??0;beamLandings=enemy?.BeamLandings??0;dissolveStarts=enemy?.DissolveStarts??0;}
        public Vector3 BeamOrigin => hero!=null&&hero.IsRigged?hero.BeamOrigin:HeroHome+BattleAxis*.72f+Vector3.up*2.72f;
        public Vector3 BeamTarget => enemy!=null?enemy.BeamSurfaceContact:EnemyHome+Vector3.up*2.48f-BattleAxis*.33f;
        Vector3 ShieldCenter => HeroHome+BattleAxis*.78f+Vector3.up*1.9f;
        readonly Transform backdrop;
        readonly Material backdropMaterial;
        readonly CinematicCamera cinematic;
        readonly MonsterAttackEffects monsterEffects;
        readonly MonsterRay monsterRay;
        public bool MonsterRayVisible=>monsterRay.Visible;
        public float MonsterRayPower=>monsterRay.Power;
        public Vector3 MonsterRayOrigin=>monsterRay.Origin;
        public Vector3 MonsterRayTip=>monsterRay.Tip;
        public int MonsterRayLaunches=>monsterRay.Launches;
        readonly CombatVfx effects;
        readonly StrikeTrails strikeTrails;
        readonly ArcadeStageFx arcade;
        readonly VolcanoStage volcano;
        readonly Light heroRim;
        readonly Light monsterRim;
        public bool EnemySlashVisible => monsterEffects.SlashVisible;
        public bool BeamVisible => effects.BeamVisible;
        public bool BeamImpactVisible=>effects.BeamImpactVisible;
        public float BeamImpactAge=>effects.BeamImpactAge;
        public int BeamImpactCount=>effects.BeamImpactCount;
        public bool ChargeVisible=>effects.ChargeVisible;
        public float ChargePower=>effects.ChargePower;
        public Vector3 ChargeCenter=>effects.ChargeCenter;
        public int ActiveSparkCount => effects.ActiveSparkCount;
        public int ActiveContactCount=>effects.ActiveContactCount;
        public int ActiveGroundStones=>effects.ActiveGroundStones;
        public int ActiveGroundDust=>effects.ActiveGroundDust;
        public float GroundImpactAge=>effects.GroundImpactAge;
        public int GroundContactCount=>effects.GroundContactCount;
        public string GroundContactCause=>effects.GroundContactCause;
        public bool HeroTrailVisible => strikeTrails.HeroVisible;
        public bool MonsterTrailVisible => strikeTrails.MonsterVisible;
        readonly Vector3 cameraHome=new Vector3(-.25f,2.9f,-10.5f),lookAt=new Vector3(0,1.65f,.4f);
        float impact,impactAge=10,transformAge,celebrateAt,clock;
        readonly ImpactTiming hitTiming=new ImpactTiming();
        float framingFieldOfView=35;
        bool beamWasVisible,landingPending;
        Battle threatBattle;
        GamePhase previous;
        public GameWorld()
        {
            var root=new GameObject("Volcanic night arena").transform;
            Camera=UnityEngine.Camera.main;
            if(!Camera)Camera=new GameObject("Main Camera").AddComponent<Camera>();
            Camera.tag="MainCamera";Camera.transform.position=cameraHome;Camera.transform.LookAt(lookAt);Camera.fieldOfView=39;
            Camera.clearFlags=CameraClearFlags.SolidColor;Camera.backgroundColor=new Color(.02f,.006f,.018f);Camera.farClipPlane=150;Camera.allowHDR=true;
            if(!Camera.GetComponent<ContactShadows>())Camera.gameObject.AddComponent<ContactShadows>();
            cinematic=Camera.GetComponent<CinematicCamera>();
            if(!cinematic)cinematic=Camera.gameObject.AddComponent<CinematicCamera>();
            if(!Object.FindFirstObjectByType<AudioListener>())Camera.gameObject.AddComponent<AudioListener>();
            backdropMaterial=RuntimeResources.Own(root,new Material(Resources.Load<Shader>("Backdrop")));
            backdropMaterial.mainTexture=Resources.Load<Texture2D>("Art/VolcanoFujiNight");
            backdropMaterial.SetFloat("_Clock",0);
            var backMat=backdropMaterial;
            backdrop=Primitive("Realistic Mount Fuji night backdrop",PrimitiveType.Quad,root,Vector3.zero,Vector3.one,backMat);
            backdrop.rotation=Camera.transform.rotation;
            var key=Directional(root,"Volcanic moon key",new Color(.80f,.86f,1),.95f,new Vector3(38,-38,0));
            key.shadows=LightShadows.Soft;key.shadowStrength=.78f;key.shadowBias=.025f;key.shadowNormalBias=.06f;
            Directional(root,"Ash sky fill",new Color(.18f,.24f,.52f),.28f,new Vector3(25,130,0));
            Directional(root,"Lava rim",new Color(1,.28f,.10f),.32f,new Vector3(18,155,0));
            var arcadeFill=Point(root,"Arcade character fill",new Color(.65f,.74f,.92f),9);
            arcadeFill.transform.position=new Vector3(-1.4f,3.8f,-3.2f);arcadeFill.intensity=1.4f;
            arcadeFill.shadows=LightShadows.None;
            // A cabinet uses colored edge light to keep the fighters readable
            // against a dark stage. These two small, shadowless sources breathe
            // with the combat clocks instead of flattening the Fuji backdrop.
            heroRim=Point(root,"Hero blue rim",new Color(.12f,.56f,1),6.5f);
            heroRim.shadows=LightShadows.None;
            monsterRim=Point(root,"Monster ember rim",new Color(1,.20f,.055f),6.5f);
            monsterRim.shadows=LightShadows.None;
            QualitySettings.shadowDistance=30;QualitySettings.antiAliasing=4;QualitySettings.shadows=ShadowQuality.All;
            QualitySettings.shadowResolution=UnityEngine.ShadowResolution.High;QualitySettings.pixelLightCount=6;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.27f,.30f,.36f);RenderSettings.ambientEquatorColor=new Color(.21f,.23f,.28f);
            RenderSettings.ambientGroundColor=new Color(.10f,.10f,.12f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=13;RenderSettings.fogEndDistance=42;
            RenderSettings.fogColor=new Color(.11f,.125f,.145f);
            volcano=VolcanoStage.Create(root);
            monsterEffects=new MonsterAttackEffects(root,EnemyHome,HeroHome);monsterRay=new MonsterRay(root);effects=new CombatVfx(root);strikeTrails=new StrikeTrails(root);arcade=new ArcadeStageFx(root,HeroHome);
        }
        static Light Directional(Transform parent,string name,Color color,float intensity,Vector3 angles)
        {var light=new GameObject(name).AddComponent<Light>();light.transform.SetParent(parent,false);light.type=LightType.Directional;light.color=color;light.intensity=intensity;light.transform.eulerAngles=angles;return light;}
        static Light Point(Transform parent,string name,Color color,float range)
        {var light=new GameObject(name).AddComponent<Light>();light.transform.SetParent(parent,false);light.type=LightType.Point;light.color=color;light.range=range;return light;}
        internal static Transform Primitive(string name,PrimitiveType type,Transform parent,Vector3 position,Vector3 scale,Material material)
        {
            var obj=GameObject.CreatePrimitive(type);obj.name=name;obj.transform.SetParent(parent,false);obj.transform.position=position;obj.transform.localScale=scale;
            obj.GetComponent<Renderer>().sharedMaterial=material;
            if(Application.isPlaying)Object.Destroy(obj.GetComponent<Collider>());else Object.DestroyImmediate(obj.GetComponent<Collider>());return obj.transform;
        }
        public float BattleDelta(float dt,Battle state) => Closeup.Active?0:hitTiming.Delta(dt,state.Phase);
        public void ResetPresentation()
        {Closeup.Cancel();comboCamera.Clear();exchangeCamera.Clear();hitTiming.Clear();arcade.Clear();effects.Clear();monsterEffects.Clear();monsterRay.Clear();strikeTrails.Clear();cinematic.Clear();impact=0;impactAge=10;beamWasVisible=BeamStarted=landingPending=MonsterLanded=MonsterStaggerLanded=MonsterDissolving=TransformationCloseup=false;ThreatFocus=EntranceAge=lastEntranceAge=0;staggerLandings=enemy?.StaggerLandings??0;launchLandings=enemy?.LaunchLandings??0;beamLandings=enemy?.BeamLandings??0;dissolveStarts=enemy?.DissolveStarts??0;}
        public void Burst(Vector3 position,int count,float force=1,bool enemyEffect=false) => effects.Burst(position,count,force,enemyEffect);
        void Kick(float strength,bool special=false)
        {impact=strength;impactAge=0;hitTiming.Hit(special);}
        public void Hit(bool special,Battle state)
        {
            // Let a normal hit read as a cabinet impact without turning it into
            // a long shake. The stronger envelope is still short enough for a
            // four-year-old to keep the action legible.
            Kick(special?.12f:.065f,special);
            // Use the monster's position at this contact, including its own forward step.
            var position=special?BeamTarget:hero==null?EnemyHome+Vector3.up*2.15f:hero.StrikeOrigin(state.Action);
            cinematic.PulseAt(position,special?new Color(.25f,.68f,1):new Color(1,.48f,.16f),special?.82f:.30f);
            enemy?.BindSurfaceImpact(position);
            effects.Impact(position,special,combo:!special&&state!=null&&ComboStrikeMotion.Active(state),direction:BattleAxis,volumetric:special);
            if(special)effects.BeamHit(position,BattleAxis);
            if(!special&&state!=null&&state.Punches>0&&state.Punches%5==0)
            {
                Kick(.09f);
                cinematic.PulseAt(position,new Color(1,.68f,.20f),.42f);
                effects.Combo(position);
            }
        }
        public void Cue(GameCue cue,Battle state=null)
        {
            if(cue==GameCue.EnemyAttack)
            {
                // The rush begins with a readable visual beat.  It is a presentation
                // pulse only; damage is still resolved at EnemyHitSeconds.
                if(state==null||!MonsterRayMotion.Variant(state.EnemyAttackCount))
                {Burst(EnemyHome+Vector3.up*.16f,22,.62f,true);cinematic.PulseAt(EnemyHome+Vector3.up*2,new Color(1,.28f,.08f),.18f);}
            }
            if(cue==GameCue.Block)
            {
                Vector3 contact=ShieldCenter;
                if(state!=null&&MonsterRayMotion.Variant(state.EnemyAttackCount))contact+=Vector3.up*.26f;
                else if(state!=null&&MonsterSlamMotion.Variant(state.EnemyAttackCount))contact-=Vector3.up*.45f;
                else if(enemy!=null&&state!=null)
                    contact+=Vector3.ClampMagnitude(Vector3.ProjectOnPlane(enemy.EnemyStrikeOrigin(state)-ShieldCenter,BattleAxis),.65f);
                hero?.BindGuardImpact(contact);
                if(state!=null&&MonsterRayMotion.Variant(state.EnemyAttackCount))monsterRay.Impact(contact,true);
                effects.Impact(contact,false,true);monsterEffects.Impact(true,contact,state?.EnemyAttackCount??-1);Kick(.04f);
            }
            if(cue==GameCue.Hurt)
            {
                Vector3 impact=HeroHome+Vector3.up*2;
                if(state!=null&&MonsterRayMotion.Variant(state.EnemyAttackCount))monsterRay.Impact(impact,false);
                effects.Impact(impact,false,false,true);
                monsterEffects.Impact(false,impact,state?.EnemyAttackCount??-1);Kick(.055f);
            }
            if(cue==GameCue.HeroLanded)landingPending=true;
            if(cue==GameCue.Transform)Burst(HeroHome+Vector3.up*1.4f,30,.5f);
            if(cue==GameCue.Victory){effects.Impact(EnemyHome+Vector3.up*1.7f,true);Burst(EnemyHome+Vector3.up*2.2f,48,1.3f);}
            if(cue==GameCue.Beam)
            {
                Closeup.Begin();
                cinematic.Pulse(new Color(.20f,.68f,1),.42f);
                Burst(BeamOrigin,18,.48f);
                if(Debug.isDebugBuild)Debug.Log($"[BeamCloseup] begin duration={BeamCloseup.Duration:F2}");
            }
        }
        public void Tick(Battle state,float dt,float time)
        {
            MonsterLanded=MonsterStaggerLanded=MonsterDissolving=false;
            if(enemy!=null)
            {
                if(enemy.DissolveStarts>dissolveStarts&&!Showcase&&state.Phase==GamePhase.Victory)
                {
                    MonsterDissolving=true;
                    effects.GroundBurst(enemy.GroundContactPosition,BattleAxis,false);
                    effects.GroundBurst(enemy.GroundContactPosition,-BattleAxis,false);
                }
                dissolveStarts=enemy.DissolveStarts;
                if(enemy.BeamLandings>beamLandings&&!Showcase&&state.Phase==GamePhase.Battle)
                {
                    effects.GroundBurst(enemy.FootPosition(enemy.BeamRecoilLeft),BattleAxis,true,"beam-brace");
                    if(Debug.isDebugBuild)Debug.Log($"[MonsterBeam] brace side={(enemy.BeamRecoilLeft?"left":"right")} age={enemy.BeamRecoilAge:F3}");
                }
                beamLandings=enemy.BeamLandings;
                if(enemy.StaggerLandings>staggerLandings&&!Showcase&&state.Phase==GamePhase.Battle)
                {
                    MonsterStaggerLanded=true;
                    effects.GroundBurst(enemy.FootPosition(enemy.StaggerLeft),BattleAxis,true,"stagger");
                    impact=Mathf.Max(impact*Mathf.Exp(-impactAge*14),.022f);impactAge=0;
                    if(Debug.isDebugBuild)Debug.Log($"[MonsterStagger] landed side={(enemy.StaggerLeft?"left":"right")} age={enemy.StaggerAge:F3}");
                }
                staggerLandings=enemy.StaggerLandings;
                if(enemy.LaunchLandings>launchLandings&&!Showcase&&state.Phase==GamePhase.Battle)
                {
                    effects.GroundBurst((enemy.FootPosition(true)+enemy.FootPosition(false))*.5f,BattleAxis,true,"uppercut-land");
                    impact=Mathf.Max(impact*Mathf.Exp(-impactAge*14),.045f);impactAge=0;
                    if(Debug.isDebugBuild)Debug.Log($"[MonsterLaunch] landed age={enemy.LaunchAge:F3}");
                }
                launchLandings=enemy.LaunchLandings;
            }
            // Cues arrive before actor sampling. Emit the ground hit here,
            // using the pelvis from the actual landing pose, once per contact.
            if(landingPending)
            {
                if(state.Phase==GamePhase.Battle&&state.Action==HeroAction.Hurt)
                {
                    effects.GroundBurst(hero!=null?hero.GroundContactPosition:HeroHome,-BattleAxis,true,"hero-land");
                    impact=.025f;impactAge=0;
                }
                landingPending=false;
            }
            if(state.Phase==GamePhase.Paused||state.Phase==GamePhase.Waiting)cinematic.Clear();else cinematic.Tick(dt);
            float priorVictoryAge=previous==GamePhase.Victory?arcade.PhaseAge:0;
            clock+=dt;hitTiming.Tick(dt,state.Phase);arcade.Tick(state,dt,clock);
            EntranceAge=state.TransformationAge;
            TransformationCloseup=!Showcase&&state.Phase==GamePhase.Transforming&&TransformationMotion.Closeup(EntranceAge);
            if(!Showcase&&state.Phase==GamePhase.Transforming)
            {
                if(lastEntranceAge<TransformationMotion.CloseupStart&&EntranceAge>=TransformationMotion.CloseupStart)
                    cinematic.Pulse(new Color(.38f,.70f,1),.20f);
                if(lastEntranceAge<TransformationMotion.CloseupEnd&&EntranceAge>=TransformationMotion.CloseupEnd)
                    cinematic.Pulse(new Color(.58f,.80f,1),.22f);
                lastEntranceAge=EntranceAge;
            }
            else if(state.Phase!=GamePhase.Paused)lastEntranceAge=0;
            if(!Showcase&&state.Phase==GamePhase.Victory&&priorVictoryAge<VictoryMotion.LandingSeconds&&arcade.PhaseAge>=VictoryMotion.LandingSeconds)
            {
                MonsterLanded=true;
                effects.GroundBurst(enemy!=null?enemy.FootPosition(true):EnemyHome,-BattleAxis,true,"defeat");
                effects.GroundBurst(enemy!=null?enemy.FootPosition(false):EnemyHome,BattleAxis,false);
                impact=.032f;impactAge=0;
                if(Debug.isDebugBuild)Debug.Log("[VictoryStage] monster-landed age="+arcade.PhaseAge.ToString("F2"));
            }
            bool wasCloseup=Closeup.Active;Closeup.Tick(dt,state,Showcase);
            if(wasCloseup&&!Closeup.Active&&Debug.isDebugBuild)Debug.Log($"[BeamCloseup] end phase={state.Phase} action={state.Action}");
            float focus=Closeup.Focus;
            int oldShots=comboCamera.Shots;
            comboCamera.Tick(state,dt,Showcase||Closeup.Active);
            exchangeCamera.Tick(state,dt,Showcase||Closeup.Active);
            if(comboCamera.Shots>oldShots&&Debug.isDebugBuild)Debug.Log($"[ComboCamera] begin side={comboCamera.Side} punches={state.Punches}");
            bool combat=state.Phase==GamePhase.Battle;
            float punchPulse=combat&&(state.Action==HeroAction.LeftPunch||state.Action==HeroAction.RightPunch)
                ?Mathf.Sin(Mathf.Clamp01(state.ActionAge/.42f)*Mathf.PI):0;
            float enemyPulse=combat&&state.Enemy==EnemyPhase.Attack
                ?Mathf.Sin(Mathf.Clamp01(state.EnemyAge/Battle.EnemyAttackSeconds)*Mathf.PI):0;
            float warningPulse=combat&&state.Enemy==EnemyPhase.Windup
                ?.5f+.5f*Mathf.Sin(state.EnemyAge*8):0;
            float beamPulse=state.Action==HeroAction.Beam?Mathf.Clamp01(state.ActionAge/1.15f):0;
            heroRim.transform.position=HeroHome-BattleAxis*1.15f+Vector3.up*2.35f;
            monsterRim.transform.position=EnemyHome+BattleAxis*1.05f+Vector3.up*2.35f;
            heroRim.intensity=Showcase?.55f:combat?.48f+punchPulse*1.6f+beamPulse*1.4f:state.Phase==GamePhase.Transforming?1.1f:.32f;
            monsterRim.intensity=Showcase?.42f:combat?.42f+enemyPulse*1.45f+warningPulse*.9f:state.Phase==GamePhase.Victory?Mathf.Max(0,.75f-arcade.PhaseAge*.3f):.24f;
            if(state.Phase==GamePhase.Victory)
            {
                heroRim.color=new Color(.18f,.66f,1);
                monsterRim.color=new Color(1,.28f,.08f);
            }
            else
            {
                heroRim.color=state.Action==HeroAction.Beam?new Color(.20f,.78f,1):new Color(.12f,.56f,1);
                monsterRim.color=state.Enemy==EnemyPhase.Attack?new Color(1,.30f,.08f):new Color(1,.20f,.055f);
            }
            bool battleView=state.Phase==GamePhase.Battle||state.Phase==GamePhase.Paused||state.Phase==GamePhase.Victory;
            // Give the two fighters the visual priority of an arcade cabinet while
            // retaining enough margin for the feet, effects and camera preview.
            // Leave space above the helmets and below the planted feet for the
            // edge HUD, including the return from a special-move close-up.
            float fieldOfView=Showcase||state.Phase==GamePhase.Victory||state.Phase==GamePhase.Transforming?32:battleView?(state.Action==HeroAction.Beam?29.2f:state.Shield?28.5f:29.2f):37;
            framingFieldOfView=Mathf.Lerp(framingFieldOfView,fieldOfView,dt*4);
            float dynamicZoom=0;
            if(!ReferenceEquals(threatBattle,state)){threatBattle=state;ThreatFocus=0;}
            if(Showcase||!combat||Closeup.Active||state.Action==HeroAction.Beam)ThreatFocus=0;
            else if(state.Enemy==EnemyPhase.Windup)
                ThreatFocus=Mathf.SmoothStep(0,1,state.EnemyAge/.9f)*(1-Mathf.SmoothStep(0,1,(state.EnemyAge-2.1f)/1.3f));
            else ThreatFocus=Mathf.MoveTowards(ThreatFocus,0,dt*3.4f);
            Camera.fieldOfView=Mathf.Lerp(framingFieldOfView,14,focus);
            float h=160*Mathf.Tan(27*Mathf.Deg2Rad*.5f);
            var texture=backdropMaterial.mainTexture;
            float aspect=texture?texture.width/(float)texture.height:16f/9f;
            // The victory pan looks farther left than the combat lens. Grow
            // the distant plate with that pan so its edge never enters view.
            float victoryFraming=!Showcase&&state.Phase==GamePhase.Victory
                ?Mathf.SmoothStep(0,1,Mathf.Clamp01((arcade.PhaseAge-VictoryMotion.TurnStartSeconds)/2)):0;
            float scale=Mathf.Max(1,Camera.aspect/aspect)*(TransformationCloseup?1.56f:Mathf.Lerp(1.5f,1.7f,victoryFraming));
            scale*=Mathf.Lerp(1,1.32f,ThreatFocus);
            backdrop.localScale=new Vector3(h*aspect*scale,h*scale,1);
            backdropMaterial.SetFloat("_Clock",clock);
            var backgroundRotation=Quaternion.LookRotation(lookAt-cameraHome);
            backdrop.rotation=backgroundRotation;
            // Keep Fuji's summit and open sky in frame when the fight lens
            // tightens. Overscan still covers the wider introduction and recoil.
            backdrop.position=cameraHome+backgroundRotation*new Vector3(0,-h*.04f,80);
            impactAge+=dt;
            if(state.Phase==GamePhase.Paused||state.Phase==GamePhase.Waiting){impact=0;effects.Clear();}
            float kick=impact*Mathf.Exp(-impactAge*14)*(1-focus);
            // One damped recoil, with a restrained camera displacement for a young player.
            Camera.transform.position=cameraHome+new Vector3(Mathf.Sin(impactAge*47)*kick,Mathf.Sin(impactAge*31)*kick*.35f,-kick*.4f);
            Vector3 target=lookAt;
            // Use the stable arena basis, not the previous frame's camera. A
            // repeated render or returning from a hero cut-in must not feed its
            // orientation back into the next strike/defence camera offset.
            Vector3 viewForward=(lookAt-cameraHome).normalized;
            Vector3 viewRight=Vector3.Cross(Vector3.up,viewForward).normalized;
            // One low approach at the start of the warning, then return with
            // two seconds left for the child to read the guard and incoming claw.
            // Follow the enemy clock; an instruction cancellation gets a short
            // return, while a beam cut-in, pause or new round clears the shot.
            Camera.transform.position+=new Vector3(-.9f,-.55f,2.35f)*ThreatFocus;
            target+=new Vector3(.22f,.84f,0)*ThreatFocus;
            // The warning close-up owns its lens even when the child raises
            // a shield; the wider defence framing resumes before the lunge.
            dynamicZoom+=(1.2f+Mathf.Max(0,framingFieldOfView-25))*ThreatFocus;
            if(!Showcase&&state.Phase==GamePhase.Battle&&!Closeup.Active)
            {
                float hurt=state.Action==HeroAction.Hurt?KnockdownMotion.Weight(state.ActionAge):0;

                // A short arcade lens move makes each exchange readable on a TV.
                // It is intentionally small and uses the same deterministic battle clock
                // as the actors, so it never changes gesture timing or gameplay state.
                bool heroStrike=state.Action==HeroAction.LeftPunch||state.Action==HeroAction.RightPunch;
                float strike=Mathf.Sin(Mathf.Clamp01(state.ActionAge/.42f)*Mathf.PI);
                if(heroStrike)
                {
                    float side=state.Action==HeroAction.LeftPunch?-1:1;
                    Camera.transform.position+=BattleAxis*(.40f*strike)+viewRight*(side*.18f*strike);
                    target+=BattleAxis*(.23f*strike)+Vector3.up*(.075f*strike);
                    dynamicZoom+=.55f*strike;
                }
                if(state.Action==HeroAction.Hurt)
                {
                    // Keep the falling hero inside the 16:9 frame. The old
                    // positive lateral kick pushed the silhouette into the
                    // lower-left corner while the HUD was still visible.
                    Camera.transform.position+=viewRight*(-.10f*hurt)+BattleAxis*(.10f*hurt);
                    target+=viewRight*(-.34f*hurt)+Vector3.up*(-.16f*hurt);
                    // Open the lens for the fall so the full body, dust and
                    // the monster's reaction share one readable cabinet shot.
                    dynamicZoom-=3.8f*hurt;
                }
            }
            if(!Showcase&&state.Phase==GamePhase.Victory)
            {
                // Let the collapse finish in the two-actor shot, then follow
                // the hero's planted-foot turn toward the child.
                float victory=victoryFraming;
                float sway=Mathf.Sin(Mathf.Clamp01(arcade.PhaseAge/3.1f)*Mathf.PI);
                Camera.transform.position+=viewRight*(sway*.34f)+Vector3.up*(victory*.16f)+BattleAxis*(victory*.18f);
                target=Vector3.Lerp(lookAt,(hero!=null?hero.Root.position:HeroHome)+Vector3.up*2.04f,victory*.92f);
                dynamicZoom+=victory*1.5f;
            }
            // Apply the ordinary strike lens before the dedicated combo and
            // enemy-exchange compositions below take ownership of the camera.
            Camera.fieldOfView=Mathf.Lerp(framingFieldOfView-dynamicZoom,14,focus);
            if(ComboFocus>0)
            {
                Vector3 closePosition=cameraHome+viewRight*(comboCamera.Side*.65f)+Vector3.down*.14f+viewForward*.70f;
                Camera.transform.position=Vector3.Lerp(Camera.transform.position,closePosition,ComboFocus);
                target=Vector3.Lerp(target,lookAt+Vector3.down*.04f+BattleAxis*.06f,ComboFocus);
                Camera.fieldOfView=Mathf.Lerp(Camera.fieldOfView,29.2f,ComboFocus);
            }
            if(ExchangeFocus>0)
            {
                float side=exchangeCamera.Side,travel=exchangeCamera.Travel;
                float hurt=state.Action==HeroAction.Hurt?KnockdownMotion.Weight(state.ActionAge):0;
                Vector3 position=cameraHome+viewRight*(side*Mathf.Lerp(.18f,.58f,travel))-viewForward*.08f;
                Vector3 exchangeTarget=lookAt+viewRight*(side*.06f-.20f*hurt)+Vector3.up*(-.035f-.10f*hurt);
                Camera.transform.position=Vector3.Lerp(Camera.transform.position,position,ExchangeFocus);
                target=Vector3.Lerp(target,exchangeTarget,ExchangeFocus);
                Camera.fieldOfView=Mathf.Lerp(Camera.fieldOfView,28.5f+1.7f*hurt,ExchangeFocus);
            }
            float launchFocus=enemy?.LaunchCamera??0;
            if(!Showcase&&MonsterRayMotion.Active(state)&&!Closeup.Active)
            {
                float weight=MonsterRayMotion.Prepare(state);
                Camera.transform.position=Vector3.Lerp(Camera.transform.position,cameraHome+viewRight*.22f,weight);
                target=Vector3.Lerp(target,lookAt+Vector3.up*.12f,weight);
                Camera.fieldOfView=Mathf.Lerp(Camera.fieldOfView,29.0f,weight);
            }
            if(MonsterLaunchMotion.Uppercut(state)&&state.ActionAge<Battle.PunchHitSeconds&&state.Enemy!=EnemyPhase.Attack&&
                (state.Enemy!=EnemyPhase.Windup||state.WarningDuration-state.EnemyAge>1.6f))
                launchFocus=Mathf.Max(launchFocus,Mathf.SmoothStep(0,1,state.ActionAge/Battle.PunchHitSeconds));
            if(!Showcase&&state.Phase==GamePhase.Battle&&!Closeup.Active&&launchFocus>0)
            {
                float weight=launchFocus;
                Camera.transform.position=Vector3.Lerp(Camera.transform.position,cameraHome+viewRight*.36f+Vector3.up*.16f,weight);
                target=Vector3.Lerp(target,lookAt+Vector3.up*.35f+BattleAxis*.10f,weight);
                Camera.fieldOfView=Mathf.Lerp(Camera.fieldOfView,31.5f,weight);
            }
            Camera.transform.LookAt(Vector3.Lerp(target,HeroHome+BattleAxis*.2f+Vector3.up*2.60f,focus));
            if(HeroShot)
            {
                // Cut to a front three-quarter lens. A restrained orbit keeps the
                // finisher alive like a cabinet cut-in without changing the pose
                // window or sweeping the camera through the arena.
                float closeupT=Mathf.Clamp01(Closeup.Age/BeamCloseup.Duration);
                float orbit=Mathf.Sin(closeupT*Mathf.PI)*5.5f;
                Vector3 offset=Quaternion.AngleAxis(orbit,Vector3.up)*new Vector3(3.8f,2.82f,.9f);
                offset.y+=Mathf.Sin(closeupT*Mathf.PI)*.10f;
                Camera.transform.position=HeroHome+offset;
                Camera.transform.LookAt(HeroHome+BattleAxis*.26f+Vector3.up*(2.93f+Mathf.Sin(closeupT*Mathf.PI)*.06f));
                Camera.fieldOfView=32;
                // Reproject the distant landscape for this dedicated lens.
                backdrop.rotation=Camera.transform.rotation;
                backdrop.position=Camera.transform.position+Camera.transform.rotation*new Vector3(0,-h*.04f,80);
            }
            if(TransformationCloseup)
            {
                float t=Mathf.InverseLerp(TransformationMotion.CloseupStart,TransformationMotion.CloseupEnd,EntranceAge);
                // One front three-quarter reveal, then cut back under the
                // release pulse. No fast orbit through the monster or scenery.
                Vector3 side=Vector3.Cross(Vector3.up,BattleAxis);
                Camera.transform.position=HeroHome+BattleAxis*Mathf.Lerp(6.7f,6.3f,t)+side*1.75f+Vector3.up*2.55f;
                Camera.transform.LookAt(HeroHome+Vector3.up*2.02f);Camera.fieldOfView=37;
                backdrop.rotation=Camera.transform.rotation;
                backdrop.position=Camera.transform.position+Camera.transform.rotation*new Vector3(0,-h*.04f,80);
            }
            volcano.SetBackdrop(backdropMaterial.mainTexture,backdrop.worldToLocalMatrix,clock);
            volcano.Tick(clock);
            bool active=state.Phase==GamePhase.Battle;
            monsterEffects.Tick(state,Camera,dt,enemy!=null&&enemy.IsRigged?(Vector3?)enemy.EnemyStrikeOrigin(state):null);
            monsterRay.Tick(state,Camera,enemy?.RayOrigin??EnemyHome+Vector3.up*3.32f-BattleAxis*.46f,
                state.Shield?ShieldCenter+Vector3.up*.26f:HeroHome+Vector3.up*2,Showcase||Closeup.Active);
            bool firing=active&&!Closeup.Active&&state.Action==HeroAction.Beam&&state.ActionAge>BeamStream.LaunchSeconds;
            BeamStarted=firing&&!beamWasVisible;beamWasVisible=firing;
            if(BeamStarted&&Debug.isDebugBuild)Debug.Log($"[BeamCloseup] beam-visible actionAge={state.ActionAge:F2}");
            effects.Tick(state,Camera,dt,BeamOrigin,EnemyHome+Vector3.up*2.6f,ShieldCenter,BattleAxis,Closeup.Active,focus,firing,BeamTarget,
                Closeup.Age,hero?.StrikeOrigin(HeroAction.LeftPunch)??BeamOrigin,hero?.HandPosition??BeamOrigin);
            strikeTrails.Tick(state,Camera,dt,hero,enemy,Closeup.Active);
            if(!Closeup.Active&&dt>0)
            {
                int before=effects.GroundContactCount;effects.MotionDust(state,hero,enemy,BattleAxis);
                if(effects.GroundContactCount>before&&effects.GroundContactCause=="slam")
                {impact=.085f;impactAge=0;cinematic.PulseAt(enemy.Root.position,new Color(1,.65f,.32f),.22f);}
            }
            if(state.Phase!=previous){transformAge=0;previous=state.Phase;}
            transformAge+=dt;
            if(state.Phase==GamePhase.Transforming&&clock>celebrateAt)
            {celebrateAt=clock+.09f;Burst(HeroHome+new Vector3(Random.Range(-.55f,.55f),transformAge%1*3.0f,Random.Range(-.3f,.3f)),3,.25f);}
            if(state.Phase==GamePhase.Victory&&clock>celebrateAt&&transformAge<3)
            {celebrateAt=clock+.25f;Burst(HeroHome+new Vector3(Random.Range(-1.3f,1.3f),2.8f,Random.Range(-.7f,.7f)),4,.45f);}
        }
    }
}
