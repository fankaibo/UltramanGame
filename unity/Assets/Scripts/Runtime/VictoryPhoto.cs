using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    public sealed class VictoryPhoto : IDisposable
    {
        readonly int port;
        public string HeroId="Tiga";
        // Keyboard shortcuts remain available to a parent, but the guided
        // camera flow must not present them as a required child interaction.
        public bool KeyboardMode {get;set;}=true;
        readonly GameAudio sound;
        readonly GuidedPhoto session=new GuidedPhoto();
        readonly PhotoChoiceGesture choice=new PhotoChoiceGesture();
        readonly Queue<PoseFrame> photoPoses=new Queue<PoseFrame>();
        long photoPoseStamp;
        PhotoClient client;
        PhotoComposition composition;
        PhotoFrame frame;
        Texture2D person,saved,originalSaved;
        Texture cameraPreview;
        byte[] photoPng;
        string message="",savedPath="";
        bool validPerson,freshPerson,reportedPerson;
        bool enhancementReady,showEnhanced=true,comparePhotos;
        float enhancementMeanDelta,enhancementChangedPct,enhancementSubjectMeanDelta;
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
        LocalPhotoEnhancement enhancement;
        public VictoryPhoto(int port,GameAudio sound=null) {this.port=port;this.sound=sound;}
        double Now=>Time.realtimeSinceStartupAsDouble;
        float Say(string key)
        {sound?.Speak(key,6,GamePhase.Victory);return sound?.VoiceLength(key)??0;}
        public void Open()
        {
            if(Active)return;
            PlayAgainRequested=false;int quality=Math.Max(1,DisplayPreferences.Quality);
            composition=new PhotoComposition(DisplayPreferences.Width(quality),DisplayPreferences.Height(quality),HeroId);Prepare();
        }
        void Prepare()
        {
            DestroyPhotoTextures();photoPng=null;savedPath="";
            enhancementReady=false;showEnhanced=true;comparePhotos=false;enhancementMeanDelta=enhancementChangedPct=enhancementSubjectMeanDelta=0;
            enhancement=null;
            frame=null;validPerson=freshPerson=reportedPerson=false;composition.HidePerson();composition.ResetFraming();choice.Reset();
            photoPoses.Clear();photoPoseStamp=0;
            client?.Dispose();client=new PhotoClient(port);previousNumber=0;
            float seconds=Say("photo_intro");session.Open(Now,seconds);nextGuide=Now+seconds+8;
            message="看着右边的自己，摆个喜欢的姿势";
            Debug.Log($"[Photo] automatic live viewfinder opened voice={seconds:F2} reaction=3 countdown=5");
        }
        public void Back()
        {
            // Parent-only keyboard escape remains available; the child flow uses gestures.
            Close();PlayAgainRequested=true;
        }
        public void PlayAgain(){if(Stage!=PhotoStage.Review)return;Debug.Log("[Photo] button=play-again");Close();PlayAgainRequested=true;}
        public void Retake(){if(Stage!=PhotoStage.Review)return;Debug.Log("[Photo] button=retake");Prepare();}
        public void ResumeGuidance(){if(Active&&Stage!=PhotoStage.Review){session.Open(Now,0);previousNumber=0;}}
        public void Tick(long now,PoseFrame pose=null,Texture liveCamera=null)
        {
            if(!Active)return;
            cameraPreview=liveCamera;
            if(pose!=null&&pose.capturedMs!=photoPoseStamp)
            {photoPoseStamp=pose.capturedMs;photoPoses.Enqueue(pose);while(photoPoses.Count>48)photoPoses.Dequeue();}
            if(session.Stage==PhotoStage.Review)
            {
                if(savedPath.Length==0&&photoPng!=null&&Now>=nextSaveRetry)SavePhoto();
                if(enhancement!=null&&enhancement.Done)
                {
                    var resultPng=enhancement.TakeResultPng();
                    if(resultPng!=null)
                    {var edited=new Texture2D(2,2,TextureFormat.RGB24,false);if(edited.LoadImage(resultPng))
                        {ReportEnhancementDelta(originalSaved,edited,enhancement.UsedLocalFallback,out enhancementMeanDelta,out enhancementChangedPct,out enhancementSubjectMeanDelta);if(saved&&saved!=originalSaved)UnityEngine.Object.Destroy(saved);saved=edited;enhancementReady=true;showEnhanced=true;comparePhotos=true;message=enhancement.UsedLocalFallback?"本地环境光版已另存到 Downloads":"AI 光色版已另存到 Downloads";}
                        else UnityEngine.Object.Destroy(edited);}
                    else message=enhancement.Status;
                    enhancement=null;
                }
                // Observe the hands-down release DURING the spoken review;
                // only selecting a menu item waits for the narration to finish.
                var selected=choice.Update(pose,now,Now>=reviewReadyAt);
                if(selected==PhotoChoice.Retake) {Debug.Log("[Photo] gesture=retake");Prepare();}
                if(selected==PhotoChoice.PlayAgain)
                {Debug.Log("[Photo] gesture=play-again");Close();PlayAgainRequested=true;}
                return;
            }
            var incoming=client?.TakeLatest();
            if(incoming!=null)
            {
                if(!person)person=new Texture2D(2,2,TextureFormat.RGBA32,false);
                PoseFrame nearest=null;long distance=221;
                foreach(var candidate in photoPoses)
                {
                    long age=Math.Abs(candidate.capturedMs-incoming.CapturedMs);
                    if(age<distance){distance=age;nearest=candidate;}
                }
                validPerson=incoming.Fresh(now)&&incoming.Present&&person.LoadImage(incoming.Png)&&composition.SetPerson(person,nearest);
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
                saved=composition.Snapshot();originalSaved=saved;photoPng=saved.EncodeToPNG();Captures++;
                flashUntil=Now+.22;SavePhoto();
                // The file is written (and, when enabled, the enhancement
                // worker has copied its own input files). The network frame
                // is no longer needed for review.
                if(frame!=null)frame.Png=null;
                choice.Reset();
                // The narration already tells the player to lower then raise
                // their hands.  A second multi-second grace period made the
                // next round feel stuck; keep only a short buffer after the
                // voice ends, while PhotoChoiceGesture still requires a fresh
                // neutral interval before accepting the raised-hands pose.
                reviewReadyAt=Now+Math.Max(2.5,Say("photo_saved")+.45);
                Debug.Log($"[Photo] automatic capture complete; frozen review; choiceReadyIn={(reviewReadyAt-Now):F2}s; hands-down gate active");
            }
            catch(UnityException e)
            {Prepare();Debug.LogWarning("[Photo] snapshot failed: "+e.GetType().Name);}
        }
        static void ReportEnhancementDelta(Texture2D before,Texture2D after,bool fallback,out float meanDelta,out float changedPct,out float subjectMeanDelta)
        {
            meanDelta=changedPct=subjectMeanDelta=0;
            if(!before||!after||before.width!=after.width||before.height!=after.height)return;
            var a=before.GetPixels32();var b=after.GetPixels32();long sum=0;int changed=0;
            long subjectSum=0;int subjectCount=0;
            int count=Math.Min(a.Length,b.Length);
            int split=Mathf.RoundToInt(before.width*.48f);
            for(int i=0;i<count;i++)
            {int d=Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b);sum+=d;if(d>=12)changed++;
                int x=i%before.width;if(x>=split){subjectSum+=d;subjectCount++;}}
            meanDelta=sum/(float)Math.Max(1,count);changedPct=changed*100f/Math.Max(1,count);
            subjectMeanDelta=subjectSum/(float)Math.Max(1,subjectCount);
            Debug.Log($"[PhotoAI] result loaded fallback={fallback} meanDelta={meanDelta:F2} subjectMeanDelta={subjectMeanDelta:F2} changedPct={changedPct:F1}");
        }
        void SavePhoto()
        {
            nextSaveRetry=Now+10;
            try
            {
                savedPath=PhotoFiles.Save(PhotoFiles.Downloads,photoPng,frame.Synthetic);
                // Keep the retry buffer only when the write failed. A saved
                // 1080P/2K PNG can otherwise remain alongside the Texture2D
                // and AI worker result throughout the review screen.
                photoPng=null;
                message="已保存到 Downloads";
                if(PlayerPrefs.GetInt("photo.ai",1)==1&&!frame.Synthetic)
                {enhancement=new LocalPhotoEnhancement(savedPath,composition.CleanPlate(),composition.PersonMatte());message="原图已保存 · AI 正在匹配环境光与边缘";}
                Debug.Log($"[Photo] saved source={(frame.Synthetic?"synthetic":"camera")} size={saved.width}x{saved.height} file={Path.GetFileName(savedPath)}");
            }
            catch(Exception e) when(e is IOException||e is UnauthorizedAccessException)
            {message="照片已保留，正在重试保存";Debug.LogWarning("[Photo] save failed: "+e.GetType().Name);}
        }
        public void Draw(HudPainter hud)
        {
            bool review=session.Stage==PhotoStage.Review,counting=session.Stage==PhotoStage.Countdown;
            hud.Box(new Rect(0,0,1280,720),new Color(.012f,.025f,.05f));
            Texture reviewImage=showEnhanced&&enhancementReady&&saved?saved:(Texture)(originalSaved??saved);
            if(review&&enhancementReady&&comparePhotos)
            {
                // A single full-screen toggle makes subtle lighting changes
                // easy to miss. The side-by-side view keeps both files at the
                // same scale and gives the parent a concrete before/after.
                hud.Image(new Rect(0,64,640,548),originalSaved,ScaleMode.ScaleToFit);
                hud.Image(new Rect(640,64,640,548),saved,ScaleMode.ScaleToFit);
                hud.Box(new Rect(0,64,640,28),new Color(.005f,.02f,.05f,.82f));
                hud.Box(new Rect(640,64,640,28),new Color(.005f,.02f,.05f,.82f));
                hud.Text(new Rect(18,68,270,20),"原图",13,HudPainter.Muted,bold:true);
                hud.Text(new Rect(658,68,360,20),"AI 光色融合版",13,HudPainter.Cyan,bold:true);
            }
            else hud.Image(new Rect(0,0,1280,720),review&&reviewImage?reviewImage:composition.Preview,ScaleMode.ScaleToFit);
            if(!review&&!freshPerson&&cameraPreview)
            {
                // Visible, honest fallback while native cutout initializes/reconnects.
                // This camera layer is HUD only and can never enter the saved composition.
                hud.Rounded(new Rect(720,112,465,456),new Color(.015f,.04f,.075f,.86f));
                hud.Image(new Rect(735,128,435,402),cameraPreview,ScaleMode.ScaleToFit);
                hud.Text(new Rect(735,529,435,30),"实时镜头 · 正在准备镂空人像",16,HudPainter.Gold,TextAnchor.MiddleCenter);
            }
            hud.Box(new Rect(0,0,1280,64),new Color(.008f,.025f,.06f,.76f));
            hud.Text(new Rect(32,12,750,38),review?"光之英雄 · 合照纪念":"光之英雄 · 与"+HeroRoster.At(HeroRoster.Index(HeroId)).Name+"的胜利合照",25,HudPainter.Ink,bold:true);
            hud.Text(new Rect(900,16,345,29),frame?.Synthetic==true?"合成测试 · 非真人":review?"照片预览 · 已定格":"实时镂空取景",14,HudPainter.Cyan,TextAnchor.MiddleRight);
            if(review&&enhancementReady)
            {
                string label=comparePhotos?"全屏查看":"原图 / AI 并排对比";
                if(hud.Button(new Rect(1004,76,240,32),label,comparePhotos?HudPainter.Gold:HudPainter.Cyan,14))comparePhotos=!comparePhotos;
                if(!comparePhotos&&hud.Button(new Rect(748,76,244,32),showEnhanced?"查看原图":"查看 AI 光色版",showEnhanced?HudPainter.Cyan:HudPainter.Gold,14))showEnhanced=!showEnhanced;
                hud.Text(new Rect(748,111,496,18),comparePhotos?$"对比完成 · 全图影响 {enhancementChangedPct:F1}% · 人像平均差异 {enhancementSubjectMeanDelta:F1}":showEnhanced?$"AI 光色融合版 · 人像平均差异 {enhancementSubjectMeanDelta:F1}":"原图对照",11,showEnhanced?HudPainter.Cyan:HudPainter.Muted,TextAnchor.MiddleRight);
            }
            hud.Box(new Rect(0,612,1280,108),new Color(.008f,.025f,.06f,.88f));
            if(review)
            {
                hud.Text(new Rect(32,617,600,30),message,17,HudPainter.Cyan);
                bool reading=Now<reviewReadyAt;
                hud.Text(new Rect(32,647,1216,24),reading?"先放下双手，慢慢欣赏我们的合照":!choice.Armed?"先放下双手，再举高开始下一局":"单手举高重拍 · 双手举高再玩一次",18,HudPainter.Ink,TextAnchor.MiddleCenter,true);
                if(KeyboardMode)
                {
                    if(hud.Button(new Rect(378,677,244,32),"再拍一张 · 空格",HudPainter.Cyan,15))Retake();
                    if(hud.Button(new Rect(656,677,244,32),"再玩一次 · Enter",HudPainter.Gold,15))PlayAgain();
                }
                hud.Bar(new Rect(460,714,360,3),reading?0:choice.Progress,HudPainter.Gold);
            }
            else
            {
                Corners(hud,new Rect(720,94,470,496),freshPerson?new Color(.3f,.9f,1,.4f):new Color(1,.76f,.38f,.6f));
                hud.Text(new Rect(770,73,365,28),freshPerson?"这是你 · 姿势实时可见":"让头和身体进入镜头",17,freshPerson?HudPainter.Cyan:HudPainter.Gold,TextAnchor.MiddleCenter);
                hud.Text(new Rect(60,633,1000,37),counting?"保持喜欢的姿势，马上自动拍照":freshPerson?message:"站进镜头，画面准备好后会自动倒数",25,HudPainter.Ink,TextAnchor.MiddleCenter,true);
                hud.Text(new Rect(60,677,1000,24),freshPerson&&!composition.FullBody?"半身合照也可以 · 退后让双脚入镜，可拍全身照":counting?"不需要点击 · 拍完自动显示合照":"先听完引导，再留三秒准备 · 不需要键盘或鼠标",14,HudPainter.Muted,TextAnchor.MiddleCenter);
                if(freshPerson&&cameraPreview)
                {
                    // Keep this HUD-only reference inside the existing footer;
                    // the old top-right window covered the person's face and raised arm.
                    var reference=new Rect(1100,636,152,76);
                    hud.Text(new Rect(1094,616,164,19),"姿势参考 · 实时镜头",10,HudPainter.Cyan,TextAnchor.MiddleCenter,true);
                    hud.Image(reference,cameraPreview,ScaleMode.ScaleToFit);
                }
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
            if(person)UnityEngine.Object.Destroy(person);DestroyPhotoTextures();photoPng=null;
            frame=null;freshPerson=validPerson=false;cameraPreview=null;Debug.Log("[Photo] closed; live cutout subscription released");
        }
        void DestroyPhotoTextures()
        {
            if(saved&&saved!=originalSaved)UnityEngine.Object.Destroy(saved);
            if(originalSaved)UnityEngine.Object.Destroy(originalSaved);
            person=null;saved=originalSaved=null;
        }
        public void Dispose()=>Close();
    }
}
