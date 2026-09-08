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
        // Renders the same meshes and materials as the player, without a camera feed.
        [MenuItem("UltramanGame/Render character review")]
        public static void Render()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var world=new GameWorld();var state=new Battle();
            var hero=new PrototypeActor("Tiga",new Vector3(-1.1f,0,0));
            var enemy=new PrototypeActor("Golza",new Vector3(1.0f,0,0),true);
            for(int i=0;i<60;i++){hero.Update(null,state,1/60f,i/60f);enemy.Update(null,state,1/60f,i/60f);}
            world.Tick(state,1/60f,1);
            var camera=world.Camera;camera.orthographic=true;camera.orthographicSize=1.90f;
            camera.transform.position=new Vector3(0,1.65f,-9);camera.transform.LookAt(new Vector3(0,1.65f,0));
            var key=new GameObject("Review softbox").AddComponent<Light>();key.type=LightType.Directional;key.intensity=.45f;key.transform.rotation=Quaternion.Euler(25,170,0);
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/characters"));Directory.CreateDirectory(folder);
            var target=new RenderTexture(1800,1100,24,RenderTextureFormat.ARGB32);target.antiAliasing=4;target.Create();camera.targetTexture=target;
            foreach(int angle in new[]{0,65,180})
            {
                hero.Root.rotation=enemy.Root.rotation=Quaternion.Euler(0,180+angle,0);
                camera.Render();RenderTexture.active=target;
                var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();
                File.WriteAllBytes(Path.Combine(folder,"view-"+angle+".png"),texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
            }
            int vertices=0,triangles=0,renderers=0;
            foreach(var actor in new[]{hero.Root,enemy.Root})foreach(var filter in actor.GetComponentsInChildren<MeshFilter>())
            {
                var mesh=filter.sharedMesh;vertices+=mesh.vertexCount;triangles+=mesh.triangles.Length/3;renderers++;
                foreach(var point in mesh.vertices)if(!float.IsFinite(point.x)||!float.IsFinite(point.y)||!float.IsFinite(point.z))throw new Exception("Nonfinite character vertex");
            }
            RenderTexture.active=null;camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);
            Debug.Log($"[CharacterReview] vertices={vertices} triangles={triangles} renderers={renderers} output={folder}");
        }
    }
}
