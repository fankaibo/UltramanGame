using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class CinematicFlashReview
    {
        [MenuItem("UltramanGame/Verify rendered impact flash decay")]
        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var world=new GameWorld();var battle=new Battle();
            battle.Tick(.01f,new PlayerInput{Tracking=true,Transform=true});
            for(int i=0;i<30;i++)battle.Tick(.1f,new PlayerInput{Tracking=true});
            if(battle.Phase!=GamePhase.Battle)throw new Exception("Flash review did not reach battle");
            world.Tick(battle,1,0);
            // Isolate the actual compositor from moving geometry and point lights.
            // Render a fixed scene color so a persistent screen tint cannot hide
            // behind a passing game-state or animation check.
            var camera=world.Camera;camera.cullingMask=0;camera.backgroundColor=new Color(.05f,.06f,.09f);
            var target=new RenderTexture(96,54,24,RenderTextureFormat.ARGB32);target.Create();camera.targetTexture=target;
            var pixels=new Texture2D(96,54,TextureFormat.RGB24,false);
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/flash-decay-review"));Directory.CreateDirectory(folder);
            var oldTarget=RenderTexture.active;
            Color Read(string name=null)
            {
                camera.Render();RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,96,54),0,0);pixels.Apply();
                if(name!=null)File.WriteAllBytes(Path.Combine(folder,name+".png"),pixels.EncodeToPNG());
                return pixels.GetPixel(48,27);
            }
            float Difference(Color a,Color b)=>Mathf.Max(Mathf.Abs(a.r-b.r),Mathf.Max(Mathf.Abs(a.g-b.g),Mathf.Abs(a.b-b.b)));
            try
            {
                Color baseline=Read("baseline");
                foreach(int fps in new[]{15,30,60})
                foreach(bool special in new[]{false,true})
                {
                    world.ResetPresentation();world.Hit(special,battle);
                    Color peak=Read();
                    if(Difference(peak,baseline)<.05f)throw new Exception("Impact flash failed to appear in rendered pixels");
                    world.Tick(battle,0,0);
                    if(Difference(Read(),peak)>.005f)throw new Exception("Zero presentation time advanced the flash");
                    // Rendering twice must not consume the pulse; only explicit
                    // presentation time advances it, in batch and in the player.
                    if(Difference(Read(),peak)>.005f)throw new Exception("Camera render consumed the flash");
                    world.Tick(battle,1f/fps,0);
                    if(Difference(Read(),peak)>.005f)throw new Exception($"Contact-frame flash disappeared at {fps} fps");
                    for(int frame=1;frame<Mathf.CeilToInt(fps*.2f);frame++)world.Tick(battle,1f/fps,frame/(float)fps);
                    float difference=Difference(Read($"recovered-{fps}-{(special?"beam":"punch")}"),baseline);
                    if(difference>.005f)throw new Exception($"Impact flash remained in rendered pixels after 0.2s: fps={fps} special={special} difference={difference:F4}");
                    Debug.Log($"[FlashDecayReview] fps={fps} special={special} recoveredPixelDifference={difference:F4}");
                }
                world.Hit(true,battle);world.ResetPresentation();
                if(Difference(Read("reset"),baseline)>.005f)throw new Exception("Restart retained a screen flash");
                world.Hit(true,battle);battle.Pause();world.Tick(battle,0,0);
                if(Difference(Read("paused"),baseline)>.005f)throw new Exception("Pause retained a screen flash");
                Debug.Log("[FlashDecayReview] PASS rendered pulse, explicit clock, 15/30/60 fps recovery, restart and pause");
            }
            finally
            {
                camera.targetTexture=null;RenderTexture.active=oldTarget;target.Release();
                UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(pixels);
            }
        }
    }
}
