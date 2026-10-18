using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class PhotoPersonShadowReview
    {
        public static void Run()
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--person-shadow-output");
            string folder=Path.GetFullPath(at>=0?args[at+1]:"../artifacts/photo-person-shadow");Directory.CreateDirectory(folder);
            var report=new StringBuilder();
            if(ShaderUtil.ShaderHasError(Resources.Load<Shader>("PhotoPersonShadow")))throw new Exception("Person shadow shader failed");
            foreach(int width in new[]{1920,2560})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                using(var photo=new PhotoComposition(width,width*9/16,"Tiga"))
                {
                    var hero=photo.Hero.Root;var position=hero.localPosition;var scale=hero.localScale;var rotation=hero.localRotation;
                    photo.Render(true);byte[] clean=photo.CleanPlate();
                    var shadow=hero.parent.Find("Person/Photo person ground shadow").GetComponent<Renderer>();
                    foreach(string kind in new[]{"full","half","raised"})
                    {
                        var source=PhotoFramingReview.Person(kind,out var pose);photo.ResetFraming();
                        try
                        {
                            for(int i=0;i<5;i++)if(!photo.SetPerson(source,pose))throw new Exception("Invalid person "+kind);
                            if(shadow.enabled!=(kind=="full"))throw new Exception("Shadow without proven full body: "+kind);
                            // Keep receiver bounds constant: toggling the renderer changes
                            // Unity's directional-shadow atlas fit, even with casting off.
                            byte[] matte=photo.PersonMatte();var on=photo.Snapshot();shadow.sharedMaterial.SetFloat("_Strength",0);var off=photo.Snapshot();
                            shadow.sharedMaterial.SetFloat("_Strength",1);var repeat=photo.Snapshot();
                            try
                            {
                                File.WriteAllBytes(folder+$"/{width}-{kind}-on.png",on.EncodeToPNG());File.WriteAllBytes(folder+$"/{width}-{kind}-off.png",off.EncodeToPNG());
                                var a=on.GetPixels32();var b=off.GetPixels32();var c=repeat.GetPixels32();int changed=0,heroPixels=0,upperPixels=0,maxRepeat=0,brightened=0;
                                for(int i=0;i<a.Length;i++)
                                {
                                    int dark=b[i].r+b[i].g+b[i].b-a[i].r-a[i].g-a[i].b;
                                    if(dark>3){changed++;if(i%width<width/2)heroPixels++;if(i/width>on.height*.20f)upperPixels++;}
                                    if(dark< -3)brightened++;
                                    maxRepeat=Math.Max(maxRepeat,Delta(a[i],c[i]));
                                }
                                if(kind=="full"&&changed<100||kind!="full"&&changed>0||heroPixels>0||upperPixels>0||brightened>0||maxRepeat>3)
                                    throw new Exception($"Invalid shadow {kind}/{width}: changed={changed} hero={heroPixels} upper={upperPixels} bright={brightened} repeat={maxRepeat}");
                                if(Convert.ToBase64String(matte)!=Convert.ToBase64String(photo.PersonMatte()))throw new Exception("Shadow altered segmentation matte");
                                File.WriteAllBytes(folder+$"/{width}-{kind}-on.png",on.EncodeToPNG());
                                if(kind=="full")File.WriteAllBytes(folder+$"/{width}-{kind}-off.png",off.EncodeToPNG());
                                report.AppendLine($"{width}-{kind}: shadowPixels={changed} heroPixels={heroPixels} upperPixels={upperPixels} repeatError={maxRepeat} matte=identical");
                            }
                            finally{UnityEngine.Object.DestroyImmediate(on);UnityEngine.Object.DestroyImmediate(off);UnityEngine.Object.DestroyImmediate(repeat);}
                            photo.HidePerson();if(shadow.gameObject.activeInHierarchy)throw new Exception("Tracking loss left shadow active");
                            Compare(clean,photo.CleanPlate());photo.SetPerson(source,pose);Compare(clean,photo.CleanPlate());
                            if(hero.localPosition!=position||hero.localScale!=scale||hero.localRotation!=rotation)throw new Exception("Person changed fixed hero");
                        }
                        finally{UnityEngine.Object.DestroyImmediate(source);}
                    }
                }
            }
            PhotoCompositionChecks.Run();
            File.WriteAllText(folder+"/validation.txt",report+"clean plate, tracking loss, retake, fixed hero and disposal: passed\n");
            Debug.Log("[PersonShadowReview] PASS\n"+report);
        }
        static int Delta(Color32 a,Color32 b)=>Math.Max(Math.Abs(a.r-b.r),Math.Max(Math.Abs(a.g-b.g),Math.Abs(a.b-b.b)));
        static void Compare(byte[] first,byte[] next)
        {
            var a=new Texture2D(2,2);var b=new Texture2D(2,2);
            try
            {
                a.LoadImage(first);b.LoadImage(next);var x=a.GetPixels32();var y=b.GetPixels32();int changed=0,maximum=0;long total=0;
                for(int i=0;i<x.Length;i++){int d=Delta(x[i],y[i]);maximum=Math.Max(maximum,d);total+=d;if(d>2)changed++;}
                if(maximum>32||changed>128||total/(double)x.Length>.003)throw new Exception($"Person/shadow changed clean plate: max={maximum} changed={changed} mean={total/(double)x.Length}");
            }
            finally{UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b);}
        }
    }
}
