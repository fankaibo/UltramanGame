using System;
using System.Collections.Generic;
using UnityEngine;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    public sealed class ArenaController : MonoBehaviour
    {
        Battle battle=new Battle();
        GestureRecognizer recognizer=new GestureRecognizer();
        PlayerPresence presence=new PlayerPresence();
        PoseClient client;
        PoseFrame pose;
        PlayerInput held;
        PrototypeActor hero,enemy;
        Transform shield,beam;
        Camera view;
        AudioSource voice,effects;
        Font font;
        bool keyboard,paused,muted;
        bool lastTracking;
        string caption="站在摄像头前，让肩膀和双手进入画面";
        float captionUntil;
        string stream;
        long sequence;
        float lastHealth=Battle.MaxHealth,impact;
        GUIStyle title,body,small,button;
        readonly Dictionary<int,AudioClip> tones=new Dictionary<int,AudioClip>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if(FindFirstObjectByType<ArenaController>()==null) new GameObject("UltramanGame").AddComponent<ArenaController>();
        }
        void Start()
        {
            Application.targetFrameRate=60;
            keyboard=Array.IndexOf(Environment.GetCommandLineArgs(),"--keyboard")>=0;
            Application.runInBackground=true;
            Screen.sleepTimeout=SleepTimeout.NeverSleep;
            client=new PoseClient();
            font=Resources.Load<Font>("Fonts/NotoSansSC-Regular");
            if(font==null) { Debug.LogError("Bundled Chinese font is missing.");font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
            voice=gameObject.AddComponent<AudioSource>(); effects=gameObject.AddComponent<AudioSource>();
            voice.volume=.7f;effects.volume=.2f;
            CreateScene();
        }
        void CreateScene()
        {
            var existing=Camera.main;
            view=existing?existing:new GameObject("Main Camera").AddComponent<Camera>();
            view.tag="MainCamera";
            view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=new Color(.055f,.09f,.17f);
            view.transform.position=new Vector3(5.8f,4.5f,-7.5f);view.transform.LookAt(new Vector3(0,1.65f,2.6f));view.fieldOfView=43;
            if(!FindFirstObjectByType<AudioListener>()) view.gameObject.AddComponent<AudioListener>();
            var light=new GameObject("Sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.transform.rotation=Quaternion.Euler(45,-30,0);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.62f,.68f,.78f);
            var fill=new GameObject("Hero fill light").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.8f;fill.transform.rotation=Quaternion.Euler(25,150,0);
            RenderSettings.fog=true;RenderSettings.fogColor=view.backgroundColor;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.015f;
            var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.name="Training plaza";ground.transform.localScale=new Vector3(12,1,12);
            ground.GetComponent<Renderer>().sharedMaterial=PrototypeActor.Material(new Color(.13f,.20f,.27f));
            var cityMat=PrototypeActor.Material(new Color(.21f,.30f,.39f));
            var lit=PrototypeActor.Material(new Color(.13f,.56f,.72f),0,true);
            var random=new System.Random(41);
            for(int row=0;row<5;row++) for(int column=-5;column<=5;column++)
            {
                if(Math.Abs(column)<2) continue;
                float h=3+(float)random.NextDouble()*8;
                var block=GameObject.CreatePrimitive(PrimitiveType.Cube);block.name="City";
                block.transform.position=new Vector3(column*4,h/2,row*6-4);
                block.transform.localScale=new Vector3(2.5f,h,3);block.GetComponent<Renderer>().sharedMaterial=cityMat;
                var window=GameObject.CreatePrimitive(PrimitiveType.Cube);window.transform.SetParent(block.transform,false);
                window.transform.localPosition=new Vector3(0,0,-.505f);window.transform.localScale=new Vector3(.65f,.7f,.01f);window.GetComponent<Renderer>().sharedMaterial=lit;
            }
            hero=new PrototypeActor("Hero prototype",Vector3.zero);
            enemy=new PrototypeActor("Friendly training monster",new Vector3(0,0,4),true);enemy.Root.rotation=Quaternion.Euler(0,180,0);
            var shieldObj=GameObject.CreatePrimitive(PrimitiveType.Sphere);shieldObj.name="Shield";shield=shieldObj.transform;
            shield.position=new Vector3(0,1.9f,.8f);shield.localScale=new Vector3(1.6f,1.8f,.08f);
            shieldObj.GetComponent<Renderer>().sharedMaterial=PrototypeActor.Material(new Color(.1f,.65f,.95f),.4f,true);
            var beamObj=GameObject.CreatePrimitive(PrimitiveType.Cylinder);beamObj.name="Light beam";beam=beamObj.transform;
            beam.position=new Vector3(0,2.2f,2.25f);beam.rotation=Quaternion.Euler(90,0,0);beam.localScale=new Vector3(.22f,1.5f,.22f);
            beamObj.GetComponent<Renderer>().sharedMaterial=PrototypeActor.Material(new Color(.7f,.92f,1),0,true);
        }
        void Update()
        {
            if(Input.GetKeyDown(KeyCode.F2)) SetMode(!keyboard);
            if(Input.GetKeyDown(KeyCode.F11)) Screen.fullScreen=!Screen.fullScreen;
            if(Input.GetKeyDown(KeyCode.Escape)) { paused=!paused;if(paused) battle.Pause(); }
            if(Input.GetKeyDown(KeyCode.R)) Restart();
            long now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            PlayerInput input=default;
            if(keyboard)
            {
                input=new PlayerInput { Tracking=true, Transform=Input.GetKeyDown(KeyCode.Space),
                    LeftPunch=Input.GetKeyDown(KeyCode.A),RightPunch=Input.GetKeyDown(KeyCode.D),
                    Shield=Input.GetKey(KeyCode.S),Beam=Input.GetKeyDown(KeyCode.J) };
            }
            else
            {
                input=held;input.Transform=input.LeftPunch=input.RightPunch=input.Beam=false;
                var line=client.TakeLatest();
                if(line!=null)
                {
                    try
                    {
                        var incoming=JsonUtility.FromJson<PoseFrame>(line);
                        if(incoming!=null && (incoming.streamId!=stream || incoming.sequence>sequence))
                        {
                            pose=incoming;stream=pose.streamId;sequence=pose.sequence;
                            input=recognizer.Update(pose,now);
                        }
                    }
                    catch(ArgumentException) { pose=null;recognizer.Reset();input=default; }
                }
                if(!PoseQuality.Present(pose,now)) { input=default;recognizer.Reset(); }
                input.Tracking=presence.Update(pose,now);
                held=input;
            }
            if(paused) input.Tracking=false;
            if(input.Tracking!=lastTracking)
            {
                lastTracking=input.Tracking;
                if(Debug.isDebugBuild) Debug.Log($"[Input] tracking={lastTracking} mode={(keyboard?"keyboard":pose?.source??"camera")}");
            }
            battle.Tick(Time.unscaledDeltaTime,input);
            // Pausing cancels queued one-shot inputs; recovery consumes a fresh stable interval.
            while(battle.TryCue(out var cue)) PlayCue(cue);
            float dt=Time.unscaledDeltaTime;
            hero.Update(!keyboard && PoseQuality.Present(pose,now)?pose:null,battle,dt,Time.time);
            enemy.Update(null,battle,dt,Time.time);
            hero.Root.rotation=Quaternion.Slerp(hero.Root.rotation,
                Quaternion.Euler(0,battle.Phase==GamePhase.Waiting||battle.Phase==GamePhase.Transforming||battle.Phase==GamePhase.Victory?150:0,0),dt*3);
            bool punching=battle.Action==HeroAction.LeftPunch || battle.Action==HeroAction.RightPunch;
            hero.Root.position=new Vector3(0,0,punching?Mathf.Sin(Mathf.Clamp01(battle.ActionAge/.45f)*Mathf.PI)*2.2f:0);
            shield.gameObject.SetActive(battle.Shield && battle.Phase==GamePhase.Battle);
            beam.gameObject.SetActive(battle.Action==HeroAction.Beam && battle.ActionAge>.3f && battle.Phase==GamePhase.Battle);
            if(battle.EnemyHealth<lastHealth) impact=.25f;
            lastHealth=battle.EnemyHealth;impact=Mathf.Max(0,impact-dt);
            enemy.Root.position=new Vector3(0,0,4+impact*Mathf.Sin(Time.time*42));
        }
        void PlayCue(GameCue cue)
        {
            if(Debug.isDebugBuild) Debug.Log($"[Game] cue={cue} phase={battle.Phase} health={battle.EnemyHealth} energy={battle.Energy}");
            string key="",text="";
            switch(cue)
            {
                case GameCue.Transform:key="transform";text="光的力量，准备变身！";break;
                case GameCue.BattleStart:key="battle";text="挥动拳头，保护城市！";break;
                case GameCue.Warning:key="warning";text="怪兽要攻击了！双手放在胸前防御";break;
                case GameCue.Block:key="block";text="挡住了！做得好！";break;
                case GameCue.Hurt:key="recover";text="没关系，恢复力量，再来一次！";break;
                case GameCue.EnergyReady:key="energy";text="能量满了！摆出光线姿势！";break;
                case GameCue.Beam:key="beam";text="发射光线！";break;
                case GameCue.Victory:key="victory";text="城市安全了！谢谢你，光之英雄！";break;
                case GameCue.Resume:text="准备好了，继续战斗！";break;
                case GameCue.Punch:if(!muted) effects.PlayOneShot(Tone(190,.08f));return;
            }
            caption=text;captionUntil=Time.unscaledTime+3.5f;
            if(muted) return;
            var clip=Resources.Load<AudioClip>("Voice/"+key);
            if(clip && (cue==GameCue.Warning || cue==GameCue.Victory || !voice.isPlaying))
            { voice.Stop();voice.clip=clip;voice.Play(); }
            else if(!clip) effects.PlayOneShot(Tone(cue==GameCue.Warning?520:880,.12f));
        }
        AudioClip Tone(float frequency,float duration)
        {
            if(tones.TryGetValue((int)frequency,out var cached)) return cached;
            int n=(int)(22050*duration);var data=new float[n];
            for(int i=0;i<n;i++) data[i]=Mathf.Sin(i*frequency*2*Mathf.PI/22050)*.16f*Mathf.Sin(i*Mathf.PI/n);
            var clip=AudioClip.Create("Feedback",n,1,22050,false);clip.SetData(data,0);tones[(int)frequency]=clip;return clip;
        }
        void Restart()
        { battle=new Battle();recognizer.Reset();presence.Reset();pose=null;held=default;paused=false;enemy.Root.localScale=Vector3.one;lastHealth=Battle.MaxHealth;caption="双手举高，准备变身";voice.Stop(); }
        void SetMode(bool value)
        { keyboard=value;Restart(); }
        void Styles()
        {
            title=new GUIStyle(GUI.skin.label) { font=font,fontSize=30,fontStyle=FontStyle.Bold };
            body=new GUIStyle(GUI.skin.label) { font=font,fontSize=23,alignment=TextAnchor.MiddleCenter,wordWrap=true };
            small=new GUIStyle(GUI.skin.label) { font=font,fontSize=15,wordWrap=true };
            button=new GUIStyle(GUI.skin.button) { font=font,fontSize=17 };
        }
        static void Rect(float x,float y,float w,float h,Color color)
        { var before=GUI.color;GUI.color=color;GUI.DrawTexture(new Rect(x,y,w,h),Texture2D.whiteTexture);GUI.color=before; }
        void OnGUI()
        {
            if(font==null) return;
            if(title==null) Styles();
            GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(Screen.width/1280f,Screen.height/720f,1));
            Rect(0,0,1280,100,new Color(.03f,.06f,.11f,.93f));
            GUI.Label(new Rect(30,20,450,50),"迪迦 · 光之训练场",title);
            GUI.Label(new Rect(32,65,470,25),"体感原型  /  自制简模 · 美术待升级",small);
            GUI.Label(new Rect(700,15,220,30),"怪兽的力量",small);
            Rect(700,50,350,12,new Color(.24f,.28f,.34f));Rect(700,50,350*battle.EnemyHealth/Battle.MaxHealth,12,new Color(1,.4f,.32f));
            GUI.Label(new Rect(1090,22,160,40),keyboard?"键盘练习":pose?.source=="synthetic"?"合成动作测试":"摄像头体感",small);
            Rect(25,490,230,135,new Color(.03f,.06f,.11f,.84f));
            GUI.Label(new Rect(43,505,200,30),"光线能量",small);
            for(int i=0;i<6;i++) Rect(43+i*32,546,24,24,i<battle.Energy?new Color(.3f,.85f,1):new Color(.2f,.27f,.34f));
            GUI.Label(new Rect(43,580,200,35),$"挥拳 {battle.Punches}    防御 {battle.Blocks}",small);
            if(battle.Phase==GamePhase.Waiting || battle.Phase==GamePhase.Transforming || battle.Phase==GamePhase.Paused || battle.Phase==GamePhase.Victory)
            {
                bool waiting=battle.Phase==GamePhase.Waiting;
                float panelX=waiting?30:330,panelWidth=waiting?425:650;
                Rect(panelX,215,panelWidth,210,new Color(.035f,.075f,.14f,.92f));
                string text=battle.Phase==GamePhase.Waiting?(keyboard?"按空格，变身！":"双手举过肩膀，准备变身"):
                    battle.Phase==GamePhase.Transforming?"光的力量，正在苏醒":
                    battle.Phase==GamePhase.Victory?"你守护了这座城市！":paused?"休息一下 · 点击继续":"让肩膀回到画面中，准备继续";
                GUI.Label(new Rect(panelX+20,235,panelWidth-40,80),text,body);
                if(battle.Phase==GamePhase.Waiting)
                    GUI.Label(new Rect(panelX+20,325,panelWidth-40,80),keyboard?"A / D 挥拳\n按住 S 防御 · J 光线":"让肩膀、手肘和双手进入画面\n双手举高，英雄就会变身",body);
                if(battle.Phase==GamePhase.Victory && GUI.Button(new Rect(520,340,270,50),"再守护一次",button)) Restart();
                if(battle.Phase==GamePhase.Paused && paused && GUI.Button(new Rect(520,340,270,50),"继续",button)) paused=false;
            }
            if(battle.Phase==GamePhase.Battle && battle.Enemy==EnemyPhase.Windup)
            {
                Rect(375,120,540,80,new Color(.68f,.28f,.07f,.93f));
                GUI.Label(new Rect(390,130,510,60),"怪兽正在蓄力！双手护住胸前",body);
            }
            if(Time.unscaledTime<captionUntil) { Rect(290,555,700,65,new Color(.02f,.05f,.1f,.86f));GUI.Label(new Rect(310,560,660,55),caption,body); }
            Rect(0,655,1280,65,new Color(.03f,.06f,.11f,.95f));
            long now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string cameraStatus=PoseQuality.Present(pose,now)?
                (PoseQuality.Valid(pose,now)?"已经看见你 · 尽情挥动双手 · F11全屏":"你还在画面中 · 手臂暂时没看清，可把双手移入画面"):
                "让肩膀进入画面 · "+client.Status;
            GUI.Label(new Rect(25,668,700,38),keyboard?"空格变身 · A/D挥拳 · S防御 · J光线 · F11全屏":cameraStatus,small);
            if(GUI.Button(new Rect(740,669,160,32),keyboard?"切回摄像头":"键盘练习",button)) SetMode(!keyboard);
            if(GUI.Button(new Rect(915,669,110,32),muted?"开启声音":"静音",button)) { muted=!muted;if(muted) { voice.Stop();effects.Stop(); } }
            if(GUI.Button(new Rect(1040,669,100,32),paused?"继续":"暂停",button))
            { paused=!paused;if(paused) { battle.Pause();voice.Stop(); } }
            if(GUI.Button(new Rect(1155,669,95,32),"重来",button)) Restart();
        }
        void OnDestroy() { client?.Dispose();foreach(var clip in tones.Values) Destroy(clip); }
    }
}
