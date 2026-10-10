using System;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Security.Cryptography;
using UnityEngine;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    // Explicit opt-in integration check: synthetic pixels only, using the same
    // background worker and local Python entrypoint as a saved family photo.
    public static class PhotoEnhancementWorkerReview
    {
        [Serializable] sealed class Proof
        {
            public string result,source_sha256,output_sha256,worker_sha256,python_sha256,entrypoint_sha256;
            public float seconds;
            public bool original_preserved,left_hero_unchanged,synthetic_only;
        }
        public static void Run()
        {
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));
            string folder=Path.Combine(root,"artifacts/photo-linear-fusion/worker");Directory.CreateDirectory(folder);
            File.Delete(Path.Combine(folder,"validation.json"));
            string rendered=Path.Combine(root,"artifacts/photo-linear-fusion/render/Tiga-1920-full");
            string source=Path.Combine(folder,"synthetic-worker.png");File.Copy(rendered+".png",source,true);
            string originalHash=Hash(source);var watch=Stopwatch.StartNew();
            var job=new LocalPhotoEnhancement(source,File.ReadAllBytes(rendered+"-plate.png"),File.ReadAllBytes(rendered+"-mask.png"));
            while(!job.Done&&watch.Elapsed.TotalSeconds<105)Thread.Sleep(50);
            if(!job.Done||job.ResultPng==null)throw new Exception("Synthetic AI worker did not return an image: "+job.Status+" type="+job.ErrorType);
            if(Hash(source)!=originalHash)throw new Exception("Worker changed original photo");
            var before=new Texture2D(2,2,TextureFormat.RGB24,false);var after=new Texture2D(2,2,TextureFormat.RGB24,false);
            try
            {
                if(!before.LoadImage(File.ReadAllBytes(source))||!after.LoadImage(job.ResultPng)||before.width!=after.width||before.height!=after.height)
                    throw new Exception("Worker output cannot be displayed at original resolution");
                var a=before.GetPixels32();var b=after.GetPixels32();
                for(int y=0;y<before.height;y++)for(int x=0;x<before.width/2;x++)
                {int i=y*before.width+x;if(a[i].r!=b[i].r||a[i].g!=b[i].g||a[i].b!=b[i].b)throw new Exception("Worker changed fixed hero");}
                var proof=new Proof{result="passed",source_sha256=originalHash,output_sha256=Hash(source.Replace(".png","_AI.png")),
                    worker_sha256=Hash(Path.Combine(Application.dataPath,"Scripts/Runtime/LocalPhotoEnhancement.cs")),
                    python_sha256=Hash(Path.Combine(root,"vision/photo_enhance.py")),entrypoint_sha256=Hash(Path.Combine(root,"scripts/enhance_photo.py")),
                    seconds=(float)watch.Elapsed.TotalSeconds,original_preserved=true,left_hero_unchanged=true,synthetic_only=true};
                File.WriteAllText(Path.Combine(folder,"validation.json"),JsonUtility.ToJson(proof,true));
                UnityEngine.Debug.Log("[PhotoEnhancementWorkerReview] PASS "+job.Status+" seconds="+proof.seconds+" fallback="+job.UsedLocalFallback+" error="+job.ErrorType);
            }
            finally{UnityEngine.Object.DestroyImmediate(before);UnityEngine.Object.DestroyImmediate(after);}
        }
        static string Hash(string path)
        {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
    }
}
