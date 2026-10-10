using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    // Export the exact GPU composite, clean plate and linear matte consumed
    // by the Python AI job. All subjects are clearly synthetic test drawings.
    public static class PhotoFusionReview
    {
        public static void Run()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/photo-linear-fusion/render"));
            Directory.CreateDirectory(folder);File.Delete(folder+"/validation.txt");var report=new StringBuilder();
            foreach(string id in new[]{"Tiga","Grigio"})foreach(int width in new[]{1920,2560})
            foreach(string framing in new[]{"full","half","translucent"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                PoseFrame pose=null;
                var source=framing=="translucent"?Swatches():PhotoFramingReview.Person(framing,out pose);
                try
                {
                    using(var photo=new PhotoComposition(width,width*9/16,id))
                    {
                        if(!photo.SetPerson(source,pose))throw new Exception("Rejected fusion input");
                        string stem=folder+"/"+id+"-"+width+"-"+framing;
                        var snapshot=photo.Snapshot();
                        try{File.WriteAllBytes(stem+".png",snapshot.EncodeToPNG());}
                        finally{UnityEngine.Object.DestroyImmediate(snapshot);}
                        File.WriteAllBytes(stem+"-plate.png",photo.CleanPlate());
                        File.WriteAllBytes(stem+"-mask.png",photo.PersonMatte());
                        report.AppendLine($"{id} {width} {framing}: fullBody={photo.FullBody} layers=exported");
                    }
                }
                finally{UnityEngine.Object.DestroyImmediate(source);}
            }
            var hashes=new StringBuilder();using(var sha=System.Security.Cryptography.SHA256.Create())
                foreach(string path in new[]{"Scripts/Runtime/PhotoComposition.cs","Scripts/Runtime/PhotoHero.cs","Scripts/Runtime/PhotoLighting.cs","Scripts/Core/PhotoLayout.cs","Resources/PhotoLayer.shader","Editor/PhotoFusionReview.cs","Editor/PhotoFramingReview.cs"})
                    hashes.AppendLine(path+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Application.dataPath,path)))).Replace("-","").ToLowerInvariant());
            File.WriteAllText(folder+"/sources.txt",hashes.ToString());File.WriteAllText(folder+"/validation.txt",report.ToString());
            Debug.Log("[PhotoFusionReview] PASS\n"+report);
        }
        static Texture2D Swatches()
        {
            var texture=new Texture2D(160,160,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};
            var pixels=new Color32[160*160];byte[] opacity={255,192,128,64,0};
            for(int y=0;y<160;y++)for(int x=0;x<160;x++)
                pixels[y*160+x]=new Color32(193,117,52,y<8||y>151?(byte)255:opacity[Math.Min(4,x/32)]);
            texture.SetPixels32(pixels);texture.Apply();return texture;
        }
    }
}
