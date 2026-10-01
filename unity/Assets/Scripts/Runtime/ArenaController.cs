using System;
using UnityEngine;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    public sealed partial class ArenaController : MonoBehaviour
    {
        Battle battle=new Battle();
        readonly Battle showcaseBattle=new Battle();
        bool showcase;int showcaseFrame;
        ReviewPlayback review;float reviewFinishedAt=-1;int reviewBeams;bool reviewPaused,reviewFinisher;
        readonly GestureRecognizer recognizer=new GestureRecognizer();
        readonly PlayerPresence presence=new PlayerPresence();
        PoseClient client;
        PreviewClient previewClient;
        PreviewFrame previewFrame;
        Texture2D previewTexture;
        PoseFrame pose;
        PlayerInput held;
        AnimatedActor hero,enemy;
        GameWorld world;
        GameAudio sound;
        LocalMusic music;
        HudPainter hud;
        VictoryPhoto photo;
        bool photoAvailable,autoPhotoOpened,finalGuide;
        float victoryAt,waitingGuideAt=20;
        bool keyboard,paused,muted,settings,audioSettings,videoSettings,showPreview=true,previewReported,lastTracking;
        // The first play session is for a four-year-old: start with the
        // forgiving pose envelope. Parents can still raise the threshold in
        // Settings after the child has learned the three gestures.
        int draftDifficulty=0,draftResolution=1;
        bool draftPhotoAi=true;
        Vector3 previousMouse;
        float cursorUntil;
        const string MonsterHitsKey="battle.monsterHits";
        int monsterHits=Battle.DefaultMonsterHits,draftMonsterHits=Battle.DefaultMonsterHits;
        string caption="",stream,gestureFeedback="";
        float gestureFeedbackUntil;
        long sequence;
        float captionUntil,lastHealth=Battle.DefaultMonsterHits,enemyHealthDisplay=Battle.DefaultMonsterHits,impact,phaseStarted,battleStartCueAt=-1,hintAt=12,hitUntil,beamHelpAt,damagePopAt;
        int presentedPunches,presentedHits,comboCount;
        float comboUntil;
        int lastDamage;
        GamePhase lastPhase;
        float sampleAge,sampleSeconds,sampleWorst;
        int sampleFrames;
        int sampleWindows;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
#if UNITY_EDITOR
            if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--audio-version")>=0)return;
#endif
            if(FindFirstObjectByType<ArenaController>()==null)new GameObject("UltramanGame").AddComponent<ArenaController>();
        }
        static int LocalPort(string option,int fallback)
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,option);
            return i>=0&&i+1<args.Length&&int.TryParse(args[i+1],out int port)&&port>0&&port<=65535?port:fallback;
        }
        void Start()
        {
            Application.SetStackTraceLogType(LogType.Log,StackTraceLogType.None);
            monsterHits=Battle.ClampMonsterHits(PlayerPrefs.GetInt(MonsterHitsKey,Battle.DefaultMonsterHits));
            battle=new Battle(monsterHits);lastHealth=enemyHealthDisplay=battle.MaxHealth;
            presentedPunches=presentedHits=comboCount=0;comboUntil=0;
            DisplayPreferences.Startup();recognizer.Difficulty=PlayerPrefs.GetInt("gesture.difficulty",0);
            keyboard=Array.IndexOf(Environment.GetCommandLineArgs(),"--keyboard")>=0;
            if(Debug.isDebugBuild&&Array.IndexOf(Environment.GetCommandLineArgs(),"--review-playback")>=0)
            {review=new ReviewPlayback(Array.IndexOf(Environment.GetCommandLineArgs(),"--review-slam")>=0,Array.IndexOf(Environment.GetCommandLineArgs(),"--review-ray")>=0,Array.IndexOf(Environment.GetCommandLineArgs(),"--review-linked")>=0);reviewFinisher=Array.IndexOf(Environment.GetCommandLineArgs(),"--review-finisher")>=0;keyboard=true;monsterHits=reviewFinisher?24:Battle.DefaultMonsterHits;battle=new Battle(monsterHits);lastHealth=enemyHealthDisplay=battle.MaxHealth;}
            Application.runInBackground=true;Screen.sleepTimeout=SleepTimeout.NeverSleep;
            client=new PoseClient(LocalPort("--pose-port",8765));previewClient=new PreviewClient(LocalPort("--preview-port",8766));
            var font=Resources.Load<Font>("Fonts/NotoSansSC-Regular");
            if(!font)font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hud=new HudPainter(font);world=new GameWorld();sound=new GameAudio(gameObject);
            sound.InstructionStarted+=InstructionStarted;
            photoAvailable=!keyboard||Array.IndexOf(Environment.GetCommandLineArgs(),"--photo-port")>=0;
            photo=new VictoryPhoto(LocalPort("--photo-port",8767),sound);
            photo.KeyboardMode=keyboard;
            heroIndex=HeroRoster.Index(PlayerPrefs.GetString("hero.selected","Tiga"));if(review!=null||!HeroAvailable(heroIndex))heroIndex=0;
            if(review!=null)
            {
                var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--review-hero");
                if(at>=0)
                {
                    if(at+1>=args.Length)throw new ArgumentException("Missing review hero");
                    heroIndex=HeroRoster.Index(args[at+1]);
                    if(SelectedHero.Id!=args[at+1]||!HeroAvailable(heroIndex))throw new ArgumentException("Unavailable review hero");
                }
                Debug.Log($"[FullGameReviewHero] id={SelectedHero.Id}");
            }
            photo.HeroId=SelectedHero.Id;sound.HeroId=SelectedHero.Id;
            hero=new AnimatedActor(SelectedHero.Id,world.HeroHome,world.EnemyHome);enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);
            world.BindActors(hero,enemy);
            PresentationWarmup.Run(world,hero,enemy);
            music=gameObject.AddComponent<LocalMusic>();music.Initialize(sound);
            if(!keyboard)sound.Speak("arcade_ready",1,GamePhase.Waiting);
        }
        void Update()
        {
            if(Input.mousePosition!=previousMouse){previousMouse=Input.mousePosition;cursorUntil=Time.unscaledTime+3;}
            Cursor.visible=keyboard||settings||showcase||music.Choosing||Time.unscaledTime<cursorUntil||photo.Stage==PhotoStage.Review;
            if(Input.GetKeyDown(KeyCode.F2)){SetMode(!keyboard);return;}
            if(Input.GetKeyDown(KeyCode.F4)){if(settings)CloseSettings();else OpenSettings();}
            if(settings)
            {
                if(!audioSettings&&!videoSettings)
                {
                    if(Input.GetKeyDown(KeyCode.LeftArrow))draftMonsterHits=Battle.ClampMonsterHits(draftMonsterHits-10);
                    if(Input.GetKeyDown(KeyCode.RightArrow))draftMonsterHits=Battle.ClampMonsterHits(draftMonsterHits+10);
                }
                if(Input.GetKeyDown(KeyCode.Escape))CloseSettings();
                if(Input.GetKeyDown(KeyCode.Return))ApplySettings();
                sound.Tick(GamePhase.Paused,muted,Time.unscaledDeltaTime);
                return;
            }
            if(photo.Active)
            {
                if(Input.GetKeyDown(KeyCode.Escape))photo.Back();
                if(Input.GetKeyDown(KeyCode.Return))photo.PlayAgain();
                if(Input.GetKeyDown(KeyCode.Space))photo.Retake();
                if(Input.GetKeyDown(KeyCode.F11))Screen.fullScreen=!Screen.fullScreen;
                long photoNow=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                UpdatePreview(photoNow);ReadPhotoPose();
                sound.Tick(GamePhase.Victory,muted,Time.unscaledDeltaTime);
                photo.Tick(photoNow,pose,previewTexture);
                if(photo.PlayAgainRequested)Restart();
                return;
            }
            if(Input.GetKeyDown(KeyCode.F7)&&battle.Phase==GamePhase.Victory&&photoAvailable) {photo.Open();return;}
            if(Input.GetKeyDown(KeyCode.F3))showPreview=!showPreview;
            if(Input.GetKeyDown(KeyCode.F5))showcase=!showcase;
            if(showcase)showcaseFrame=(showcaseFrame+8+(Input.GetKeyDown(KeyCode.RightArrow)?1:0)-(Input.GetKeyDown(KeyCode.LeftArrow)?1:0))%8;
            if(!showcase&&battle.Phase==GamePhase.Waiting)
            {
                if(Input.GetKeyDown(KeyCode.LeftArrow))SelectHero(heroIndex-1);
                if(Input.GetKeyDown(KeyCode.RightArrow))SelectHero(heroIndex+1);
            }
            if(Input.GetKeyDown(KeyCode.F6)) {showcase=false;OpenSettings(true);music.Choose();}
            if(Input.GetKeyDown(KeyCode.F11))Screen.fullScreen=!Screen.fullScreen;
            if(Input.GetKeyDown(KeyCode.Escape)) { if(showcase)showcase=false;else if(settings)settings=false;else paused=!paused; }
            if(Input.GetKeyDown(KeyCode.R))Restart();
            long now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();float rawDt=Time.unscaledDeltaTime,dt=Mathf.Min(rawDt,.1f);
            sampleAge+=rawDt;
            if(Debug.isDebugBuild&&rawDt>.12f)Debug.Log($"[FrameTiming] age={sampleAge:F2} phase={battle.Phase} action={battle.Action} frameMs={rawDt*1000:F1}");
            if(Debug.isDebugBuild&&sampleWindows<2&&sampleAge>3)
            {
                sampleSeconds+=rawDt;sampleFrames++;sampleWorst=Mathf.Max(sampleWorst,rawDt);
                if(sampleSeconds>=10)
                {sampleWindows++;Debug.Log($"[Runtime] window={sampleWindows} renderFps={sampleFrames/sampleSeconds:F1} worstFrameMs={sampleWorst*1000:F1} {sound.Diagnostics}");sampleSeconds=sampleWorst=0;sampleFrames=0;}
            }
            UpdatePreview(now);
            PlayerInput input;
            if(keyboard)
                input=new PlayerInput {Tracking=true,Transform=Input.GetKeyDown(KeyCode.Space),LeftPunch=Input.GetKeyDown(KeyCode.A),
                    RightPunch=Input.GetKeyDown(KeyCode.D),Shield=Input.GetKey(KeyCode.S),Beam=Input.GetKeyDown(KeyCode.J)};
            else
            {
                input=held;input.Transform=input.LeftPunch=input.RightPunch=input.Beam=false;
                var line=client.TakeLatest();
                if(line!=null)
                {
                    try
                    {
                        var incoming=JsonUtility.FromJson<PoseFrame>(line);
                        if(incoming!=null&&(incoming.streamId!=stream||incoming.sequence>sequence))
                        {
                            pose=incoming;stream=pose.streamId;sequence=pose.sequence;
                            // A charged finisher must remain available through the
                            // monster's warning beat. Previously this was disabled
                            // during Windup/Attack, so a child who started the beam
                            // pose at the warning could be forced into guard and
                            // miss the arcade finisher window.
                            input=recognizer.Update(pose,now,battle.Phase==GamePhase.Battle&&battle.Energy>=Battle.MaxEnergy,battle.Phase==GamePhase.Waiting);
                            string detected=input.Beam?"必杀光线":input.Transform?"举手变身":input.LeftPunch||input.RightPunch?
                                (recognizer.ForwardPunch?"向前挥拳":"侧前挥拳"):"";
                            if(detected.Length>0)
                            {
                                gestureFeedback="已识别："+detected;gestureFeedbackUntil=Time.unscaledTime+1.3f;
                                if(Debug.isDebugBuild)Debug.Log($"[Gesture] {detected} sequence={pose.sequence}");
                            }
                            if(input.Shield&&!held.Shield)
                            {
                                gestureFeedback="护盾已展开";gestureFeedbackUntil=Time.unscaledTime+1.3f;
                                if(Debug.isDebugBuild)Debug.Log($"[Gesture] 护盾已展开 sequence={pose.sequence}");
                            }
                        }
                    }
                    catch(ArgumentException) {pose=null;recognizer.Reset();input=default;}
                }
                if(!PoseQuality.Present(pose,now)) {input=default;recognizer.Reset();}
                input.Tracking=presence.Update(pose,now);held=input;
            }
            if(review!=null)input=review.Next(battle,dt);
            if(!keyboard&&battle.Phase==GamePhase.Waiting&&Time.unscaledTime>=waitingGuideAt&&!sound.VoicePlaying)
            {sound.Speak("arcade_ready",1,GamePhase.Waiting);waitingGuideAt=Time.unscaledTime+22;}
            if(paused||settings||showcase||music.Choosing)input.Tracking=false;
            if(input.Tracking!=lastTracking)
            { lastTracking=input.Tracking;if(Debug.isDebugBuild)Debug.Log($"[Input] tracking={lastTracking} mode={(keyboard?"keyboard":pose?.source??"camera")} health={battle.EnemyHealth} energy={battle.Energy}"); }
            if(battle.Phase==GamePhase.Waiting&&Time.unscaledTime-selectionChangedAt<.5f)input.Transform=false;
            battle.Tick(world.BattleDelta(dt,battle),input);
            if(battle.Punches>presentedPunches)
            {
                comboCount=Time.unscaledTime<=comboUntil?comboCount+(battle.Punches-presentedPunches):battle.Punches-presentedPunches;
                comboUntil=Time.unscaledTime+2.6f;presentedPunches=battle.Punches;
            }
            if(battle.HitsTaken>presentedHits)
            {presentedHits=battle.HitsTaken;comboCount=0;comboUntil=0;}
            if(battle.Phase!=lastPhase) {phaseStarted=Time.unscaledTime;lastPhase=battle.Phase;}
            sound.Tick(paused||settings||showcase?GamePhase.Paused:battle.Phase,muted,dt);
            while(battle.TryCue(out var cue))PlayCue(cue);
            bool damage=battle.EnemyHealth<lastHealth;bool specialDamage=lastHealth-battle.EnemyHealth>1;
            if(damage)
            {
                bool special=lastHealth-battle.EnemyHealth>1;lastDamage=Mathf.RoundToInt(lastHealth-battle.EnemyHealth);damagePopAt=Time.unscaledTime;
                impact=special?.35f:.2f;hitUntil=Time.unscaledTime+1;
                if(Debug.isDebugBuild)Debug.Log($"[ArcadeImpact] hold={(special?.14f:.065f):F3}s special={special} damage={lastDamage}");
                if(battle.Finishing&&Debug.isDebugBuild)Debug.Log($"[FinalStrike] contact action={battle.Action} actionAge={battle.ActionAge:F3} health={battle.EnemyHealth}");
                sound.Hit(!special&&battle.Punches%5==0,special);
            }
            lastHealth=battle.EnemyHealth;enemyHealthDisplay=Mathf.Max(battle.EnemyHealth,Mathf.MoveTowards(enemyHealthDisplay,battle.EnemyHealth,Mathf.Max(30,monsterHits*1.8f)*dt));impact=Mathf.Max(0,impact-dt);
            world.Showcase=showcase;
            // Sample the contact pose first. The shared impact clock holds its
            // action age on following frames while sparks, light and recoil
            // continue to move. Never freeze the renderer on a pre-contact pose.
            hero.Update(showcase?showcaseBattle:battle,world.Camera,dt,Time.unscaledTime,showcase?showcaseFrame:-1);
            enemy.Update(showcase?showcaseBattle:battle,world.Camera,dt,Time.unscaledTime,showcase?showcaseFrame:-1);
            if(damage)world.Hit(specialDamage,battle);
            int groundContacts=world.GroundContactCount;
            world.Tick(showcase?showcaseBattle:battle,dt,Time.unscaledTime);
            sound.SetChargePower(showcase?0:world.ChargePower);
            sound.SetMonsterRayPower(showcase?0:world.MonsterRayPower);
            if(world.GroundContactCount>groundContacts)sound.GroundContact(world.GroundContactCause=="rush"||world.GroundContactCause=="slam"||world.GroundContactCause=="arrival");
            if(world.MonsterEntranceRoar)sound.MonsterArrival();
            if(world.MonsterLanded)sound.MonsterLanding();
            if(world.MonsterDissolving)sound.MonsterDeparture();
            if(world.MonsterStaggerLanded)sound.MonsterRecoveryStep();
            if(world.BeamStarted){reviewBeams++;}
            if(world.BeamStarted)sound.Effect("beam",sound.HasOriginalBeamVoice?.4f:.7f);
            enemy.SetPresentationOpacity(world.EnemyOpacity);
            if(!keyboard&&battle.Phase==GamePhase.Victory&&photoAvailable&&!autoPhotoOpened&&Time.unscaledTime>=victoryAt+6&&!sound.VoicePlaying)
            {autoPhotoOpened=true;photo.Open();}
            if(!keyboard&&!finalGuide&&!battle.Finishing&&battle.Phase==GamePhase.Battle&&battle.EnemyHealth<=battle.MaxHealth*.3f&&battle.Energy<Battle.MaxEnergy&&battle.Enemy==EnemyPhase.Rest&&battle.InstructionRemaining<=0&&!sound.VoicePlaying)
            {finalGuide=true;sound.Speak("arcade_final",3,battle.Phase);}
            if(!keyboard&&!paused&&!settings&&!showcase&&!world.Closeup.Active&&!battle.Finishing&&battle.Phase==GamePhase.Battle)
            {
                if(battle.Punches==0&&Time.unscaledTime>hintAt&&battle.Enemy==EnemyPhase.Rest&&battle.InstructionRemaining<=0)
                {sound.Speak("tutorial",3,battle.Phase);hintAt=Time.unscaledTime+20;}
                if(battle.Energy<Battle.MaxEnergy)beamHelpAt=Time.unscaledTime+6;
                else if(Time.unscaledTime>beamHelpAt&&battle.InstructionRemaining<=0&&battle.Enemy==EnemyPhase.Rest)
                {sound.Speak(recognizer.BeamNeedsRelease&&sound.HasVoice("beam_reset")?"beam_reset":"beam_help",3,battle.Phase);beamHelpAt=Time.unscaledTime+22;}
            }
        }
        void LateUpdate()
        {
            // The photo view covers the entire screen and owns its offscreen
            // camera. Suspend arena shadows/bloom until the child returns.
            world.Camera.enabled=!photo.Active;
            CaptureGuidedProof();
            if(review==null)return;
            if(battle.Phase==GamePhase.Paused)reviewPaused=true;
            if(battle.Phase==GamePhase.Victory)
            {
                if(reviewFinishedAt<0)
                {
                    reviewFinishedAt=review.Age;
                    bool pass=battle.MaxHealth==(reviewFinisher?24:50)&&battle.Punches==(reviewFinisher?15:32)&&reviewBeams==(reviewFinisher?1:2)&&battle.Blocks>=1&&battle.HitsTaken==1&&reviewPaused;
                    Debug.Log($"[FullGameReview] pass={pass} age={review.Age:F2} punches={battle.Punches} beams={reviewBeams} blocks={battle.Blocks} hurt={battle.HitsTaken} pause={reviewPaused} health={battle.EnemyHealth}");
                    if(!pass){Application.Quit(2);return;}
                }
                if(review.Age-reviewFinishedAt>5)Application.Quit(0);
            }
            else if(review.Age>150){Debug.LogError("[FullGameReview] timeout");Application.Quit(3);}
        }
        void InstructionStarted(string key,float voiceSeconds)
        {
            battle.GiveInstructionTime(voiceSeconds,key=="warning");
            if(Debug.isDebugBuild)Debug.Log($"[Instruction] key={key} voice={voiceSeconds:F2} reaction={Battle.InstructionReactionSeconds:F1} warningDuration={battle.WarningDuration:F2} enemyHold={battle.InstructionRemaining:F2}");
            switch(key)
            {
                case "battle":caption="挥动拳头，守护火山基地！";break;
                case "energy":caption="能量满了 · 双手向前推，停一下";break;
                case "beam_help":caption="摆 L 形，或双手向前推 · 停一下";break;
                case "beam_reset":caption="先收回双手，再摆光线姿势，停一下";break;
                case "tutorial":caption="先把手收回来，再挥出去";break;
                case "resume":caption="准备好了，继续！";break;
                default:return;
            }
            captionUntil=Time.unscaledTime+voiceSeconds+Battle.InstructionReactionSeconds;
            if(key=="energy")beamHelpAt=captionUntil+3;
        }
        void PlayCue(GameCue cue)
        {
            if(Debug.isDebugBuild)Debug.Log($"[Game] cue={cue} phase={battle.Phase} health={battle.EnemyHealth} energy={battle.Energy}");
            if(cue!=GameCue.EnemyAttack||!MonsterRayMotion.Variant(battle.EnemyAttackCount))sound.Cue(cue,battle.Phase);
            world.Cue(cue,battle);
            switch(cue)
            {
                case GameCue.BattleStart:caption="挥动拳头，守护火山基地！";battleStartCueAt=Time.unscaledTime;hintAt=Time.unscaledTime+12;break;
                case GameCue.Block:caption="挡住了！护盾成功";break;
                case GameCue.Hurt:caption="没关系，力量正在恢复";break;
                case GameCue.EnergyReady:caption="能量满了 · 双手向前推，停一下";break;
                case GameCue.Beam:caption=SelectedHero.Beam+"！";break;
                case GameCue.Victory:victoryAt=Time.unscaledTime;if(Debug.isDebugBuild)Debug.Log($"[FinalStrike] completed action={battle.Action} actionAge={battle.ActionAge:F3}");break;
                case GameCue.Resume:caption="准备好了，继续！";break;
                default:return;
            }
            captionUntil=Mathf.Max(captionUntil,Time.unscaledTime+2.5f);
        }
        void Restart()
        {
            photo?.Close();autoPhotoOpened=finalGuide=false;waitingGuideAt=Time.unscaledTime+18;
            battle=new Battle(monsterHits);recognizer.Reset();presence.Reset();pose=null;held=default;
            // A photo round can leave the native worker on the same camera stream.
            // Reset the envelope cursor so the first frames of the new round are
            // always eligible to re-arm the raised-hands transform gesture.
            stream=null;sequence=0;lastTracking=false;paused=settings=showcase=false;
            lastHealth=enemyHealthDisplay=battle.MaxHealth;impact=0;captionUntil=0;battleStartCueAt=-1;hitUntil=0;damagePopAt=0;lastDamage=0;gestureFeedbackUntil=0;hintAt=Time.unscaledTime+12;beamHelpAt=Time.unscaledTime+6;sound.Reset();world.ResetPresentation();
            presentedPunches=presentedHits=comboCount=0;comboUntil=0;
            if(!keyboard)sound.Speak("arcade_ready",1,GamePhase.Waiting);
        }
        void OpenSettings(bool audio=false)
        {draftMonsterHits=monsterHits;draftDifficulty=recognizer.Difficulty;draftResolution=DisplayPreferences.Quality;
         draftPhotoAi=PlayerPrefs.GetInt("photo.ai",1)==1;audioSettings=audio;videoSettings=false;settings=true;}
        void CloseSettings(){settings=false;photo.ResumeGuidance();recognizer.Reset();held=default;}
        void ApplySettings()
        {
            sound.Save();recognizer.Difficulty=draftDifficulty;
            PlayerPrefs.SetInt("gesture.difficulty",draftDifficulty);PlayerPrefs.SetInt("photo.ai",draftPhotoAi?1:0);
            if(DisplayPreferences.Quality!=draftResolution)DisplayPreferences.Apply(draftResolution);
            PlayerPrefs.SetInt("display.resolution",draftResolution);PlayerPrefs.Save();CloseSettings();
            if(draftMonsterHits==monsterHits)return;
            monsterHits=Battle.ClampMonsterHits(draftMonsterHits);PlayerPrefs.SetInt(MonsterHitsKey,monsterHits);PlayerPrefs.Save();
            if(Debug.isDebugBuild)Debug.Log($"[Settings] monsterHits={monsterHits} energyPunches={Battle.MaxEnergy}");
            Restart();
        }
        void SetMode(bool value)
        {
            if(keyboard==value)return;
            keyboard=value;photoAvailable=!keyboard||Array.IndexOf(Environment.GetCommandLineArgs(),"--photo-port")>=0;
            photo.KeyboardMode=keyboard;
            // Only replace the input source: keep the round, selected hero and
            // any open settings/photo review, while discarding stale gestures.
            recognizer.Reset();presence.Reset();held=default;pose=null;stream=null;sequence=0;
            lastTracking=false;gestureFeedbackUntil=0;cursorUntil=Time.unscaledTime+3;
            if(battle.Phase==GamePhase.Waiting&&!photo.Active)
            {sound.Reset();if(!keyboard&&!settings)sound.Speak("arcade_ready",1,GamePhase.Waiting);}
            if(Debug.isDebugBuild)Debug.Log($"[InputMode] mode={(keyboard?"keyboard":"camera")} phase={battle.Phase} health={battle.EnemyHealth} energy={battle.Energy} hero={SelectedHero.Id}");
        }
        void ReadPhotoPose()
        {
            var line=client.TakeLatest();
            if(line==null)return;
            try {pose=JsonUtility.FromJson<PoseFrame>(line);}
            catch(ArgumentException) {pose=null;}
        }
        void UpdatePreview(long now)
        {
            var incoming=previewClient.TakeLatest();
            if((keyboard&&!photo.Active)||!showPreview)
            {previewFrame=null;if(previewTexture){Destroy(previewTexture);previewTexture=null;}return;}
            if(incoming!=null&&incoming.Fresh(now))
            {
                if(!previewTexture)previewTexture=new Texture2D(2,2,TextureFormat.RGB24,false);
                if(previewTexture.LoadImage(incoming.Jpeg))
                {
                    previewFrame=incoming;
                    if(!previewReported&&Debug.isDebugBuild)
                    {Debug.Log($"[Preview] displayed source={(incoming.Synthetic?"synthetic":"camera")} size={previewTexture.width}x{previewTexture.height}");previewReported=true;}
                }
                else previewFrame=null;
            }
            if(previewFrame==null||!previewFrame.Fresh(now))
            {previewFrame=null;if(previewTexture){Destroy(previewTexture);previewTexture=null;}}
        }
        void OnGUI()
        {
            if(hud==null)return;
            GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(Screen.width/1280f,Screen.height/720f,1));
            if(settings){DrawSettings();return;}
            if(photo.Active)
            {
                GUI.matrix=Matrix4x4.identity;hud.Box(new Rect(0,0,Screen.width,Screen.height),new Color(.012f,.025f,.05f));
                float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
                GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)/2,(Screen.height-720*scale)/2,0),Quaternion.identity,new Vector3(scale,scale,1));
                photo.Draw(hud);DrawSettingsEntry();return;
            }
            if(showcase)
            {
                hud.Box(new Rect(0,0,1280,94),new Color(.012f,.025f,.06f,.92f));
                hud.Text(new Rect(34,18,800,43),"角色展示 · "+SelectedHero.Name+"与哥尔赞",28,HudPainter.Ink,bold:true);
                hud.Text(new Rect(36,63,900,24),SelectedHero.Name+" · "+(showcaseFrame==4?SelectedHero.Beam:AnimatedActor.HeroPoses[showcaseFrame])+"    /    哥尔赞 · "+AnimatedActor.MonsterPoses[showcaseFrame],17,HudPainter.Cyan);
                if(hero.IsRigged)hud.Text(new Rect(844,23,400,34),SelectedHero.Id=="Tiga"?"迪迦：Extrazhang · BlendSwap · CC-BY-NC":SelectedHero.Id=="Geed"||SelectedHero.Id=="Grigio"?SelectedHero.Name+"：RayNoGame · GTAall":SelectedHero.Name+"：TengenGenesic / ultimo · SFMLab",12,HudPainter.Muted);
                if(enemy.IsRigged)hud.Text(new Rect(844,48,400,28),"哥尔赞：TengenGenesic / ultimo · SFMLab",12,HudPainter.Muted);
                hud.Box(new Rect(0,648,1280,72),new Color(.012f,.025f,.06f,.95f));
                if(hud.Button(new Rect(36,665,144,37),"上个动作"))showcaseFrame=(showcaseFrame+7)%8;
                if(hud.Button(new Rect(192,665,144,37),"下个动作"))showcaseFrame=(showcaseFrame+1)%8;
                hud.Text(new Rect(364,665,580,37),$"{showcaseFrame+1} / 8    ← / → 切换动作 · F5 返回游戏",17,HudPainter.Muted);
                if(hud.Button(new Rect(1050,665,192,37),"返回游戏 · F5"))showcase=false;
                return;
            }
            if(world.Closeup.Active) {DrawBeamCloseup();DrawSettingsEntry();return;}
            DrawArcadeHud();DrawSettingsEntry();DrawKeyboardActions();
        }
        void DrawBeamCloseup()
        {
            float focus=world.Closeup.Focus;
            float band=Mathf.Lerp(18,66,focus);
            hud.Box(new Rect(0,0,1280,band),new Color(.006f,.016f,.04f,.96f));
            hud.Box(new Rect(0,720-band,1280,band),new Color(.006f,.016f,.04f,.96f));
            hud.Box(new Rect(0,band,1280,2),new Color(.25f,.78f,1,.65f*focus));
            hud.Box(new Rect(0,718-band,1280,2),new Color(.25f,.78f,1,.65f*focus));
            // Peripheral speed lines leave the enlarged head and L-shaped hands unobstructed.
            for(int i=0;i<6;i++)
            {
                float y=145+i*72,offset=Mathf.Repeat(world.Closeup.Age*210+i*31,95);
                var color=new Color(.48f,.83f,1,focus*(.08f+(i%3)*.025f));
                hud.Line(new Vector2(-offset,y),new Vector2(115+i*17-offset,y-12),color,2);
                hud.Line(new Vector2(1165+offset,y-12),new Vector2(1320+offset,y),color,2);
            }
            hud.Text(new Rect(140,720-band,1000,band),SelectedHero.Beam+"！",32,new Color(1,.82f,.47f,focus),TextAnchor.MiddleCenter,true);
        }
        void DrawSettings()
        {
            hud.Box(new Rect(0,0,1280,661),new Color(.005f,.01f,.03f,.72f));hud.Panel(new Rect(355,115,570,521),HudPainter.Cyan);
            hud.Text(new Rect(385,134,500,40),"游戏设置",27,HudPainter.Ink,bold:true);
            DrawModeSwitch(new Rect(705,138,189,34),14);
            if(hud.Button(new Rect(386,184,160,36),"战斗与动作",!audioSettings&&!videoSettings?HudPainter.Cyan:HudPainter.Muted)){audioSettings=videoSettings=false;}
            if(hud.Button(new Rect(560,184,160,36),"声音与音乐",audioSettings?HudPainter.Cyan:HudPainter.Muted)){audioSettings=true;videoSettings=false;}
            if(hud.Button(new Rect(734,184,160,36),"画面与合照",videoSettings?HudPainter.Cyan:HudPainter.Muted)){videoSettings=true;audioSettings=false;}
            if(videoSettings)DrawDisplaySettings();else if(audioSettings)DrawAudioSettings();else
            {
                hud.Text(new Rect(386,241,508,28),"怪兽血量 · 可承受的普攻次数",20,HudPainter.Ink,bold:true);
                if(hud.Button(new Rect(386,289,64,42),"−10"))draftMonsterHits=Battle.ClampMonsterHits(draftMonsterHits-10);
                hud.Text(new Rect(467,284,348,52),$"{draftMonsterHits} 次普攻",31,HudPainter.Gold,TextAnchor.MiddleCenter,true);
                if(hud.Button(new Rect(830,289,64,42),"+10"))draftMonsterHits=Battle.ClampMonsterHits(draftMonsterHits+10);
                draftMonsterHits=Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(400,356,480,24),draftMonsterHits,Battle.MinMonsterHits,Battle.MaxMonsterHits)/10)*10;
                hud.Text(new Rect(386,382,320,30),$"{Battle.MinMonsterHits}–{Battle.MaxMonsterHits} 次 · 每次调整 10",14,HudPainter.Muted);
                if(hud.Button(new Rect(760,382,134,30),"恢复默认 50"))draftMonsterHits=Battle.DefaultMonsterHits;
                hud.Text(new Rect(386,429,508,25),"动作门槛 · 默认宽松，优先保证孩子能做出来",17,HudPainter.Cyan);
                for(int i=0;i<3;i++)if(hud.Button(new Rect(386+i*174,466,160,34),new[]{"宽松","标准","挑战"}[i],draftDifficulty==i?HudPainter.Gold:HudPainter.Muted,15))draftDifficulty=i;
                hud.Text(new Rect(386,516,508,28),$"大招需 {Battle.MaxEnergy} 次普攻蓄满 · 更换血量会开始新一局",14,HudPainter.Muted);
            }
            bool changed=draftMonsterHits!=monsterHits;
            if(hud.Button(new Rect(386,567,508,40),changed?"应用并开始新一局":"保存并返回"))
                ApplySettings();
        }
        void DrawSettingsEntry()
        {
            // Keyboard shortcuts remain available for a parent, but the child
            // should see a clean playfield in guided camera mode. Showing F2/F4
            // beside every instruction made the game look like a desktop tool
            // and suggested that mouse/keyboard input was required.
            if(!keyboard)
            {
                // Keep a visible parent control in guided mode as well.  F2
                // remains a shortcut, but a child-facing camera session must
                // not strand the parent in pose input with no way back to the
                // keyboard practice mode or settings screen.
                hud.Rounded(new Rect(18,640,335,60),new Color(.006f,.025f,.053f,.86f),7);
                hud.Text(new Rect(30,645,310,17),"输入模式 · 摄像头体感",11,HudPainter.Cyan,bold:true);
                if(hud.Button(new Rect(28,667,156,27),"切换按键 · F2",HudPainter.Cyan,12))SetMode(true);
                if(hud.Button(new Rect(191,667,145,27),"设置 · F4",HudPainter.Muted,12))OpenSettings();
                return;
            }
            // Keep the parent controls visible in keyboard practice. This
            // compact status strip does not cover either fighter.
            var modeAccent=keyboard?HudPainter.Gold:HudPainter.Cyan;
            hud.Rounded(new Rect(18,640,335,60),new Color(.006f,.025f,.053f,.86f),7);
            hud.Text(new Rect(30,645,310,17),keyboard?"输入模式 · 按键练习":"输入模式 · 摄像头体感",11,modeAccent,bold:true);
            if(hud.Button(new Rect(28,667,156,27),keyboard?"切换体感 · F2":"切换按键 · F2",modeAccent,12))SetMode(!keyboard);
            if(hud.Button(new Rect(191,667,145,27),"设置 · F4",HudPainter.Muted,12))OpenSettings();
        }
        void DrawModeSwitch(Rect rect,int size)
        {if(hud.Button(rect,keyboard?"切换体感 · F2":"切换按键 · F2",HudPainter.Cyan,size))SetMode(!keyboard);}
        void DrawDisplaySettings()
        {
            hud.Text(new Rect(386,239,508,30),"画面分辨率 · 目标 60 FPS",21,HudPainter.Ink,bold:true);
            for(int i=0;i<3;i++)if(hud.Button(new Rect(386,280+i*46,508,36),DisplayPreferences.Label(i),draftResolution==i?HudPainter.Gold:HudPainter.Muted,18))draftResolution=i;
            hud.Text(new Rect(386,423,508,26),$"当前画面 {Screen.width} × {Screen.height} · 60 FPS 上限",14,HudPainter.Muted);
            if(hud.Button(new Rect(386,462,508,35),draftPhotoAi?"合照 AI 光色优化：开":"合照 AI 光色优化：关",draftPhotoAi?HudPainter.Cyan:HudPainter.Muted,17))draftPhotoAi=!draftPhotoAi;
            hud.Text(new Rect(386,510,508,40),"原图先保存，AI 版本完成后另存；不耽误下一局。\n2K 对显卡要求更高，可随时切回 1080P。",14,HudPainter.Muted);
        }
        void DrawAudioSettings()
        {
            hud.Text(new Rect(386,236,508,36),music.SelectedName,23,HudPainter.Gold,bold:true);
            hud.Text(new Rect(386,276,508,32),music.Status,14,HudPainter.Muted);
            GUI.enabled=!music.Loading&&!music.Choosing;
            if(hud.Button(new Rect(386,315,276,38),"导入音乐 · F6",HudPainter.Cyan))music.Choose();
            if(hud.Button(new Rect(677,315,216,38),"恢复内置配乐"))music.BuiltIn();
            GUI.enabled=true;
            hud.Text(new Rect(386,360,225,25),"总音量  "+Mathf.RoundToInt(sound.Volume*100)+"%",17);
            sound.Volume=GUI.HorizontalSlider(new Rect(633,368,256,20),sound.Volume,0,1);
            hud.Text(new Rect(386,405,225,25),"音乐音量  "+Mathf.RoundToInt(sound.MusicVolume*100)+"%",17);
            sound.MusicVolume=GUI.HorizontalSlider(new Rect(633,413,256,20),sound.MusicVolume,0,1);
            if(hud.Button(new Rect(386,454,156,34),sound.MusicEnabled?"音乐：开":"音乐：关"))sound.MusicEnabled=!sound.MusicEnabled;
            if(hud.Button(new Rect(560,454,157,34),muted?"恢复声音":"全部静音"))muted=!muted;
            if(hud.Button(new Rect(735,454,158,34),"全屏 · F11"))Screen.fullScreen=!Screen.fullScreen;
            hud.Text(new Rect(386,510,506,34),"支持 MP3 / WAV / OGG / AIFF · 选择后会自动记住\nF3 取景 · F4 设置 · F5 角色动作 · Esc 返回",13,HudPainter.Muted);
        }
        void OnDestroy()
        {Cursor.visible=true;photo?.Dispose();client?.Dispose();previewClient?.Dispose();if(previewTexture)Destroy(previewTexture);hud?.Dispose();sound?.Save();}
    }
}
