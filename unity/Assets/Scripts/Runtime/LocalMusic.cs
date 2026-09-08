using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace UltramanGame.Runtime
{
    public sealed class LocalMusic : MonoBehaviour
    {
        const string Preference="sound.localMusic";
        GameAudio sound;
        Coroutine loading;
        readonly byte[] pathBuffer=new byte[16384];
        public bool Loading { get; private set; }
        public bool Choosing { get; private set; }
        public string Status { get; private set; }="可导入《奇迹再现》或其他喜欢的音乐";
        public string SelectedName { get; private set; }="内置战斗音乐";
        public bool HasSelection { get; private set; }
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        [DllImport("UltramanMusicPicker")] static extern void TigaBeginMusicChoice();
        [DllImport("UltramanMusicPicker")] static extern int TigaPollMusicChoice([Out] byte[] buffer,int capacity);
#endif
        public void Initialize(GameAudio audio)
        {
            sound=audio;
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--music");
            string path=i>=0&&i+1<args.Length?args[i+1]:PlayerPrefs.GetString(Preference,"");
            if(!string.IsNullOrEmpty(path))Load(path);
        }
        public void Choose()
        {
            if(Loading||Choosing)return;
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            try
            {
                TigaBeginMusicChoice();Choosing=true;
            }
            catch(DllNotFoundException) {Status="音乐导入组件缺失，请重新构建游戏";}
            catch(EntryPointNotFoundException) {Status="音乐导入组件需要更新，请重新构建游戏";}
#else
            Status="此版本的文件选择器支持 macOS";
#endif
        }
        void Update()
        {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            if(!Choosing)return;
            int size=TigaPollMusicChoice(pathBuffer,pathBuffer.Length);
            if(size==0)return;
            Choosing=false;
            if(Debug.isDebugBuild)Debug.Log("[MusicPicker] "+(size>0?"selected":size==-1?"cancelled":"path too long"));
            if(size>0)Load(Encoding.UTF8.GetString(pathBuffer,0,size));
            else if(size==-2)Status="文件路径过长，请把音乐移到较短的路径";
#endif
        }
        public void Load(string path)
        {
            if(loading!=null)StopCoroutine(loading);
            Loading=false;
            AudioType type;
            switch(Path.GetExtension(path).ToLowerInvariant())
            {
                case ".mp3":type=AudioType.MPEG;break;
                case ".wav":type=AudioType.WAV;break;
                case ".ogg":type=AudioType.OGGVORBIS;break;
                case ".aif":case ".aiff":type=AudioType.AIFF;break;
                default:Status="请选择 MP3、WAV、OGG 或 AIFF 音频";return;
            }
            if(!File.Exists(path)) {Status="找不到上次的音乐，请重新选择；已保留当前配乐";return;}
            if(new FileInfo(path).Length>80*1024*1024) {Status="请选择小于 80 MB 的音频";return;}
            loading=StartCoroutine(Read(Path.GetFullPath(path),type));
        }
        IEnumerator Read(string path,AudioType type)
        {
            Loading=true;Status="正在载入音乐…";
            using(var request=UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri.Replace("+","%2B"),type))
            {
                // An entire normal-length song fits locally and loops without streaming gaps.
                request.timeout=20;
                yield return request.SendWebRequest();
                if(request.result!=UnityWebRequest.Result.Success)
                {Debug.LogWarning($"[LocalMusic] decode failed result={request.result} error={request.error}");Status="音频无法读取，请选择普通音频文件；已保留当前配乐";Loading=false;yield break;}
                var clip=DownloadHandlerAudioClip.GetContent(request);
                if(!clip||clip.length<1||clip.length>15*60)
                {
                    if(clip)Destroy(clip);
                    Status="请选择 1 秒至 15 分钟的完整音乐";Loading=false;yield break;
                }
                clip.name=Path.GetFileNameWithoutExtension(path);
                sound.UseLocalMusic(clip);
                SelectedName=clip.name;HasSelection=true;
                PlayerPrefs.SetString(Preference,path);PlayerPrefs.Save();
                Status="本机循环播放 · 语音时自动降低音乐音量";
                Debug.Log($"[LocalMusic] loaded duration={clip.length:F2}s channels={clip.channels} loop=true");
            }
            Loading=false;loading=null;
        }
        public void BuiltIn()
        {
            if(loading!=null)StopCoroutine(loading);
            loading=null;Loading=false;sound.UseLocalMusic(null);
            PlayerPrefs.DeleteKey(Preference);PlayerPrefs.Save();
            SelectedName="内置战斗音乐";HasSelection=false;
            Status="可导入《奇迹再现》或其他喜欢的音乐";
        }
    }
}
