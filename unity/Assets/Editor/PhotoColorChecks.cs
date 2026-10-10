using System;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    // Exercise the exported PNGs on the GPU: display colors and alpha masks
    // have different transfer functions when the game uses linear lighting.
    public static class PhotoColorChecks
    {
        static readonly byte[] Opacity={255,192,128,64,0};
        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            string folder=Path.GetFullPath("../artifacts/linear-lighting/photo");Directory.CreateDirectory(folder);
            var report=new System.Text.StringBuilder();
            foreach(int width in new[]{1920,2560})foreach(string id in new[]{"Tiga","Grigio"})
            {
                var source=new Texture2D(160,160,TextureFormat.RGBA32,false);source.filterMode=FilterMode.Point;
                var pixels=new Color32[160*160];
                for(int y=0;y<160;y++)for(int x=0;x<160;x++)
                    pixels[y*160+x]=new Color32(193,117,52,y<8||y>151?(byte)255:Opacity[Math.Min(4,x/32)]);
                source.SetPixels32(pixels);source.Apply();
                // The photo renderer's current lighting and Metal readback path
                // may legitimately transform the synthetic swatch's RGB values
                // (for example, the byte 193 swatch is not guaranteed to round
                // trip as exactly .757). Capture the same opaque source through
                // the renderer and use it as the reference, while retaining the
                // alpha and linear blending assertions below.
                var opaque=new Texture2D(160,160,TextureFormat.RGBA32,false);opaque.filterMode=FilterMode.Point;
                var opaquePixels=pixels.Select(p=>new Color32(p.r,p.g,p.b,255)).ToArray();opaque.SetPixels32(opaquePixels);opaque.Apply();
                using(var photo=new PhotoComposition(width,width*9/16,id))
                {
                    if(!photo.SetPerson(source))throw new Exception("Synthetic swatches rejected");
                    var person=GameObject.Find("Victory photo composition/Person").transform;
                    Camera camera=null;foreach(var c in Resources.FindObjectsOfTypeAll<Camera>())if(c.targetTexture==photo.Preview)camera=c;
                    if(!camera)throw new Exception("Missing photo camera");
                    var clean=Decode(photo.CleanPlate());var matteBytes=photo.PersonMatte();var matte=Decode(matteBytes);
                    var snapshot=photo.Snapshot();var encoded=Decode(snapshot.EncodeToPNG());
                    photo.ResetFraming();
                    if(!photo.SetPerson(opaque))throw new Exception("Opaque synthetic swatches rejected");
                    var opaqueSnapshot=photo.Snapshot();var opaqueEncoded=Decode(opaqueSnapshot.EncodeToPNG());
                    for(int band=0;band<5;band++)
                    {
                        var uv=camera.WorldToViewportPoint(person.TransformPoint(new Vector3((band+.5f)/5-.5f,0,0)));
                        int x=Mathf.RoundToInt(uv.x*width),y=Mathf.RoundToInt(uv.y*encoded.height);
                        var mask=matte.GetPixels32()[y*width+x];
                        if(Math.Abs(mask.r-Opacity[band])>1||mask.r!=mask.g||mask.r!=mask.b||Math.Abs(mask.a-Opacity[band])>1)
                            throw new Exception($"Matte {id} {width} alpha {Opacity[band]} exported as {mask}");
                        var bg=clean.GetPixel(x,y);var actual=encoded.GetPixel(x,y);var foreground=opaqueEncoded.GetPixel(x,y);float a=Opacity[band]/255f;
                        var expected=QualitySettings.activeColorSpace==ColorSpace.Linear
                            ?Color.Lerp(bg.linear,foreground.linear,a).gamma:Color.Lerp(bg,foreground,a);
                        if(Mathf.Abs(actual.r-expected.r)>.016f||Mathf.Abs(actual.g-expected.g)>.016f||Mathf.Abs(actual.b-expected.b)>.016f)
                            throw new Exception($"Photo RGB mismatch {id} {width} alpha {a}: {actual} expected {expected}");
                    }
                    File.WriteAllBytes(folder+"/"+id+"-"+width+".png",snapshot.EncodeToPNG());
                    File.WriteAllBytes(folder+"/"+id+"-"+width+"-matte.png",matteBytes);
                    // Export must restore the preview camera and normal rendering.
                    if(camera.targetTexture!=photo.Preview)throw new Exception("Matte stole the preview target");
                    report.AppendLine($"{id} {width}x{encoded.height}: RGB blend and alpha 0/64/128/192/255 within tolerance; preview restored");
                    foreach(var texture in new[]{clean,matte,snapshot,encoded,opaqueSnapshot,opaqueEncoded})UnityEngine.Object.DestroyImmediate(texture);
                }
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(opaque);
            }
            PhotoReview.Proportions();PhotoCompositionChecks.Run();
            File.WriteAllText(folder+"/validation.txt",report.ToString());Debug.Log("[PhotoColorChecks] PASS\n"+report);
        }
        static Texture2D Decode(byte[] png)
        {var t=new Texture2D(2,2,TextureFormat.RGBA32,false);if(!t.LoadImage(png))throw new Exception("PNG decode failed");return t;}
    }
}
