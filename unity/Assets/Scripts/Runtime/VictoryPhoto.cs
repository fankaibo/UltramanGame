using System;
using System.IO;
using UnityEngine;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    public sealed class VictoryPhoto : IDisposable
    {
        readonly int port;
        readonly PhotoCountdown countdown=new PhotoCountdown();
        PhotoClient client;
        PhotoComposition composition;
        PhotoFrame frame;
        Texture2D person,saved;
        string message="",savedPath="";
        bool validPerson;
        double flashUntil;
        public bool Active {get;private set;}
        public VictoryPhoto(int port) {this.port=port;}
        public void Open()
        {
            if(Active)return;
            composition=new PhotoComposition();Active=true;Retake();
        }
        void Retake()
        {
            if(countdown.Running)return;
            if(saved)UnityEngine.Object.Destroy(saved);saved=null;savedPath="";frame=null;validPerson=false;
            composition.HidePerson();client?.Dispose();client=new PhotoClient(port);
            message="看向摄像头，摆个喜欢的姿势";countdown.Begin(Time.realtimeSinceStartupAsDouble);
            Debug.Log("[Photo] countdown started seconds=5");
        }
        public void Tick(long now)
        {
            if(!Active||!countdown.Running)return;
            var incoming=client?.TakeLatest();
            if(incoming!=null&&incoming.Fresh(now))
            {
                if(!person)person=new Texture2D(2,2,TextureFormat.RGBA32,false);
                validPerson=incoming.Present&&person.LoadImage(incoming.Png)&&composition.SetPerson(person);
                frame=incoming;
            }
            if(frame==null||!frame.Fresh(now)||!validPerson)composition.HidePerson();
            composition.Render();
            if(!countdown.TakeShot(Time.realtimeSinceStartupAsDouble))return;
            bool usable=frame!=null&&frame.Fresh(now)&&validPerson;
            client?.Dispose();client=null;
            if(!usable)
            {message="这次没有拍到清楚的人像，请站进镜头再试一次";Debug.Log("[Photo] skipped: no fresh person; no file saved");return;}
            try
            {
                saved=composition.Snapshot();savedPath=PhotoFiles.Save(PhotoFiles.Downloads,saved.EncodeToPNG(),frame.Synthetic);
                message="已保存到 Downloads";flashUntil=Time.realtimeSinceStartupAsDouble+.22;
                Debug.Log($"[Photo] saved source={(frame.Synthetic?"synthetic":"camera")} size={saved.width}x{saved.height} file={Path.GetFileName(savedPath)}");
            }
            catch(Exception e) when(e is IOException||e is UnauthorizedAccessException||e is UnityException)
            {message="照片未能保存，请检查 Downloads 文件夹后重拍";Debug.LogWarning("[Photo] save failed: "+e.GetType().Name);}
        }
        public void Draw(HudPainter hud)
        {
            hud.Box(new Rect(0,0,1280,720),new Color(.012f,.025f,.05f));
            hud.Text(new Rect(34,13,650,30),"与迪迦合照",21,HudPainter.Ink,bold:true);
            hud.Text(new Rect(730,17,515,24),frame?.Synthetic==true?"合成测试画面 · 非真人照片":"迪迦在左 · 你在右",13,HudPainter.Cyan,TextAnchor.MiddleRight);
            // Preserve the same 16:9 composition in windowed, Retina and full-screen display.
            GUI.DrawTexture(new Rect(128,61,1024,576),saved?(Texture)saved:composition.Preview,ScaleMode.ScaleToFit,false);
            if(countdown.Running)
            {
                hud.Dot(new Vector2(640,124),76,new Color(.015f,.06f,.11f,.85f));
                hud.Text(new Rect(596,80,88,88),countdown.Remaining(Time.realtimeSinceStartupAsDouble).ToString(),49,HudPainter.Gold,TextAnchor.MiddleCenter,true);
                string guide=frame==null?"正在准备人像…":!validPerson?"向后站一点，让肩膀和身体进入镜头":"看向摄像头，摆个喜欢的姿势";
                hud.Text(new Rect(220,585,840,35),guide,16,HudPainter.Ink,TextAnchor.MiddleCenter);
                if(hud.Button(new Rect(540,659,200,38),"取消 · Esc",size:14))Close();
            }
            else
            {
                hud.Text(new Rect(34,649,670,26),message,15,savedPath.Length>0?HudPainter.Cyan:HudPainter.Gold);
                hud.Text(new Rect(34,678,720,22),savedPath.Length>0?Path.GetFileName(savedPath):"没有保存空白照片；重新拍摄仍会倒计时 5 秒",11,HudPainter.Muted);
                if(hud.Button(new Rect(818,658,182,40),"再拍一张",HudPainter.Gold,15))Retake();
                if(hud.Button(new Rect(1016,658,228,40),"返回游戏 · Esc",size:15))Close();
            }
            float flash=(float)((flashUntil-Time.realtimeSinceStartupAsDouble)/.22);
            if(flash>0)hud.Box(new Rect(0,0,1280,720),new Color(1,1,1,Mathf.Clamp01(flash)*.55f));
        }
        public void Close()
        {
            if(!Active)return;
            countdown.Cancel();client?.Dispose();client=null;composition?.Dispose();composition=null;
            if(person)UnityEngine.Object.Destroy(person);if(saved)UnityEngine.Object.Destroy(saved);person=saved=null;
            frame=null;Active=false;Debug.Log("[Photo] closed; live cutout subscription released");
        }
        public void Dispose()=>Close();
    }
}
