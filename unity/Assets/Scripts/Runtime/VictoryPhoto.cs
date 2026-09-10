using System;
using System.IO;
using UnityEngine;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    public sealed class VictoryPhoto : IDisposable
    {
        readonly int port;
        readonly GameAudio sound;
        readonly GuidedPhoto session=new GuidedPhoto();
        readonly PhotoChoiceGesture choice=new PhotoChoiceGesture();
        PhotoClient client;
        PhotoComposition composition;
        PhotoFrame frame;
        Texture2D person,saved;
        Texture cameraPreview;
        byte[] photoPng;
        string message="",savedPath="";
        bool validPerson,freshPerson,reportedPerson;
        int previousNumber;
        double flashUntil,nextGuide,reviewReadyAt,nextSaveRetry;
        public bool Active=>session.Stage!=PhotoStage.Closed;
        public PhotoStage Stage=>session.Stage;
        public bool HasLivePerson=>freshPerson;
        public bool PlayAgainRequested {get;private set;}
        public int Captures {get;private set;}
        public string LastSavedPath=>savedPath;
        public RenderTexture LivePicture=>composition?.Preview;
        public Texture SavedPicture=>saved;
        public float ChoiceProgress=>choice.Progress;
        public VictoryPhoto(int port,GameAudio sound=null) {this.port=port;this.sound=sound;}
        double Now=>Time.realtimeSinceStartupAsDouble;
        float Say(string key)
        {sound?.Speak(key,6,GamePhase.Victory);return sound?.VoiceLength(key)??0;}
        public void Open()
        {
            if(Active)return;
            PlayAgainRequested=false;composition=new PhotoComposition();Prepare();
        }
        void Prepare()
        {
            if(saved)UnityEngine.Object.Destroy(saved);saved=null;photoPng=null;savedPath="";
            frame=null;validPerson=freshPerson=reportedPerson=false;composition.HidePerson();choice.Reset();
            client?.Dispose();client=new PhotoClient(port);previousNumber=0;
            float seconds=Say("photo_intro");session.Open(Now,seconds);nextGuide=Now+seconds+8;
            message="看着右边的自己，摆个喜欢的姿势";
            Debug.Log($"[Photo] automatic live viewfinder opened voice={seconds:F2} reaction=3 countdown=5");
        }
        public void Back()
        {
            // Parent-only keyboard escape remains available; the child flow uses gestures.
            Close();
        }
        public void Tick(long now,PoseFrame pose=null,Texture liveCamera=null)
        {
            if(!Active)return;
            cameraPreview=liveCamera;
            if(session.Stage==PhotoStage.Review)
            {
                if(savedPath.Length==0&&photoPng!=null&&Now>=nextSaveRetry)SavePhoto();
                if(Now<reviewReadyAt) {choice.Reset();return;}
                var selected=choice.Update(pose,now);
                if(selected==PhotoChoice.Retake) {Debug.Log("[Photo] gesture=retake");Prepare();}
                if(selected==PhotoChoice.PlayAgain)
                {Debug.Log("[Photo] gesture=play-again");Close();PlayAgainRequested=true;}
                return;
            }
            var incoming=client?.TakeLatest();
            if(incoming!=null)
            {
                if(!person)person=new Texture2D(2,2,TextureFormat.RGBA32,false);
                validPerson=incoming.Fresh(now)&&incoming.Present&&person.LoadImage(incoming.Png)&&composition.SetPerson(person);
                frame=incoming;
                if(validPerson&&!reportedPerson)
                {reportedPerson=true;Debug.Log($"[Photo] live cutout displayed ageMs={now-incoming.CapturedMs} size={person.width}x{person.height} source={(incoming.Synthetic?"synthetic":"camera")}");}
            }
            freshPerson=frame!=null&&frame.Fresh(now)&&validPerson;
            if(!freshPerson)composition.HidePerson();
            composition.Render();
            bool take=session.Tick(Now,freshPerson);
            if(session.Interrupted)
            {
                previousNumber=0;session.DelayUntil(Now+Say("photo_retry")+3);nextGuide=Now+12;
                Debug.Log("[Photo] countdown interrupted; no stale image saved; automatic retry after recovery");
            }
            if(session.Stage==PhotoStage.Countdown)
            {
                int remaining=session.Remaining(Now);
                if(remaining!=previousNumber)
                {
                    previousNumber=remaining;
                    Say(new[]{"","photo_one","photo_two","photo_three","photo_four","photo_five"}[remaining]);
                    Debug.Log($"[Photo] countdown={remaining} fresh={freshPerson}");
                }
            }
            if(!freshPerson&&Now>=nextGuide)
            {Say("photo_missing");nextGuide=Now+14;Debug.Log($"[Photo] waiting cutout={client?.Status} liveCamera={cameraPreview!=null}");}
            if(!take)return;
            client?.Dispose();client=null;
            try
            {
                saved=composition.Snapshot();photoPng=saved.EncodeToPNG();Captures++;
                flashUntil=Now+.22;SavePhoto();choice.Reset();
                reviewReadyAt=Now+Math.Max(8,Say("photo_saved")+3);
                Debug.Log("[Photo] automatic capture complete; frozen review; hands down then gesture choice");
            }
            catch(UnityException e)
            {Prepare();Debug.LogWarning("[Photo] snapshot failed: "+e.GetType().Name);}
        }
        void SavePhoto()
        {
            nextSaveRetry=Now+10;
            try
            {
                savedPath=PhotoFiles.Save(PhotoFiles.Downloads,photoPng,frame.Synthetic);
                message="已保存到 Downloads";
                Debug.Log($"[Photo] saved source={(frame.Synthetic?"synthetic":"camera")} size={saved.width}x{saved.height} file={Path.GetFileName(savedPath)}");
            }
            catch(Exception e) when(e is IOException||e is UnauthorizedAccessException)
            {message="照片已保留，正在重试保存";Debug.LogWarning("[Photo] save failed: "+e.GetType().Name);}
        }
        public void Draw(HudPainter hud)
        {
            bool review=session.Stage==PhotoStage.Review,counting=session.Stage==PhotoStage.Countdown;
            hud.Box(new Rect(0,0,1280,720),new Color(.012f,.025f,.05f));
            GUI.DrawTexture(new Rect(0,0,1280,720),review&&saved?(Texture)saved:composition.Preview,ScaleMode.ScaleToFit,false);
            if(!review&&!freshPerson&&cameraPreview)
            {
                // Visible, honest fallback while native cutout initializes/reconnects.
                // This camera layer is HUD only and can never enter the saved composition.
                hud.Rounded(new Rect(720,112,465,456),new Color(.015f,.04f,.075f,.86f));
                GUI.DrawTexture(new Rect(735,128,435,402),cameraPreview,ScaleMode.ScaleToFit,false);
                hud.Text(new Rect(735,529,435,30),"实时镜头 · 正在准备镂空人像",16,HudPainter.Gold,TextAnchor.MiddleCenter);
            }
            hud.Box(new Rect(0,0,1280,64),new Color(.008f,.025f,.06f,.76f));
            hud.Text(new Rect(32,12,750,38),review?"光之英雄 · 合照纪念":"光之英雄 · 和迪迦站在一起",25,HudPainter.Ink,bold:true);
            hud.Text(new Rect(900,16,345,29),frame?.Synthetic==true?"合成测试 · 非真人":review?"照片预览 · 已定格":"实时镂空取景",14,HudPainter.Cyan,TextAnchor.MiddleRight);
            hud.Box(new Rect(0,612,1280,108),new Color(.008f,.025f,.06f,.88f));
            if(review)
            {
                hud.Text(new Rect(32,617,600,30),message,17,HudPainter.Cyan);
                bool reading=Now<reviewReadyAt;
                hud.Text(new Rect(32,654,1216,36),reading?"先放下双手，慢慢欣赏我们的合照":"举起一只手 · 再拍一张         双手举高 · 再玩一次",23,HudPainter.Ink,TextAnchor.MiddleCenter,true);
                hud.Bar(new Rect(460,704,360,4),reading?0:choice.Progress,HudPainter.Gold);
            }
            else
            {
                Corners(hud,new Rect(720,94,470,496),freshPerson?new Color(.3f,.9f,1,.4f):new Color(1,.76f,.38f,.6f));
                hud.Text(new Rect(770,73,365,28),freshPerson?"这是你 · 姿势实时可见":"让头和身体进入镜头",17,freshPerson?HudPainter.Cyan:HudPainter.Gold,TextAnchor.MiddleCenter);
                hud.Text(new Rect(60,633,1160,37),counting?"保持喜欢的姿势，马上自动拍照":freshPerson?message:"站进镜头，画面准备好后会自动倒数",25,HudPainter.Ink,TextAnchor.MiddleCenter,true);
                hud.Text(new Rect(60,677,1160,24),counting?"不需要点击 · 拍完自动显示合照":"先听完引导，再留三秒准备 · 不需要键盘或鼠标",14,HudPainter.Muted,TextAnchor.MiddleCenter);
                if(counting)
                {
                    hud.Dot(new Vector2(640,127),94,new Color(.015f,.06f,.11f,.9f));
                    hud.Text(new Rect(595,79,90,92),session.Remaining(Now).ToString(),57,HudPainter.Gold,TextAnchor.MiddleCenter,true);
                }
            }
            float flash=(float)((flashUntil-Now)/.22);
            if(flash>0)hud.Box(new Rect(0,0,1280,720),new Color(1,1,1,Mathf.Clamp01(flash)*.55f));
        }
        static void Corners(HudPainter hud,Rect r,Color color)
        {
            foreach(float x in new[]{r.x,r.xMax})foreach(float y in new[]{r.y,r.yMax})
            {hud.Line(new Vector2(x,y),new Vector2(x+(x==r.x?24:-24),y),color,2);hud.Line(new Vector2(x,y),new Vector2(x,y+(y==r.y?24:-24)),color,2);}
        }
        public void Close()
        {
            if(!Active)return;
            session.Close();client?.Dispose();client=null;composition?.Dispose();composition=null;
            if(person)UnityEngine.Object.Destroy(person);if(saved)UnityEngine.Object.Destroy(saved);person=saved=null;photoPng=null;
            frame=null;freshPerson=validPerson=false;cameraPreview=null;Debug.Log("[Photo] closed; live cutout subscription released");
        }
        public void Dispose()=>Close();
    }
}
