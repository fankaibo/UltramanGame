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
    public static class CinematicFlashReview
    {
        public static void HighlightShoulder()
        {
            var material=new Material(Resources.Load<Shader>("CinematicComposite"));
            material.SetTexture("_Bloom",Texture2D.blackTexture);material.SetFloat("_Strength",0);material.SetVector("_PulseCenter",Vector4.zero);
            var source=new Texture2D(16,16,TextureFormat.RGBAFloat,false,true);
            var target=new RenderTexture(16,16,0,RenderTextureFormat.ARGBFloat);target.Create();
            var readback=new Texture2D(16,16,TextureFormat.RGBAFloat,false,true);var previous=RenderTexture.active;
            var report=new StringBuilder();float previousPeak=0;
            try
            {
                foreach(float strength in new[]{.1f,.5f,1f,2f,4f,8f})
                {
                    var input=new Color(.55f,.75f,1)*strength;var values=new Color[256];for(int i=0;i<values.Length;i++)values[i]=input;
                    source.SetPixels(values);source.Apply();Graphics.Blit(source,target,material,2);RenderTexture.active=target;
                    readback.ReadPixels(new Rect(0,0,16,16),0,0);readback.Apply();var output=readback.GetPixel(8,8);
                    if(output.b>=1||output.r>=output.g||output.g>=output.b||output.b<=previousPeak)
                        throw new Exception("HDR shoulder clipped, reversed hue channels, or lost intensity ordering");
                    if(strength<=.5f&&Mathf.Max(Mathf.Abs(output.r-input.r),Mathf.Abs(output.g-input.g),Mathf.Abs(output.b-input.b))>.003f)
                        throw new Exception("Highlight shoulder changed the dark/mid scene");
                    previousPeak=output.b;report.AppendLine($"inputPeak={strength:F1} output={output}");
                }
                string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/hero-environment/inspection"));Directory.CreateDirectory(folder);
                File.WriteAllText(folder+"/highlights.txt",report.ToString());Debug.Log("[HighlightShoulder] HDR range 0.1..8, no hard clipping, dark/mid unchanged, channel ordering passed\n"+report);
            }
            finally{RenderTexture.active=previous;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(source);UnityEngine.Object.DestroyImmediate(readback);UnityEngine.Object.DestroyImmediate(material);}
        }
        [MenuItem("UltramanGame/Verify rendered impact flash decay")]
        public static void Run()
        {
            var shader=Resources.Load<Shader>("CinematicComposite");
            if(!shader||ShaderUtil.ShaderHasError(shader))throw new Exception("Compositor shader failed");
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/contact-light"));
            Directory.CreateDirectory(folder);File.Delete(folder+"/validation.txt");var report=new StringBuilder();
            foreach(int width in new[]{256,192})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var world=new GameWorld();var camera=world.Camera;var compositor=camera.GetComponent<CinematicCamera>();
                // A flat dark field isolates the actual GPU compositor from
                // geometry, local lights, smoke and changes in animation.
                camera.cullingMask=0;camera.backgroundColor=new Color(.05f,.06f,.09f);
                camera.transform.SetPositionAndRotation(new Vector3(0,0,-10),Quaternion.identity);
                camera.aspect=width/144f;
                var target=new RenderTexture(width,144,24,RenderTextureFormat.ARGBHalf);target.Create();camera.targetTexture=target;
                var pixels=new Texture2D(width,144,TextureFormat.RGBAFloat,false,true);
                var oldTarget=RenderTexture.active;
                Color[] Read(string name=null)
                {
                    camera.Render();RenderTexture.active=target;
                    pixels.ReadPixels(new Rect(0,0,width,144),0,0);pixels.Apply();
                    if(name!=null)File.WriteAllBytes(Path.Combine(folder,name+".png"),pixels.EncodeToPNG());
                    return pixels.GetPixels();
                }
                Color At(Color[] image,Vector2 uv)=>image[Mathf.Clamp((int)(uv.y*144),0,143)*width+Mathf.Clamp((int)(uv.x*width),0,width-1)];
                float Delta(Color a,Color b)=>Mathf.Max(Mathf.Abs(a.r-b.r),Mathf.Max(Mathf.Abs(a.g-b.g),Mathf.Abs(a.b-b.b)));
                float MaxDelta(Color[] a,Color[] b){float value=0;for(int i=0;i<a.Length;i++)value=Mathf.Max(value,Delta(a[i],b[i]));return value;}
                try
                {
                    var baseline=Read();var uv=new Vector2(.28f,.62f);var far=new Vector2(.94f,.08f);
                    var contact=camera.ViewportToWorldPoint(new Vector3(uv.x,uv.y,5));
                    foreach(int fps in new[]{15,30,60})foreach(bool special in new[]{false,true})
                    {
                        compositor.Clear();compositor.PulseAt(contact,special?new Color(.25f,.68f,1):new Color(1,.48f,.16f),special?.82f:.30f);
                        var peak=Read($"{width}-{fps}-{(special?"beam":"punch")}-peak");
                        float nearDelta=Delta(At(peak,uv),At(baseline,uv)),farDelta=Delta(At(peak,far),At(baseline,far));
                        if(nearDelta<.10f||farDelta>.015f)throw new Exception($"Hit is not localized near={nearDelta:F4} far={farDelta:F4}");
                        compositor.Tick(0);
                        if(MaxDelta(Read(),peak)>.002f||MaxDelta(Read(),peak)>.002f)throw new Exception("Rendering or zero-time changed the flash");
                        compositor.Tick(1f/fps);
                        if(MaxDelta(Read(),peak)>.002f)throw new Exception("Contact flash was not displayed for one step");
                        for(int frame=1;frame<Mathf.CeilToInt(fps*.25f);frame++)compositor.Tick(1f/fps);
                        if(MaxDelta(Read(),baseline)>.002f)throw new Exception("Contact light failed to recover");
                        report.AppendLine($"{width}x144 rate={fps} special={special} near={nearDelta:F4} far={farDelta:F4} zeroTime=passed decay=passed");
                    }
                    // A contact away from the screen center must remain pinned
                    // to its world point after camera recoil, including Y.
                    compositor.PulseAt(contact,Color.white,.82f);camera.transform.position+=Vector3.right*1.1f+Vector3.up*.4f;
                    var projected=(Vector2)camera.WorldToViewportPoint(contact);var moved=Read();
                    if(Delta(At(moved,projected),At(baseline,projected))<.30f)throw new Exception("Flash did not follow camera projection");
                    camera.transform.position=new Vector3(0,0,-10);compositor.Clear();
                    compositor.PulseAt(contact,Color.white,.82f);compositor.Tick(.001f);compositor.Tick(.064f);
                    var wave=Read();float r=Mathf.Lerp(.035f,.52f,.064f/.22f);
                    // Equal pixel distances horizontally and vertically must
                    // carry the same wave; wide-screen circles cannot stretch.
                    Vector2 h=uv+new Vector2(r/camera.aspect,0),v=uv+new Vector2(0,-r);
                    float dh=Delta(At(wave,h),At(baseline,h)),dv=Delta(At(wave,v),At(baseline,v));
                    if(Mathf.Abs(dh-dv)>.025f||Mathf.Min(dh,dv)<.02f)throw new Exception($"Impact wave aspect mismatch {dh:F4} {dv:F4}");
                    compositor.Clear();compositor.PulseAt(camera.transform.position-Vector3.forward,Color.white,.82f);
                    if(MaxDelta(Read(),baseline)>.002f)throw new Exception("Behind-camera impact illuminated the screen");
                    compositor.Clear();compositor.Pulse(new Color(.2f,.68f,1),.42f);
                    var global=Read();float globalDelta=Delta(At(global,new Vector2(.1f,.1f)),At(baseline,new Vector2(.1f,.1f)));
                    if(globalDelta<.02f||globalDelta>.09f)throw new Exception("Transition exposure lost or excessive");
                    compositor.Clear();if(MaxDelta(Read(),baseline)>.002f)throw new Exception("Reset retained flash");
                    // Exercise the real world reset/pause routes as well.
                    var battle=new Battle();compositor.PulseAt(contact,Color.white,.82f);world.ResetPresentation();
                    if(MaxDelta(Read(),baseline)>.002f)throw new Exception("World restart retained flash");
                    battle.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});for(int f=0;f<120;f++)battle.Tick(.02f,new PlayerInput{Tracking=true});
                    compositor.PulseAt(contact,Color.white,.82f);battle.Pause();world.Tick(battle,0,0);
                    if(MaxDelta(Read(),baseline)>.002f)throw new Exception("World pause retained flash");
                    report.AppendLine($"{width}x144 projection=passed circle=passed behindCamera=passed transition={globalDelta:F4} worldPause=passed reset=passed");
                }
                finally {camera.targetTexture=null;RenderTexture.active=oldTarget;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(pixels);}
            }
            File.WriteAllText(folder+"/validation.txt",report.ToString());Debug.Log("[FlashDecayReview] PASS\n"+report);
        }
    }
}
