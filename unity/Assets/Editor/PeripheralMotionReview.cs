using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class PeripheralMotionReview
    {
        public static void After()
        {Validate();CinematicFlashReview.Run();CinematicFlashReview.HighlightShoulder();ExchangeReview.After();}
        static Battle Pose(string kind)
        {
            var state=new Battle();state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
            for(int n=0;n<(Battle.TransformationSeconds+0.2f)/(.02f);n++)state.Tick(.02f,new PlayerInput{Tracking=true});
            if(kind=="left"||kind=="right")
            {
                state.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=kind=="left",RightPunch=kind=="right"});
                for(int n=0;n<5;n++)state.Tick(.02f,new PlayerInput{Tracking=true});
            }
            else if(kind=="rush"||kind=="hurt")
            {
                for(int n=0;n<1500;n++)
                {
                    state.Tick(.02f,new PlayerInput{Tracking=true,Shield=kind=="rush"});
                    if(kind=="rush"&&state.Enemy==EnemyPhase.Attack&&state.EnemyAge>=.34f)break;
                    if(kind=="hurt"&&state.Action==HeroAction.Hurt&&state.ActionAge>=.10f)break;
                }
            }
            else if(kind=="beam")
            {
                state.GiveInstructionTime(20);
                for(int n=0;n<15;n++)
                {state.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});for(int i=0;i<24;i++)state.Tick(.02f,new PlayerInput{Tracking=true});}
                state.Tick(.02f,new PlayerInput{Tracking=true,Beam=true});
                if(state.Action!=HeroAction.Beam)throw new Exception("Finisher exclusion was not exercised");
            }
            return state;
        }
        public static void Validate()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--exchange-output");
            string output=at>=0&&at+1<args.Length?args[at+1]:Path.Combine(Application.dataPath,"../../artifacts/peripheral-motion");
            string folder=Path.GetFullPath(Path.Combine(output,"gpu"));Directory.CreateDirectory(folder);
            File.Delete(folder+"/validation.txt");var report=new StringBuilder();
            var shader=Resources.Load<Shader>("CinematicComposite");
            if(!shader||ShaderUtil.ShaderHasError(shader))throw new Exception("Peripheral compositor shader failed");
            foreach(var size in new[]{new Vector2Int(640,360),new Vector2Int(640,400),new Vector2Int(640,480),new Vector2Int(840,360)})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var world=new GameWorld();var camera=world.Camera;var compositor=camera.GetComponent<CinematicCamera>();
                camera.cullingMask=0;camera.backgroundColor=new Color(.05f,.06f,.09f);camera.aspect=size.x/(float)size.y;
                var target=new RenderTexture(size.x,size.y,24,RenderTextureFormat.ARGBHalf);target.Create();camera.targetTexture=target;
                var readback=new Texture2D(size.x,size.y,TextureFormat.RGBAFloat,false,true);var previous=RenderTexture.active;
                Color[] Read()
                {camera.Render();RenderTexture.active=target;readback.ReadPixels(new Rect(0,0,size.x,size.y),0,0);readback.Apply();return readback.GetPixels();}
                float Delta(Color a,Color b)=>Mathf.Max(Mathf.Abs(a.r-b.r),Mathf.Abs(a.g-b.g),Mathf.Abs(a.b-b.b));
                float MaxDelta(Color[] a,Color[] b){float maximum=0;for(int i=0;i<a.Length;i++)maximum=Mathf.Max(maximum,Delta(a[i],b[i]));return maximum;}
                try
                {
                    foreach(float lens in new[]{14f,29.2f,43f})
                    {
                        camera.fieldOfView=lens;compositor.Clear();var baseline=Read();
                        foreach(string kind in new[]{"left","right","rush","hurt"})
                        {
                            var state=Pose(kind);compositor.CombatMotion(state);var shown=Read();
                            float central=0,outer=0,topBottom=0;int visible=0;
                            for(int y=0;y<size.y;y++)for(int x=0;x<size.x;x++)
                            {
                                float d=Delta(shown[y*size.x+x],baseline[y*size.x+x]);
                                if(float.IsNaN(d)||float.IsInfinity(d))throw new Exception("Nonfinite motion composite");
                                float u=(x+.5f)/size.x,v=(y+.5f)/size.y;
                                if(u>=.11f&&u<=.89f)central=Mathf.Max(central,d);else{outer=Mathf.Max(outer,d);if(d>.003f)visible++;}
                                if(v<=.06f||v>=.94f)topBottom=Mathf.Max(topBottom,d);
                            }
                            if(central>.001f||topBottom>.001f||outer<.02f||visible<20)
                                throw new Exception($"Motion escaped border or disappeared: {size} {lens} {kind} center={central} outer={outer} visible={visible}");
                            compositor.Tick(0);compositor.CombatMotion(state);
                            if(MaxDelta(shown,Read())>.001f)throw new Exception("Repeated frozen motion sample changed");
                            state.Pause();compositor.CombatMotion(state);
                            if(MaxDelta(baseline,Read())>.001f)throw new Exception("Pause retained peripheral motion");
                            report.AppendLine($"{size.x}x{size.y} fov={lens:F1} mode={kind} central={central:F5} edgePeak={outer:F5} pixels={visible} frozen=passed pause=passed");
                        }
                        foreach(string kind in new[]{"idle","beam"})
                        {compositor.CombatMotion(Pose(kind));if(MaxDelta(baseline,Read())>.001f)throw new Exception(kind+" retained motion");}
                        compositor.CombatMotion(Pose("left"),true);
                        if(MaxDelta(baseline,Read())>.001f)throw new Exception("Showcase retained motion");
                        compositor.CombatMotion(Pose("left"));world.ResetPresentation();
                        if(MaxDelta(baseline,Read())>.001f)throw new Exception("New round retained motion");
                        report.AppendLine($"{size.x}x{size.y} fov={lens:F1} idle=passed beam=passed showcase=passed reset=passed");
                    }
                }
                finally{camera.targetTexture=null;RenderTexture.active=previous;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(readback);}
            }
            File.WriteAllText(folder+"/validation.txt",report.ToString());Debug.Log("[PeripheralMotionReview] PASS\n"+report);
        }
    }
}
