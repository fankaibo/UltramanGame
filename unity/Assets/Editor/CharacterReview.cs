using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class CharacterReview
    {
        // Uses the player shader, UV frames and arena; no webcam is opened or captured.
        [MenuItem("UltramanGame/Render character review")]
        public static void Render()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var world=new GameWorld();var state=new Battle();
            var hero=new AnimatedActor("Tiga",new Vector3(-1.55f,0,0));
            var enemy=new AnimatedActor("Golza",new Vector3(1.55f,0,0),true);
            world.Tick(state,1,1);
            var camera=world.Camera;camera.orthographic=true;camera.orthographicSize=2.15f;
            camera.transform.position=new Vector3(0,1.75f,-10);camera.transform.LookAt(new Vector3(0,1.75f,0));
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/animation-review"));Directory.CreateDirectory(folder);
            var target=new RenderTexture(1800,1100,24,RenderTextureFormat.ARGB32);target.antiAliasing=4;target.Create();camera.targetTexture=target;
            for(int frame=0;frame<8;frame++)
            {
                hero.Update(state,camera,0,0,frame);enemy.Update(state,camera,0,0,frame);
                if(hero.Frame!=frame||enemy.Frame!=frame)throw new Exception("Action atlas frame mismatch");
                camera.Render();RenderTexture.active=target;
                var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();
                File.WriteAllBytes(Path.Combine(folder,"pose-"+frame+".png"),texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
            }
            RenderTexture.active=null;camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);
            Debug.Log($"[CharacterReview] frames=16 renderers=2 output={folder}");
        }
    }
}
