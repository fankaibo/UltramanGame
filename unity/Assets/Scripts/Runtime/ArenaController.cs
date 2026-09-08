using System;
using UnityEngine;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    public sealed class ArenaController : MonoBehaviour
    {
        Battle battle=new Battle();
        readonly GestureRecognizer recognizer=new GestureRecognizer();
        readonly PlayerPresence presence=new PlayerPresence();
        PoseClient client;
        PreviewClient previewClient;
        PreviewFrame previewFrame;
        Texture2D previewTexture;
        PoseFrame pose;
        PlayerInput held;
        PrototypeActor hero,enemy;
        GameWorld world;
        GameAudio sound;
        HudPainter hud;
        bool keyboard,paused,muted,settings,showPreview=true,previewReported,lastTracking;
        string caption="",stream;
        long sequence;
        float captionUntil,lastHealth=Battle.MaxHealth,impact,phaseStarted,hintAt=12,hitUntil,beamHelpAt;
        GamePhase lastPhase;
        float sampleAge,sampleSeconds,sampleWorst;
        int sampleFrames;
        bool sampled;
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
            QualitySettings.vSyncCount=0;Application.targetFrameRate=60;
            keyboard=Array.IndexOf(Environment.GetCommandLineArgs(),"--keyboard")>=0;
            Application.runInBackground=true;Screen.sleepTimeout=SleepTimeout.NeverSleep;
            client=new PoseClient(LocalPort("--pose-port",8765));previewClient=new PreviewClient(LocalPort("--preview-port",8766));
            var font=Resources.Load<Font>("Fonts/NotoSansSC-Regular");
            if(!font)font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hud=new HudPainter(font);world=new GameWorld();sound=new GameAudio(gameObject);
            hero=new PrototypeActor("Tiga training hero",world.HeroHome);enemy=new PrototypeActor("Training monster",world.EnemyHome,true);
            hero.Root.rotation=Quaternion.Euler(0,140,0);enemy.Root.rotation=Quaternion.Euler(0,235,0);
            if(!keyboard)sound.Speak("welcome",1,GamePhase.Waiting);
        }
        void Update()
        {
            if(Input.GetKeyDown(KeyCode.F2))SetMode(!keyboard);
            if(Input.GetKeyDown(KeyCode.F3))showPreview=!showPreview;
            if(Input.GetKeyDown(KeyCode.F4))settings=!settings;
            if(Input.GetKeyDown(KeyCode.F11))Screen.fullScreen=!Screen.fullScreen;
            if(Input.GetKeyDown(KeyCode.Escape)) { if(settings)settings=false;else paused=!paused; }
            if(Input.GetKeyDown(KeyCode.R))Restart();
            long now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();float dt=Time.unscaledDeltaTime;
            sampleAge+=dt;
            if(Debug.isDebugBuild&&!sampled&&sampleAge>3)
            {
                sampleSeconds+=dt;sampleFrames++;sampleWorst=Mathf.Max(sampleWorst,dt);
                if(sampleSeconds>=10)
                {sampled=true;Debug.Log($"[Runtime] renderFps={sampleFrames/sampleSeconds:F1} worstFrameMs={sampleWorst*1000:F1} {sound.Diagnostics}");}
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
                        { pose=incoming;stream=pose.streamId;sequence=pose.sequence;input=recognizer.Update(pose,now); }
                    }
                    catch(ArgumentException) {pose=null;recognizer.Reset();input=default;}
                }
                if(!PoseQuality.Present(pose,now)) {input=default;recognizer.Reset();}
                input.Tracking=presence.Update(pose,now);held=input;
            }
            if(paused||settings)input.Tracking=false;
            if(input.Tracking!=lastTracking)
            { lastTracking=input.Tracking;if(Debug.isDebugBuild)Debug.Log($"[Input] tracking={lastTracking} mode={(keyboard?"keyboard":pose?.source??"camera")}"); }
            battle.Tick(dt,input);
            if(battle.Phase!=lastPhase) {phaseStarted=Time.unscaledTime;lastPhase=battle.Phase;}
            sound.Tick(paused||settings?GamePhase.Paused:battle.Phase,muted,dt);
            while(battle.TryCue(out var cue))PlayCue(cue);
            hero.Update(!keyboard&&PoseQuality.Present(pose,now)?pose:null,battle,dt,Time.unscaledTime);
            enemy.Update(null,battle,dt,Time.unscaledTime);
            bool greeting=battle.Phase==GamePhase.Waiting||battle.Phase==GamePhase.Transforming||battle.Phase==GamePhase.Victory;
            hero.Root.rotation=Quaternion.Slerp(hero.Root.rotation,Quaternion.Euler(0,greeting?140:65,0),1-Mathf.Exp(-dt*5));
            bool punching=battle.Phase==GamePhase.Battle&&(battle.Action==HeroAction.LeftPunch||battle.Action==HeroAction.RightPunch);
            hero.Root.position=world.HeroHome+(world.EnemyHome-world.HeroHome).normalized*(punching?PrototypeActor.Strike(battle.ActionAge)*.65f:0);
            if(battle.EnemyHealth<lastHealth)
            {
                bool special=lastHealth-battle.EnemyHealth>1;impact=special?.35f:.2f;hitUntil=Time.unscaledTime+1;
                world.Hit(special);sound.Effect("impact",special?1:.8f);
            }
            lastHealth=battle.EnemyHealth;impact=Mathf.Max(0,impact-dt);
            enemy.Root.position=world.EnemyHome+(world.EnemyHome-world.HeroHome).normalized*Mathf.Sin(impact*9)*.3f;
            enemy.Root.rotation=Quaternion.Euler(-impact*25,235,Mathf.Sin(impact*15)*impact*8);
            world.Tick(battle,dt,Time.unscaledTime);
            if(!keyboard&&!paused&&!settings&&battle.Phase==GamePhase.Battle)
            {
                if(battle.Punches==0&&Time.unscaledTime>hintAt)
                {sound.Speak("tutorial",1,battle.Phase);caption="先把手收回来，再挥出去";captionUntil=Time.unscaledTime+3;hintAt=Time.unscaledTime+20;}
                if(battle.Energy<6)beamHelpAt=Time.unscaledTime+6;
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
                case GameCue.Beam:caption="哉佩利敖光线！";break;
                case GameCue.Resume:caption="准备好了，继续！";break;
                default:return;
            }
            captionUntil=Time.unscaledTime+2.5f;
        }
        void Restart()
        {
            battle=new Battle();recognizer.Reset();presence.Reset();pose=null;held=default;paused=settings=false;
            lastHealth=Battle.MaxHealth;impact=0;captionUntil=0;hitUntil=0;hintAt=Time.unscaledTime+12;beamHelpAt=Time.unscaledTime+6;sound.Reset();
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
        void DrawPreview()
        {
            if(keyboard)return;
            if(!showPreview)
            {if(hud.Button(new Rect(1083,606,169,34),"显示取景 · F3"))showPreview=true;return;}
            hud.Panel(new Rect(992,389,260,253),HudPainter.Cyan);
            hud.Dot(new Vector2(1008,409),6,previewFrame!=null?HudPainter.Cyan:HudPainter.Gold);
            hud.Text(new Rect(1020,395,178,28),previewFrame?.Synthetic==true||pose?.source=="synthetic"?"合成测试 · 非摄像头":"镜像取景",14);
            if(hud.Button(new Rect(1201,398,43,24),"收起"))showPreview=false;
            hud.Box(new Rect(1000,430,244,183),Color.black);
            if(previewFrame!=null&&previewFrame.Fresh(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())&&previewTexture)
            {
                GUI.DrawTexture(new Rect(1000,430,244,183),previewTexture,ScaleMode.ScaleToFit);
                int quality=previewFrame.Quality;
                hud.Text(new Rect(1005,615,240,22),quality==2?"双臂已看清 · 动作准备就绪":quality==1?"手臂没看清 · 看橙色关节点":"请让双肩进入画面",13,quality==2?HudPainter.Cyan:HudPainter.Gold);
            }
            else
            {
                hud.Text(new Rect(1012,483,220,74),"画面暂未更新\n"+previewClient.Status,15,HudPainter.Muted,TextAnchor.MiddleCenter);
                hud.Text(new Rect(1005,615,230,22),"仅本机处理 · 不录制",13,HudPainter.Muted);
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
            long now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();float time=Time.unscaledTime;
            hud.Box(new Rect(0,0,1280,103),new Color(.012f,.025f,.06f,.91f));hud.Box(new Rect(28,101,1224,1),new Color(.25f,.53f,.8f,.25f));
            hud.Dot(new Vector2(47,47),33,new Color(.18f,.4f,.62f));hud.Dot(new Vector2(47,47),13,HudPainter.Cyan);
            hud.Text(new Rect(78,23,410,41),"迪迦 · 光之训练场",29,HudPainter.Ink,bold:true);
            hud.Text(new Rect(80,66,390,22),"CITY OF LIGHT  /  一起守护城市",12,HudPainter.Muted);
            string stage=battle.Phase==GamePhase.Waiting?"准备出发":battle.Phase==GamePhase.Transforming?"光之变身":battle.Phase==GamePhase.Victory?"守护成功":battle.Phase==GamePhase.Paused?"休息一下":"城市守护";
            hud.Rounded(new Rect(526,30,166,35),new Color(.14f,.37f,.46f,.48f));hud.Text(new Rect(526,30,166,35),stage,17,HudPainter.Cyan,TextAnchor.MiddleCenter);
            hud.Text(new Rect(801,20,250,26),"训练怪兽",17,HudPainter.Ink);
            hud.Text(new Rect(1077,20,171,26),$"{Mathf.CeilToInt(battle.EnemyHealth)} / 24",15,HudPainter.Muted,TextAnchor.MiddleRight);
            hud.Bar(new Rect(803,56,445,9),battle.EnemyHealth/Battle.MaxHealth,new Color(.93f,.49f,.34f));
            hud.Text(new Rect(803,72,440,20),keyboard?"键盘练习":pose?.source=="synthetic"?"合成动作测试 · 不使用摄像头":"摄像头体感 · 家庭训练",12,HudPainter.Muted,TextAnchor.MiddleRight);
            hud.Panel(new Rect(28,122,263,65),HudPainter.Gold,battle.Energy>=6);
            hud.Text(new Rect(42,130,235,20),battle.Energy>=6?"光线已就绪！":"光线能量",14,battle.Energy>=6?HudPainter.Gold:HudPainter.Muted);
            for(int i=0;i<6;i++)hud.Rounded(new Rect(43+i*38,160,28,8),i<battle.Energy?HudPainter.Gold:new Color(.14f,.22f,.32f),4);
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
            if(battle.Phase==GamePhase.Battle&&battle.Enemy==EnemyPhase.Windup)
            {
                hud.Panel(new Rect(395,121,527,80),HudPainter.Gold,true);
                hud.Text(new Rect(414,130,490,37),"怪兽蓄力中 · 双手护住胸前",23,HudPainter.Gold,TextAnchor.MiddleCenter,true);
                hud.Bar(new Rect(418,180,480,5),1-battle.EnemyAge/Battle.WindupSeconds,HudPainter.Gold);
            }
            else if(time<hitUntil)
                hud.Text(new Rect(510,153,325,62),battle.Action==HeroAction.Beam?"光线命中！":"漂亮一击！",30,HudPainter.Gold,TextAnchor.MiddleCenter,true);
            if(time<captionUntil&&battle.Phase==GamePhase.Battle)
            {hud.Panel(new Rect(383,447,546,53),HudPainter.Cyan);hud.Text(new Rect(397,451,518,44),caption,20,HudPainter.Ink,TextAnchor.MiddleCenter);}
            ActionCard(28,"挥拳出击",keyboard?"A / D · 左右交替":$"收手，再挥出去  ·  命中 {battle.Punches}","punch",HudPainter.Cyan,battle.Phase==GamePhase.Battle&&(battle.Action==HeroAction.LeftPunch||battle.Action==HeroAction.RightPunch));
            ActionCard(348,"光之护盾",keyboard?"按住 S 防御":"双手护住胸前","shield",HudPainter.Violet,battle.Shield);
            ActionCard(668,"必杀光线",keyboard?"能量满后按 J":battle.Energy>=6?"摆 L 形，保持片刻":"挥拳和防御可以蓄能","beam",HudPainter.Gold,battle.Phase==GamePhase.Battle&&(battle.Energy>=6||battle.Action==HeroAction.Beam));
            if(!keyboard&&battle.Energy>=6)hud.Bar(new Rect(764,623,180,4),recognizer.BeamProgress,HudPainter.Gold);
            DrawPreview();
            if(battle.Phase==GamePhase.Victory)
            {
                hud.Panel(new Rect(28,211,363,289),HudPainter.Gold,true);
                hud.Text(new Rect(51,227,317,28),"MISSION COMPLETE",13,HudPainter.Gold);
                hud.Text(new Rect(50,268,319,55),"城市守护成功！",29,HudPainter.Ink,bold:true);
                hud.Text(new Rect(51,336,307,48),$"挥拳命中 {battle.Punches} 次\n成功防御 {battle.Blocks} 次",18,HudPainter.Muted);
                if(hud.Button(new Rect(51,416,317,52),"再守护一次",HudPainter.Gold))Restart();
            }
            if(battle.Phase==GamePhase.Paused&&!settings)
            {
                hud.Panel(new Rect(391,204,511,226),HudPainter.Cyan);
                hud.Text(new Rect(415,224,463,55),paused?"休息一下吧":"等你回来，一起继续",27,HudPainter.Ink,TextAnchor.MiddleCenter,true);
                hud.Text(new Rect(421,289,451,43),paused?"准备好了，再继续守护城市":"让肩膀回到取景画面，站稳片刻",18,HudPainter.Muted,TextAnchor.MiddleCenter);
                if(paused&&hud.Button(new Rect(538,356,215,46),"继续战斗"))paused=false;
                if(!paused)hud.Bar(new Rect(487,368,319,7),battle.ResumeProgress/1.2f,HudPainter.Cyan);
            }
            hud.Box(new Rect(0,661,1280,59),new Color(.014f,.027f,.055f,.97f));
            string status=keyboard?"空格变身 · A/D挥拳 · S防御 · J光线":PoseQuality.Present(pose,now)?
                (PoseQuality.Valid(pose,now)?"已经看见你 · 尽情挥动双手":"你还在画面中 · 可把手臂移进镜头"):
                "让肩膀进入画面 · "+client.Status;
            hud.Dot(new Vector2(34,690),7,keyboard||PoseQuality.Present(pose,now)?HudPainter.Cyan:HudPainter.Gold);
            hud.Text(new Rect(49,672,655,36),status,14,HudPainter.Muted);
            if(hud.Button(new Rect(770,673,130,34),keyboard?"切回体感":"键盘练习"))SetMode(!keyboard);
            if(hud.Button(new Rect(912,673,108,34),"声音设置"))settings=!settings;
            if(hud.Button(new Rect(1032,673,100,34),paused?"继续":"暂停"))paused=!paused;
            if(hud.Button(new Rect(1144,673,108,34),"重新开始"))Restart();
            if(settings)DrawSettings();
        }
        void DrawSettings()
        {
            hud.Box(new Rect(0,0,1280,661),new Color(.005f,.01f,.03f,.62f));hud.Panel(new Rect(410,160,460,407),HudPainter.Cyan);
            hud.Text(new Rect(440,181,395,46),"声音与显示",27,HudPainter.Ink,bold:true);
            hud.Text(new Rect(440,246,180,27),"总音量",18);hud.Text(new Rect(718,246,115,27),Mathf.RoundToInt(sound.Volume*100)+"%",16,HudPainter.Muted,TextAnchor.MiddleRight);
            sound.Volume=GUI.HorizontalSlider(new Rect(442,283,390,24),sound.Volume,0,1);
            hud.Text(new Rect(440,318,220,27),"背景音乐",18);
            if(hud.Button(new Rect(730,316,103,31),sound.MusicEnabled?"已开启":"已关闭"))sound.MusicEnabled=!sound.MusicEnabled;
            sound.MusicVolume=GUI.HorizontalSlider(new Rect(442,359,390,24),sound.MusicVolume,0,1);
            if(hud.Button(new Rect(440,402,186,35),muted?"恢复声音":"全部静音"))muted=!muted;
            if(hud.Button(new Rect(644,402,188,35),"切换全屏 · F11"))Screen.fullScreen=!Screen.fullScreen;
            hud.Text(new Rect(440,452,391,38),"F3 取景 · F4 设置 · Esc 暂停\n角色为本项目自制造型",12,HudPainter.Muted);
            if(hud.Button(new Rect(440,510,392,37),"保存并返回")) {settings=false;sound.Save();}
        }
        void OnDestroy()
        {client?.Dispose();previewClient?.Dispose();if(previewTexture)Destroy(previewTexture);hud?.Dispose();sound?.Save();}
    }
}
