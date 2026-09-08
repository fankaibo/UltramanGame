using System;
using UnityEngine;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    public sealed class ArenaController : MonoBehaviour
    {
        Battle battle=new Battle();
        readonly Battle showcaseBattle=new Battle();
        bool showcase;int showcaseFrame;
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
        bool photoAvailable;
        bool keyboard,paused,muted,settings,audioSettings,showPreview=true,previewReported,lastTracking;
        const string MonsterHitsKey="battle.monsterHits";
        int monsterHits=Battle.DefaultMonsterHits,draftMonsterHits=Battle.DefaultMonsterHits;
        string caption="",stream,gestureFeedback="";
        float gestureFeedbackUntil;
        long sequence;
        float beamTitleUntil,captionUntil,lastHealth=Battle.DefaultMonsterHits,impact,phaseStarted,hintAt=12,hitUntil,beamHelpAt;
        GamePhase lastPhase;
        float sampleAge,sampleSeconds,sampleWorst;
        int sampleFrames;
        int sampleWindows;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        { if(FindFirstObjectByType<ArenaController>()==null)new GameObject("UltramanGame").AddComponent<ArenaController>(); }
        static int LocalPort(string option,int fallback)
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,option);
            return i>=0&&i+1<args.Length&&int.TryParse(args[i+1],out int port)&&port>0&&port<=65535?port:fallback;
        }
        void Start()
        {
            monsterHits=Battle.ClampMonsterHits(PlayerPrefs.GetInt(MonsterHitsKey,Battle.DefaultMonsterHits));
            battle=new Battle(monsterHits);lastHealth=battle.MaxHealth;
            QualitySettings.vSyncCount=0;Application.targetFrameRate=60;
            keyboard=Array.IndexOf(Environment.GetCommandLineArgs(),"--keyboard")>=0;
            Application.runInBackground=true;Screen.sleepTimeout=SleepTimeout.NeverSleep;
            client=new PoseClient(LocalPort("--pose-port",8765));previewClient=new PreviewClient(LocalPort("--preview-port",8766));
            var font=Resources.Load<Font>("Fonts/NotoSansSC-Regular");
            if(!font)font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hud=new HudPainter(font);world=new GameWorld();sound=new GameAudio(gameObject);
            photoAvailable=!keyboard||Array.IndexOf(Environment.GetCommandLineArgs(),"--photo-port")>=0;
            photo=new VictoryPhoto(LocalPort("--photo-port",8767));
            hero=new AnimatedActor("Tiga",world.HeroHome,world.EnemyHome);enemy=new AnimatedActor("Golza",world.EnemyHome,world.HeroHome,true);
            music=gameObject.AddComponent<LocalMusic>();music.Initialize(sound);
            if(!keyboard)sound.Speak("welcome",1,GamePhase.Waiting);
        }
        void Update()
        {
            if(photo.Active)
            {
                if(Input.GetKeyDown(KeyCode.Escape))photo.Close();
                if(Input.GetKeyDown(KeyCode.F11))Screen.fullScreen=!Screen.fullScreen;
                photo.Tick(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());sound.Tick(GamePhase.Victory,muted,Time.unscaledDeltaTime);return;
            }
            if(Input.GetKeyDown(KeyCode.F7)&&battle.Phase==GamePhase.Victory&&photoAvailable) {photo.Open();return;}
            if(Input.GetKeyDown(KeyCode.F2))SetMode(!keyboard);
            if(Input.GetKeyDown(KeyCode.F3))showPreview=!showPreview;
            if(Input.GetKeyDown(KeyCode.F4)) {if(settings)settings=false;else OpenSettings();}
            if(Input.GetKeyDown(KeyCode.F5))showcase=!showcase;
            if(showcase)showcaseFrame=(showcaseFrame+8+(Input.GetKeyDown(KeyCode.RightArrow)?1:0)-(Input.GetKeyDown(KeyCode.LeftArrow)?1:0))%8;
            if(Input.GetKeyDown(KeyCode.F6)) {showcase=false;OpenSettings(true);music.Choose();}
            if(settings&&!audioSettings)
            {
                if(Input.GetKeyDown(KeyCode.LeftArrow))draftMonsterHits=Battle.ClampMonsterHits(draftMonsterHits-10);
                if(Input.GetKeyDown(KeyCode.RightArrow))draftMonsterHits=Battle.ClampMonsterHits(draftMonsterHits+10);
                if(Input.GetKeyDown(KeyCode.Return))ApplySettings();
            }
            if(Input.GetKeyDown(KeyCode.F11))Screen.fullScreen=!Screen.fullScreen;
            if(Input.GetKeyDown(KeyCode.Escape)) { if(showcase)showcase=false;else if(settings)settings=false;else paused=!paused; }
            if(Input.GetKeyDown(KeyCode.R))Restart();
            long now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();float dt=Time.unscaledDeltaTime;
            sampleAge+=dt;
            if(Debug.isDebugBuild&&sampleWindows<2&&sampleAge>3)
            {
                sampleSeconds+=dt;sampleFrames++;sampleWorst=Mathf.Max(sampleWorst,dt);
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
                            input=recognizer.Update(pose,now,battle.Phase==GamePhase.Battle&&battle.Energy>=Battle.MaxEnergy,battle.Phase==GamePhase.Waiting);
                            string detected=input.Beam?"必杀光线":input.Transform?"举手变身":input.LeftPunch||input.RightPunch?
                                (recognizer.ForwardPunch?"向前挥拳":"侧前挥拳"):"";
                            if(detected.Length>0)
                            {
                                gestureFeedback="已识别："+detected;gestureFeedbackUntil=Time.unscaledTime+1.3f;
                                if(Debug.isDebugBuild)Debug.Log("[Gesture] "+detected);
                            }
                        }
                    }
                    catch(ArgumentException) {pose=null;recognizer.Reset();input=default;}
                }
                if(!PoseQuality.Present(pose,now)) {input=default;recognizer.Reset();}
                input.Tracking=presence.Update(pose,now);held=input;
            }
            if(paused||settings||showcase||music.Choosing)input.Tracking=false;
            if(input.Tracking!=lastTracking)
            { lastTracking=input.Tracking;if(Debug.isDebugBuild)Debug.Log($"[Input] tracking={lastTracking} mode={(keyboard?"keyboard":pose?.source??"camera")}"); }
            battle.Tick(world.Closeup.Active?0:dt,input);
            if(battle.Phase==GamePhase.Paused)beamTitleUntil=0;
            if(battle.Phase!=lastPhase) {phaseStarted=Time.unscaledTime;lastPhase=battle.Phase;}
            sound.Tick(paused||settings||showcase?GamePhase.Paused:battle.Phase,muted,dt);
            while(battle.TryCue(out var cue))PlayCue(cue);
            if(battle.EnemyHealth<lastHealth)
            {
                bool special=lastHealth-battle.EnemyHealth>1;impact=special?.35f:.2f;hitUntil=Time.unscaledTime+1;
                world.Hit(special,battle);sound.Effect("impact",special?1:.8f);
            }
            lastHealth=battle.EnemyHealth;impact=Mathf.Max(0,impact-dt);
            world.Showcase=showcase;
            world.Tick(showcase?showcaseBattle:battle,dt,Time.unscaledTime);
            if(world.BeamStarted)sound.Effect("beam",sound.HasOriginalBeamVoice?.4f:.7f);
            hero.Update(showcase?showcaseBattle:battle,world.Camera,dt,Time.unscaledTime,showcase?showcaseFrame:-1);
            enemy.Update(showcase?showcaseBattle:battle,world.Camera,dt,Time.unscaledTime,showcase?showcaseFrame:-1);
            enemy.SetPresentationOpacity(1-world.Closeup.Focus);
            if(!keyboard&&!paused&&!settings&&!showcase&&!world.Closeup.Active&&battle.Phase==GamePhase.Battle)
            {
                if(battle.Punches==0&&Time.unscaledTime>hintAt)
                {sound.Speak("tutorial",1,battle.Phase);caption="先把手收回来，再挥出去";captionUntil=Time.unscaledTime+3;hintAt=Time.unscaledTime+20;}
                if(battle.Energy<Battle.MaxEnergy)beamHelpAt=Time.unscaledTime+6;
                else if(Time.unscaledTime>beamHelpAt)
                {sound.Speak("beam_help",2,battle.Phase);beamHelpAt=Time.unscaledTime+22;}
            }
        }
        void PlayCue(GameCue cue)
        {
            if(Debug.isDebugBuild)Debug.Log($"[Game] cue={cue} phase={battle.Phase} health={battle.EnemyHealth} energy={battle.Energy}");
            sound.Cue(cue,battle.Phase);world.Cue(cue);
            switch(cue)
            {
                case GameCue.BattleStart:caption="挥动拳头，守护这座城市！";hintAt=Time.unscaledTime+12;break;
                case GameCue.Block:caption="挡住了！护盾成功";break;
                case GameCue.Hurt:caption="没关系，力量正在恢复";break;
                case GameCue.EnergyReady:caption="能量满了！摆出光线姿势";break;
                case GameCue.Beam:caption="哉佩利敖光线！";beamTitleUntil=Time.unscaledTime+BeamCloseup.Duration+2;break;
                case GameCue.Resume:caption="准备好了，继续！";break;
                default:return;
            }
            captionUntil=Time.unscaledTime+2.5f;
        }
        void Restart()
        {
            photo?.Close();
            battle=new Battle(monsterHits);recognizer.Reset();presence.Reset();pose=null;held=default;paused=settings=showcase=false;
            lastHealth=battle.MaxHealth;impact=0;captionUntil=0;beamTitleUntil=0;hitUntil=0;gestureFeedbackUntil=0;hintAt=Time.unscaledTime+12;beamHelpAt=Time.unscaledTime+6;sound.Reset();world.Closeup.Cancel();
        }
        void OpenSettings(bool audio=false)
        {draftMonsterHits=monsterHits;audioSettings=audio;settings=true;}
        void ApplySettings()
        {
            sound.Save();settings=false;
            if(draftMonsterHits==monsterHits)return;
            monsterHits=Battle.ClampMonsterHits(draftMonsterHits);PlayerPrefs.SetInt(MonsterHitsKey,monsterHits);PlayerPrefs.Save();
            if(Debug.isDebugBuild)Debug.Log($"[Settings] monsterHits={monsterHits} energyPunches={Battle.MaxEnergy}");
            Restart();
        }
        void SetMode(bool value) {keyboard=value;Restart();}
        void UpdatePreview(long now)
        {
            var incoming=previewClient.TakeLatest();
            if(keyboard||!showPreview)
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
        void DrawPreview(bool compact=false)
        {
            if(keyboard)return;
            if(!showPreview)
            {if(hud.Button(compact?new Rect(1108,639,152,31):new Rect(1083,606,169,34),"显示取景 · F3",size:compact?12:16))showPreview=true;return;}
            var panel=compact?new Rect(1056,480,204,195):new Rect(992,389,260,253);
            var picture=compact?new Rect(1063,508,190,142.5f):new Rect(1000,430,244,183);
            var feedbackRect=compact?new Rect(1065,651,190,20):new Rect(1005,615,240,22);
            hud.Panel(panel,HudPainter.Cyan);
            hud.Dot(new Vector2(panel.x+12,panel.y+17),compact?4:6,previewFrame!=null?HudPainter.Cyan:HudPainter.Gold);
            hud.Text(new Rect(panel.x+23,panel.y+5,panel.width-65,25),previewFrame?.Synthetic==true||pose?.source=="synthetic"?"合成测试 · 非摄像头":"镜像取景",compact?11:14);
            if(hud.Button(new Rect(panel.xMax-43,panel.y+7,36,22),"收起",size:compact?11:14))showPreview=false;
            hud.Box(picture,Color.black);
            if(previewFrame!=null&&previewFrame.Fresh(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())&&previewTexture)
            {
                GUI.DrawTexture(picture,previewTexture,ScaleMode.ScaleToFit);
                int quality=previewFrame.Quality;
                bool feedback=Time.unscaledTime<gestureFeedbackUntil;
                hud.Text(feedbackRect,feedback?gestureFeedback:quality==2?(compact?"双臂清晰 · 准备就绪":"双臂已看清 · 动作准备就绪"):quality==1?(compact?"手腕可前推 · 肘部可遮挡":"看清手腕可前推 · 肘部可遮挡"):"请让双肩进入画面",compact?11:13,feedback||quality==2?HudPainter.Cyan:HudPainter.Gold);
            }
            else
            {
                hud.Text(new Rect(picture.x+8,picture.y+picture.height*.3f,picture.width-16,70),"画面暂未更新\n"+previewClient.Status,compact?12:15,HudPainter.Muted,TextAnchor.MiddleCenter);
                hud.Text(feedbackRect,"仅本机处理 · 不录制",compact?11:13,HudPainter.Muted);
            }
        }
        void ActionCard(float x,string title,string hint,string gesture,Color accent,bool active)
        {
            var r=new Rect(x,529,298,110);hud.Panel(r,accent,active);
            hud.Figure(new Rect(x+13,544,64,78),gesture,Time.unscaledTime,accent);
            hud.Text(new Rect(x+91,543,192,32),title,23,active?accent:HudPainter.Ink,bold:true);
            hud.Text(new Rect(x+92,584,191,35),hint,15,HudPainter.Muted);
        }
        void OnGUI()
        {
            if(hud==null)return;
            GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(Screen.width/1280f,Screen.height/720f,1));
            if(photo.Active)
            {
                GUI.matrix=Matrix4x4.identity;hud.Box(new Rect(0,0,Screen.width,Screen.height),new Color(.012f,.025f,.05f));
                float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
                GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)/2,(Screen.height-720*scale)/2,0),Quaternion.identity,new Vector3(scale,scale,1));
                photo.Draw(hud);return;
            }
            // Give modal controls a stable event path; do not submit background buttons while it is open.
            if(settings) {DrawSettings();return;}
            if(showcase)
            {
                hud.Box(new Rect(0,0,1280,94),new Color(.012f,.025f,.06f,.92f));
                hud.Text(new Rect(34,18,800,43),"角色展示 · 迪迦与哥尔赞",28,HudPainter.Ink,bold:true);
                hud.Text(new Rect(36,63,900,24),"迪迦 · "+AnimatedActor.HeroPoses[showcaseFrame]+"    /    哥尔赞 · "+AnimatedActor.MonsterPoses[showcaseFrame],17,HudPainter.Cyan);
                hud.Box(new Rect(0,648,1280,72),new Color(.012f,.025f,.06f,.95f));
                if(hud.Button(new Rect(36,665,144,37),"上个动作"))showcaseFrame=(showcaseFrame+7)%8;
                if(hud.Button(new Rect(192,665,144,37),"下个动作"))showcaseFrame=(showcaseFrame+1)%8;
                hud.Text(new Rect(364,665,580,37),$"{showcaseFrame+1} / 8    ← / → 切换动作 · F5 返回游戏",17,HudPainter.Muted);
                if(hud.Button(new Rect(1050,665,192,37),"返回游戏 · F5"))showcase=false;
                return;
            }
            if(world.Closeup.Active) {DrawBeamCloseup();return;}
            if(battle.Phase==GamePhase.Battle||battle.Phase==GamePhase.Paused||battle.Phase==GamePhase.Victory)
            {DrawBattleHud();return;}
            long now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();float time=Time.unscaledTime;
            hud.Box(new Rect(0,0,1280,103),new Color(.012f,.025f,.06f,.91f));hud.Box(new Rect(28,101,1224,1),new Color(.25f,.53f,.8f,.25f));
            hud.Dot(new Vector2(47,47),33,new Color(.18f,.4f,.62f));hud.Dot(new Vector2(47,47),13,HudPainter.Cyan);
            hud.Text(new Rect(78,23,410,41),"迪迦 · 光之训练场",29,HudPainter.Ink,bold:true);
            hud.Text(new Rect(80,66,390,22),"CITY OF LIGHT  /  一起守护城市",12,HudPainter.Muted);
            string stage=battle.Phase==GamePhase.Waiting?"准备出发":battle.Phase==GamePhase.Transforming?"光之变身":battle.Phase==GamePhase.Victory?"守护成功":battle.Phase==GamePhase.Paused?"休息一下":"城市守护";
            hud.Rounded(new Rect(526,30,166,35),new Color(.14f,.37f,.46f,.48f));hud.Text(new Rect(526,30,166,35),stage,17,HudPainter.Cyan,TextAnchor.MiddleCenter);
            hud.Text(new Rect(801,20,250,26),"哥尔赞 · 训练对手",17,HudPainter.Ink);
            hud.Text(new Rect(1077,20,171,26),$"{Mathf.CeilToInt(battle.EnemyHealth)} / {battle.MaxHealth}",15,HudPainter.Muted,TextAnchor.MiddleRight);
            hud.Bar(new Rect(803,56,445,9),battle.EnemyHealth/battle.MaxHealth,new Color(.93f,.49f,.34f));
            hud.Text(new Rect(803,72,440,20),keyboard?"键盘练习":pose?.source=="synthetic"?"合成动作测试 · 不使用摄像头":"摄像头体感 · 家庭训练",12,HudPainter.Muted,TextAnchor.MiddleRight);
            hud.Panel(new Rect(28,122,263,65),HudPainter.Gold,battle.Energy>=Battle.MaxEnergy);
            hud.Text(new Rect(42,130,235,20),battle.Energy>=Battle.MaxEnergy?"光线已就绪！":$"光线能量  {battle.Energy:0} / {Battle.MaxEnergy}",14,battle.Energy>=Battle.MaxEnergy?HudPainter.Gold:HudPainter.Muted);
            for(int i=0;i<Battle.MaxEnergy;i++)hud.Rounded(new Rect(43+i*15,160,11,8),i<battle.Energy?HudPainter.Gold:new Color(.14f,.22f,.32f),4);
            if(battle.Phase==GamePhase.Waiting||battle.Phase==GamePhase.Transforming)
            {
                bool waiting=battle.Phase==GamePhase.Waiting;
                hud.Panel(new Rect(28,209,330,289),HudPainter.Cyan,true);
                hud.Text(new Rect(48,225,290,24),waiting?"01  /  唤醒光的力量":"光的力量，正在苏醒",14,HudPainter.Cyan);
                hud.Text(new Rect(48,259,290,43),waiting?(keyboard?"按空格，变身！":"双手举高，变身！"):"迪迦，出发！",27,HudPainter.Ink,bold:true);
                hud.Figure(new Rect(52,321,108,110),"transform",time,HudPainter.Gold);
                hud.Text(new Rect(180,323,157,100),keyboard?"空格开始\nA / D 挥拳":waiting?"像左边一样\n把双手举高\n保持一小会儿":"你就是\n守护城市的英雄",17,HudPainter.Muted);
                float progress=waiting?(keyboard?0:recognizer.TransformProgress):Mathf.Clamp01((time-phaseStarted)/2.2f);
                hud.Bar(new Rect(49,453,289,7),progress,HudPainter.Cyan);
                hud.Text(new Rect(49,466,289,22),waiting?(PoseQuality.Present(pose,now)||keyboard?"站稳，慢慢来就可以":"先让肩膀和双手进入画面"):"光之能量充能中",12,HudPainter.Muted);
            }
            ActionCard(28,"挥拳出击",keyboard?"A / D · 左右交替":$"收手，再挥出去  ·  命中 {battle.Punches}","punch",HudPainter.Cyan,battle.Phase==GamePhase.Battle&&(battle.Action==HeroAction.LeftPunch||battle.Action==HeroAction.RightPunch));
            ActionCard(348,"光之护盾",keyboard?"按住 S 防御":"双手护住胸前","shield",HudPainter.Violet,battle.Shield);
            ActionCard(668,"必杀光线",keyboard?"能量满后按 J":battle.Energy>=Battle.MaxEnergy?"摆 L 形 / 双手前推":$"普攻命中 {Battle.MaxEnergy} 次蓄满","beam",HudPainter.Gold,battle.Phase==GamePhase.Battle&&(battle.Energy>=Battle.MaxEnergy||battle.Action==HeroAction.Beam));
            if(!keyboard&&battle.Energy>=Battle.MaxEnergy)hud.Bar(new Rect(764,623,180,4),recognizer.BeamProgress,HudPainter.Gold);
            DrawPreview();
            DrawFooter(false);
        }
        void DrawFooter(bool compact)
        {
            long now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            float y=compact?689:673,height=compact?26:34;
            int fontSize=compact?12:16;
            hud.Box(new Rect(0,compact?683:661,1280,compact?37:59),new Color(.014f,.027f,.055f,.92f));
            string status=keyboard?"空格变身 · A/D挥拳 · S防御 · J光线":PoseQuality.Present(pose,now)?
                (PoseQuality.Valid(pose,now)?"已经看见你 · 尽情挥动双手":"你还在画面中 · 可把手臂移进镜头"):
                "让肩膀进入画面 · "+client.Status;
            hud.Dot(new Vector2(28,y+height/2),compact?5:7,keyboard||PoseQuality.Present(pose,now)?HudPainter.Cyan:HudPainter.Gold);
            hud.Text(new Rect(40,y,570,height),status,compact?11:14,HudPainter.Muted);
            if(hud.Button(new Rect(640,y,116,height),"角色 · F5",size:fontSize))showcase=true;
            if(hud.Button(new Rect(770,y,130,height),keyboard?"切回体感":"键盘练习",size:fontSize))SetMode(!keyboard);
            if(hud.Button(new Rect(912,y,108,height),"游戏设置",size:fontSize))OpenSettings();
            if(hud.Button(new Rect(1032,y,100,height),paused?"继续":"暂停",size:fontSize))paused=!paused;
            if(hud.Button(new Rect(1144,y,108,height),"重新开始",size:fontSize))Restart();
        }
        void BattleAction(float x,string title,string hint,string gesture,Color accent,bool active)
        {
            hud.Panel(new Rect(x,620,212,57),accent,active);
            hud.Figure(new Rect(x+10,627,36,44),gesture,Time.unscaledTime,accent);
            hud.Text(new Rect(x+55,625,150,21),title,15,active?accent:HudPainter.Ink,bold:true);
            hud.Text(new Rect(x+55,648,150,23),hint,11,HudPainter.Muted);
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
            hud.Text(new Rect(140,720-band,1000,band),"哉佩利敖光线！",32,new Color(1,.82f,.47f,focus),TextAnchor.MiddleCenter,true);
        }
        void DrawBattleHud()
        {
            float time=Time.unscaledTime;
            bool ready=battle.Energy>=Battle.MaxEnergy;
            hud.Box(new Rect(0,0,1280,62),new Color(.012f,.025f,.06f,.82f));
            hud.Dot(new Vector2(29,29),18,new Color(.18f,.4f,.62f));hud.Dot(new Vector2(29,29),7,HudPainter.Cyan);
            hud.Text(new Rect(47,8,310,26),"迪迦 · 光之训练场",18,HudPainter.Ink,bold:true);
            hud.Text(new Rect(48,34,320,18),keyboard?"键盘练习":pose?.source=="synthetic"?"合成动作测试 · 非摄像头":"摄像头体感",11,HudPainter.Muted);
            string stage=battle.Phase==GamePhase.Victory?"守护成功":battle.Phase==GamePhase.Paused?"休息一下":"城市守护";
            hud.Rounded(new Rect(552,15,136,27),new Color(.14f,.37f,.46f,.38f));
            hud.Text(new Rect(552,15,136,27),stage,13,HudPainter.Cyan,TextAnchor.MiddleCenter);
            hud.Text(new Rect(850,9,245,20),"哥尔赞",14,HudPainter.Ink);
            hud.Text(new Rect(1090,9,162,20),$"{Mathf.CeilToInt(battle.EnemyHealth)} / {battle.MaxHealth}",13,HudPainter.Muted,TextAnchor.MiddleRight);
            hud.Bar(new Rect(850,37,402,6),battle.EnemyHealth/battle.MaxHealth,new Color(.93f,.49f,.34f));
            hud.Panel(new Rect(20,78,204,49),HudPainter.Gold,ready);
            hud.Text(new Rect(32,83,181,19),ready?"光线就绪":$"光线能量  {battle.Energy:0} / {Battle.MaxEnergy}",12,ready?HudPainter.Gold:HudPainter.Muted);
            for(int i=0;i<Battle.MaxEnergy;i++)hud.Rounded(new Rect(32+i*12,109,9,5),i<battle.Energy?HudPainter.Gold:new Color(.14f,.22f,.32f),2);
            bool rushing=battle.Enemy==EnemyPhase.Attack&&battle.EnemyAge<Battle.EnemyHitSeconds;
            if(battle.Phase==GamePhase.Battle&&(battle.Enemy==EnemyPhase.Windup||rushing))
            {
                hud.Panel(new Rect(20,143,230,66),HudPainter.Gold,true);
                hud.Figure(new Rect(28,150,34,45),"shield",time,HudPainter.Gold);
                hud.Text(new Rect(73,149,166,42),rushing?"怪兽冲过来了\n双手护住胸前":"怪兽蓄力\n双手护住胸前",16,HudPainter.Gold);
                hud.Bar(new Rect(32,200,206,3),1-battle.EnemyAge/(rushing?Battle.EnemyHitSeconds:Battle.WindupSeconds),HudPainter.Gold);
            }
            else if(time<hitUntil&&battle.Phase==GamePhase.Battle)
                hud.Text(new Rect(20,143,230,34),battle.Action==HeroAction.Beam?"光线命中！":"漂亮一击！",20,HudPainter.Gold,TextAnchor.MiddleCenter,true);
            if(time<beamTitleUntil&&(battle.Phase==GamePhase.Battle||battle.Phase==GamePhase.Victory))
            {hud.Panel(new Rect(455,78,370,46),HudPainter.Gold,true);hud.Text(new Rect(465,84,350,34),"哉佩利敖光线！",23,HudPainter.Gold,TextAnchor.MiddleCenter,true);}
            else if(time<captionUntil&&battle.Phase==GamePhase.Battle)
            {hud.Panel(new Rect(465,78,350,29),HudPainter.Cyan);hud.Text(new Rect(475,81,330,23),caption,13,HudPainter.Ink,TextAnchor.MiddleCenter);}
            BattleAction(20,"挥拳出击",keyboard?"A / D 挥拳":$"收手再挥 · 命中 {battle.Punches}","punch",HudPainter.Cyan,battle.Action==HeroAction.LeftPunch||battle.Action==HeroAction.RightPunch);
            BattleAction(244,"光之护盾",keyboard?"按住 S 防御":"双手护住胸前","shield",HudPainter.Violet,battle.Shield);
            BattleAction(468,"必杀光线",keyboard?"满能量后按 J":ready?"L 形 / 双手前推":$"普攻 {battle.Energy:0}/{Battle.MaxEnergy}","beam",HudPainter.Gold,ready||battle.Action==HeroAction.Beam);
            if(!keyboard&&ready)hud.Bar(new Rect(524,673,145,2),recognizer.BeamProgress,HudPainter.Gold);
            DrawPreview(true);
            if(battle.Phase==GamePhase.Victory)
            {
                hud.Panel(new Rect(20,145,252,252),HudPainter.Gold,true);
                hud.Text(new Rect(38,160,218,36),"城市守护成功！",21,HudPainter.Ink,bold:true);
                hud.Text(new Rect(38,209,218,42),$"挥拳命中 {battle.Punches} 次\n成功防御 {battle.Blocks} 次",14,HudPainter.Muted);
                GUI.enabled=photoAvailable;
                if(hud.Button(new Rect(38,267,216,42),"      合照 · F7",HudPainter.Cyan,16))photo.Open();
                hud.Rounded(new Rect(79,283,21,14),photoAvailable?HudPainter.Cyan:HudPainter.Muted,3);
                hud.Box(new Rect(84,279,10,4),HudPainter.Cyan);hud.Dot(new Vector2(89.5f,290),8,new Color(.03f,.07f,.12f));
                GUI.enabled=true;
                hud.Text(new Rect(38,311,216,23),photoAvailable?"5 秒倒计时 · 保存至 Downloads":"体感模式启动游戏后可合照",11,HudPainter.Muted,TextAnchor.MiddleCenter);
                if(hud.Button(new Rect(38,347,216,32),"再守护一次",HudPainter.Gold,13))Restart();
            }
            if(battle.Phase==GamePhase.Paused)
            {
                hud.Panel(new Rect(462,244,356,142),HudPainter.Cyan);
                hud.Text(new Rect(478,254,324,32),paused?"休息一下吧":"等你回来，一起继续",20,HudPainter.Ink,TextAnchor.MiddleCenter,true);
                hud.Text(new Rect(478,294,324,29),paused?"准备好了，再继续守护城市":"让肩膀回到取景画面，站稳片刻",13,HudPainter.Muted,TextAnchor.MiddleCenter);
                if(paused&&hud.Button(new Rect(552,339,176,31),"继续战斗",size:13))paused=false;
                if(!paused)hud.Bar(new Rect(500,351,280,5),battle.ResumeProgress/1.2f,HudPainter.Cyan);
            }
            DrawFooter(true);
        }
        void DrawSettings()
        {
            hud.Box(new Rect(0,0,1280,661),new Color(.005f,.01f,.03f,.72f));hud.Panel(new Rect(355,115,570,521),HudPainter.Cyan);
            hud.Text(new Rect(385,134,500,40),"游戏设置",27,HudPainter.Ink,bold:true);
            if(hud.Button(new Rect(386,184,246,36),"战斗",audioSettings?HudPainter.Muted:HudPainter.Cyan))audioSettings=false;
            if(hud.Button(new Rect(648,184,246,36),"声音与音乐",audioSettings?HudPainter.Cyan:HudPainter.Muted))audioSettings=true;
            if(audioSettings)DrawAudioSettings();else
            {
                hud.Text(new Rect(386,241,508,28),"怪兽血量 · 可承受的普攻次数",20,HudPainter.Ink,bold:true);
                if(hud.Button(new Rect(386,289,64,42),"−10"))draftMonsterHits=Battle.ClampMonsterHits(draftMonsterHits-10);
                hud.Text(new Rect(467,284,348,52),$"{draftMonsterHits} 次普攻",31,HudPainter.Gold,TextAnchor.MiddleCenter,true);
                if(hud.Button(new Rect(830,289,64,42),"+10"))draftMonsterHits=Battle.ClampMonsterHits(draftMonsterHits+10);
                draftMonsterHits=Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(400,356,480,24),draftMonsterHits,Battle.MinMonsterHits,Battle.MaxMonsterHits)/10)*10;
                hud.Text(new Rect(386,382,320,30),$"{Battle.MinMonsterHits}–{Battle.MaxMonsterHits} 次 · 每次调整 10",14,HudPainter.Muted);
                if(hud.Button(new Rect(760,382,134,30),"恢复默认 50"))draftMonsterHits=Battle.DefaultMonsterHits;
                hud.Text(new Rect(386,435,508,30),$"大招蓄能：每命中 {Battle.MaxEnergy} 次普攻攒满",20,HudPainter.Cyan);
                hud.Text(new Rect(386,477,508,55),"光线伤害相当于 9 次普攻；防御不增加能量。\n← / → 调整，Enter 应用；修改后重开并记住设置。",15,HudPainter.Muted);
            }
            bool changed=draftMonsterHits!=monsterHits;
            if(hud.Button(new Rect(386,567,508,40),changed?"应用并开始新一局":"保存并返回"))
                ApplySettings();
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
        {photo?.Dispose();client?.Dispose();previewClient?.Dispose();if(previewTexture)Destroy(previewTexture);hud?.Dispose();sound?.Save();}
    }
}
