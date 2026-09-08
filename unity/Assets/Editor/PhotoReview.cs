using System;
using System.IO;
using UnityEngine;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    public static class PhotoReview
    {
        public static void Render()
        {
            string folder=Path.GetFullPath("../artifacts/photo-review");
            var person=new Texture2D(2,2,TextureFormat.RGBA32,false);
            if(!person.LoadImage(File.ReadAllBytes(Path.Combine(folder,"synthetic-person.png"))))throw new Exception("Invalid test PNG");
            using(var composition=new PhotoComposition())
            {
                if(!composition.SetPerson(person))throw new Exception("Synthetic person rejected");
                var shot=composition.Snapshot();
                if(shot.width!=1920||shot.height!=1080)throw new Exception("Wrong export size");
                File.WriteAllBytes(Path.Combine(folder,"photo-composition.png"),shot.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(shot);
                composition.HidePerson();composition.Render();
            }
            UnityEngine.Object.DestroyImmediate(person);
            Debug.Log("[PhotoReview] clean 1920x1080 composition exported; synthetic person only");
        }
    }
}
