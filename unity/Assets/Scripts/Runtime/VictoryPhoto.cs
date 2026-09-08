using System;
using System.IO;
using UnityEngine;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    public sealed class VictoryPhoto : IDisposable
    {
        readonly int port;
        readonly PhotoSession session=new PhotoSession();
        PhotoClient client;
        PhotoComposition composition;
        PhotoFrame frame;
        Texture2D person,saved;
        byte[] photoPng;
        string message="",savedPath="";
        bool validPerson,freshPerson;
        double flashUntil;
        public bool Active=>session.Stage!=PhotoStage.Closed;
        public VictoryPhoto(int port) {this.port=port;}
        public void Open()
        {
            if(Active)return;
            composition=new PhotoComposition();Prepare();
        }
        void Prepare()
        {
            if(saved)UnityEngine.Object.Destroy(saved);saved=null;photoPng=null;savedPath="";
            frame=null;validPerson=freshPerson=false;composition.HidePerson();
            client?.Dispose();client=new PhotoClient(port);session.Open();
            message="先摆好姿势，准备好了再拍";
            Debug.Log("[Photo] live viewfinder opened; waiting for explicit shutter");
        }
        void Begin()
        {
            bool ready=freshPerson&&frame!=null&&frame.Fresh(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            if(session.Begin(Time.realtimeSinceStartupAsDouble,ready))
                Debug.Log("[Photo] countdown started seconds=5");
        }
        public void Back()
        {
            if(session.Stage==PhotoStage.Countdown)
            {session.Back();message="已取消倒计时，可以继续摆姿势";Debug.Log("[Photo] countdown cancelled; live viewfinder retained");}
            else Close();
        }
        public void Tick(long now)
        {
            if(!Active)return;
            if(session.Stage==PhotoStage.Review)
            {
                if(Input.GetKeyDown(KeyCode.Return))Prepare();
                return;
            }
            var incoming=client?.TakeLatest();
            if(incoming!=null&&incoming.Fresh(now))
            {
                if(!person)person=new Texture2D(2,2,TextureFormat.RGBA32,false);
                validPerson=incoming.Present&&person.LoadImage(incoming.Png)&&composition.SetPerson(person);
                frame=incoming;
            }
            freshPerson=frame!=null&&frame.Fresh(now)&&validPerson;
            if(!freshPerson)composition.HidePerson();
            composition.Render();
            if(Input.GetKeyDown(KeyCode.Space)||Input.GetKeyDown(KeyCode.Return))Begin();
            var before=session.Stage;
            if(!session.Tick(Time.realtimeSinceStartupAsDouble,freshPerson))
            {
                if(before==PhotoStage.Countdown&&session.Missed)
                {message="刚才没有拍到清楚的人像，站好后再拍一次";Debug.Log("[Photo] missed shutter; returned to live viewfinder without saving");}
                return;
            }
            client?.Dispose();client=null;
            try
            {
                saved=composition.Snapshot();photoPng=saved.EncodeToPNG();
                flashUntil=Time.realtimeSinceStartupAsDouble+.22;
                SavePhoto();
                Debug.Log("[Photo] review opened; captured image stays visible until retake or exit");
            }
            catch(UnityException e)
            {
                Prepare();message="这次拍摄没有完成，请再试一次";
                Debug.LogWarning("[Photo] snapshot failed: "+e.GetType().Name);
            }
        }
        void SavePhoto()
        {
            try
            {
                savedPath=PhotoFiles.Save(PhotoFiles.Downloads,photoPng,frame.Synthetic);
                message="已保存到 Downloads";
                Debug.Log($"[Photo] saved source={(frame.Synthetic?"synthetic":"camera")} size={saved.width}x{saved.height} file={Path.GetFileName(savedPath)}");
            }
            catch(Exception e) when(e is IOException||e is UnauthorizedAccessException)
            {message="照片已拍好，但未能保存；可重试保存";Debug.LogWarning("[Photo] save failed: "+e.GetType().Name);}
        }
        public void Draw(HudPainter hud)
        {
            bool review=session.Stage==PhotoStage.Review,counting=session.Stage==PhotoStage.Countdown;
            hud.Box(new Rect(0,0,1280,720),new Color(.012f,.025f,.05f));
            hud.Text(new Rect(32,10,700,31),review?"拍好了！看看我们的合照":"与迪迦合照 · 先摆个喜欢的姿势",22,HudPainter.Ink,bold:true);
            string status=review?"照片预览 · 已定格":counting?"正在倒计时 · 动作实时可见":"实时镂空取景 · 像照镜子一样";
            hud.Dot(new Vector2(906,26),7,review?HudPainter.Gold:HudPainter.Cyan);
            hud.Text(new Rect(919,12,330,28),frame?.Synthetic==true?"合成测试 · "+(review?"照片预览":"实时取景"):status,13,review?HudPainter.Gold:HudPainter.Cyan,TextAnchor.MiddleRight);
            var picture=new Rect(112,52,1056,594);
            // The live alpha cutout and the saved photo use exactly the same composition.
            GUI.DrawTexture(picture,review&&saved?(Texture)saved:composition.Preview,ScaleMode.ScaleToFit,false);
            if(review)
            {
                hud.Text(new Rect(32,658,710,27),message,16,savedPath.Length>0?HudPainter.Cyan:HudPainter.Gold);
                hud.Text(new Rect(32,687,710,21),savedPath.Length>0?"可以慢慢看，满意后返回；重拍会另存一张照片":"当前预览保留在这里，重试保存不需要重新摆姿势",12,HudPainter.Muted);
                if(savedPath.Length==0&&photoPng!=null&&hud.Button(new Rect(694,668,152,38),"重试保存",HudPainter.Cyan,14))SavePhoto();
                if(hud.Button(new Rect(858,667,184,40),"再拍一张 · Enter",HudPainter.Gold,14))Prepare();
                if(hud.Button(new Rect(1054,667,194,40),"完成 · Esc",size:15))Close();
            }
            else
            {
                // Light corner guides leave the body visible; all guides stay out of the exported image.
                var color=freshPerson?new Color(.3f,.9f,1,.42f):new Color(1,.76f,.38f,.7f);
                Corners(hud,new Rect(704,83,413,500),color);
                hud.Text(new Rect(755,60,316,24),freshPerson?"这是你 · 背景已镂空":"站进镜头，右侧会显示你",13,freshPerson?HudPainter.Cyan:HudPainter.Gold,TextAnchor.MiddleCenter);
                if(counting)
                {
                    hud.Dot(new Vector2(640,118),74,new Color(.015f,.06f,.11f,.86f));
                    hud.Text(new Rect(596,74,88,88),session.Remaining(Time.realtimeSinceStartupAsDouble).ToString(),49,HudPainter.Gold,TextAnchor.MiddleCenter,true);
                    hud.Text(new Rect(32,662,760,34),"看着右侧的自己，保持喜欢的动作",17,HudPainter.Ink);
                    if(hud.Button(new Rect(1028,665,220,42),"取消倒计时 · Esc",size:15))Back();
                }
                else
                {
                    string guide=!freshPerson?(frame==null?"正在准备人像，请让头和身体进入摄像头":"还没看清你，请让头和身体进入摄像头"):message;
                    hud.Text(new Rect(32,658,710,28),guide,17,freshPerson?HudPainter.Ink:HudPainter.Gold);
                    hud.Text(new Rect(32,688,710,21),"你动，右侧的你也会动 · 拍摄后显示大图预览",12,HudPainter.Muted);
                    GUI.enabled=freshPerson;
                    if(hud.Button(new Rect(793,665,260,42),"拍照 · 5 秒倒计时",HudPainter.Gold,17))Begin();
                    GUI.enabled=true;
                    if(hud.Button(new Rect(1065,665,183,42),"返回 · Esc",size:15))Close();
                }
            }
            float flash=(float)((flashUntil-Time.realtimeSinceStartupAsDouble)/.22);
            if(flash>0)hud.Box(new Rect(0,0,1280,720),new Color(1,1,1,Mathf.Clamp01(flash)*.55f));
        }
        static void Corners(HudPainter hud,Rect r,Color color)
        {
            const float length=20;
            foreach(float x in new[]{r.x,r.xMax})foreach(float y in new[]{r.y,r.yMax})
            {
                hud.Line(new Vector2(x,y),new Vector2(x+(x==r.x?length:-length),y),color,2);
                hud.Line(new Vector2(x,y),new Vector2(x,y+(y==r.y?length:-length)),color,2);
            }
        }
        public void Close()
        {
            if(!Active)return;
            session.Close();client?.Dispose();client=null;composition?.Dispose();composition=null;
            if(person)UnityEngine.Object.Destroy(person);if(saved)UnityEngine.Object.Destroy(saved);person=saved=null;photoPng=null;
            frame=null;freshPerson=validPerson=false;Debug.Log("[Photo] closed; live cutout subscription released");
        }
        public void Dispose()=>Close();
    }
}
