using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class WindupDustReview
    {
        static string Folder=>Path.GetFullPath(Application.dataPath+"/../../artifacts/windup-pressure-20261012/inspection");
        static void Require(bool value,string message){if(!value)throw new Exception(message);}
        static Battle Ready()
        {
            var b=new Battle();b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
            for(int i=0;i<2000&&b.Enemy!=EnemyPhase.Windup;i++)b.Tick(.02f,new PlayerInput{Tracking=true});
            Require(b.Phase==GamePhase.Battle&&b.Enemy==EnemyPhase.Windup,"Missing warning");return b;
        }
        public static void Check()
        {
            Directory.CreateDirectory(Folder);File.Delete(Folder+"/validation.txt");var report=new StringBuilder();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Warning dust camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=.65f;camera.aspect=2;
            camera.depthTextureMode=DepthTextureMode.Depth;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.045f,.06f,.085f);
            camera.transform.position=new Vector3(0,.55f,-3);camera.transform.LookAt(new Vector3(0,.1f,0));
            var rt=new RenderTexture(512,256,24);rt.Create();camera.targetTexture=rt;
            var pixels=new Texture2D(512,256,TextureFormat.RGB24,false);
            var root=new GameObject("Dust inspection").transform;var dust=new MonsterWindupDust(root);
            Vector3 left=new Vector3(-.32f,.08f,0),right=new Vector3(.32f,.08f,0),forward=Vector3.back;
            Color32[] Read(string name)
            {camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,512,256),0,0);pixels.Apply();if(name!=null)File.WriteAllBytes(Folder+"/"+name+".png",pixels.EncodeToPNG());return pixels.GetPixels32();}
            int Changed(Color32[] a,Color32[] b)
            {int n=0;for(int i=0;i<a.Length;i++)if(Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b)>2)n++;return n;}
            try
            {
                var b=Ready();while(b.EnemyAge<.64f)b.Tick(.02f,new PlayerInput{Tracking=true});
                var clear=Read(null);dust.Tick(b,camera,left,right,forward);var first=Read("visible");int visible=Changed(clear,first);
                Require(visible>40,"Pressure dust is invisible");dust.Tick(b,camera,left,right,forward);Require(Changed(first,Read(null))==0,"Frozen dust moves");
                dust.Tick(b,camera,left+Vector3.right,right+Vector3.right,forward);Require(Changed(first,Read(null))==0,"Emitted dust follows a moving foot");
                b.Tick(.12f,new PlayerInput{Tracking=true});dust.Tick(b,camera,left,right,forward);Require(Changed(first,Read("later"))>40,"Dust does not expand/disperse");
                var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.transform.SetPositionAndRotation(camera.transform.position+camera.transform.forward,camera.transform.rotation);blocker.transform.localScale=new Vector3(5,5,.1f);
                var mat=new Material(Shader.Find("Unlit/Color")){color=new Color(.15f,.24f,.30f)};blocker.GetComponent<Renderer>().sharedMaterial=mat;
                var hidden=Read("occluded");dust.Clear();Require(Changed(hidden,Read(null))==0,"Dust leaks through opaque foreground");UnityEngine.Object.DestroyImmediate(blocker);UnityEngine.Object.DestroyImmediate(mat);
                int frames=0,cases=0;float maximumHeight=0;
                foreach(int hz in new[]{15,30,60})foreach(bool extended in new[]{false,true})
                {
                    b=Ready();dust.Clear();if(extended)b.GiveInstructionTime(5,true);
                    int early=0,late=0,max=0;float dt=1f/hz;
                    for(int n=0;n<hz*100&&b.Blocks<5;n++)
                    {
                        b.Tick(dt,new PlayerInput{Tracking=true,Shield=true});dust.Tick(b,camera,left,right,forward);frames++;
                        max=Math.Max(max,dust.ActiveCount);
                        if(b.Enemy==EnemyPhase.Windup&&dust.ActiveCount>0){if(b.EnemyAge<1.5f)early++;if(b.WarningDuration-b.EnemyAge<.8f)late++;}
                        if(b.Enemy!=EnemyPhase.Windup)Require(dust.ActiveCount==0,"Dust survived warning exit");
                        foreach(Transform child in root)if(child.gameObject.activeSelf){maximumHeight=Math.Max(maximumHeight,child.position.y+child.localScale.y*.5f);Require(child.position.y>=0,"Dust center below arena");}
                    }
                    Require(b.Blocks==5&&early>0&&late>0&&max<=12,"Five attack warnings lost a pressure beat");
                    report.AppendLine($"{hz}Hz extended={extended} blocks=5 earlyFrames={early} lateFrames={late} peakPool={max}");cases++;
                }
                b=Ready();while(b.EnemyAge<.64f)b.Tick(.02f,new PlayerInput{Tracking=true});
                dust.Tick(b,camera,left,right,forward);Require(dust.ActiveCount>0,"No dust before pause");b.Pause();dust.Tick(b,camera,left,right,forward);Require(dust.ActiveCount==0,"Pause retained dust");
                b=Ready();while(b.EnemyAge<.64f)b.Tick(.02f,new PlayerInput{Tracking=true});dust.Tick(b,camera,left,right,forward);dust.Tick(b,camera,left,right,forward,true);Require(dust.ActiveCount==0,"Exclusive shot retained dust");
                dust.Tick(new Battle(),camera,left,right,forward);Require(dust.ActiveCount==0,"New round retained dust");
                Require(root.childCount==12&&maximumHeight<.42f,"Warning dust grows into the hands or allocates objects");
                report.AppendLine($"PASS visiblePixels={visible} zeroTime=unchanged movingFoot=anchored depth=occluded cases={cases} frames={frames} maxTop={maximumHeight:F4} fixedClouds=12 pause/exclusive/newRound=cleared");
                File.WriteAllText(Folder+"/validation.txt",report.ToString());Debug.Log("[WindupDustReview] "+report);
            }
            finally{camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);}
        }
    }
}
