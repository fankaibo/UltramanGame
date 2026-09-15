using System;
using UnityEngine;

namespace UltramanGame.Runtime
{
    public static class DisplayPreferences
    {
        public static int Quality=>Mathf.Clamp(PlayerPrefs.GetInt("display.resolution",1),0,2);
        public static int Width(int quality)=>quality==2?2560:quality==0?1280:1920;
        public static int Height(int quality)=>quality==2?1440:quality==0?720:1080;
        public static string Label(int quality)=>quality==2?"2K · 2560 × 1440":quality==0?"720P · 1280 × 720":"1080P · 1920 × 1080";
        public static void Apply(int quality,bool resize=true)
        {
            QualitySettings.vSyncCount=0;Application.targetFrameRate=60;
            if(resize)Screen.SetResolution(Width(quality),Height(quality),Screen.fullScreenMode);
            Debug.Log($"[Display] requested={Width(quality)}x{Height(quality)} targetFps=60 resize={resize}");
        }
        public static void Startup()
        {
            // Explicit review dimensions still work; ordinary launch uses the saved preference.
            Apply(Quality,Array.IndexOf(Environment.GetCommandLineArgs(),"-screen-width")<0);
        }
    }
}
