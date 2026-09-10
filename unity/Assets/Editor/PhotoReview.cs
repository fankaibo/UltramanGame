using System;
using System.IO;
using UnityEngine;
using UltramanGame.Runtime;
using UltramanGame.Core;

namespace UltramanGame.Editor
{
    public static class PhotoReview
    {
        public static void Proportions()
        {
            string folder=Path.GetFullPath("../artifacts/photo-proportions");Directory.CreateDirectory(folder);
            foreach(bool portrait in new[]{false,true})
            {
                int radius=portrait?70:30,headY=portrait?335:390,shoulder=portrait?225:340,halfWidth=portrait?125:60;
                var texture=new Texture2D(640,480,TextureFormat.RGBA32,false);var pixels=new Color32[640*480];
                for(int y=0;y<480;y++)for(int x=0;x<640;x++)
                {
                    bool head=(x-320)*(x-320)+(y-headY)*(y-headY)<radius*radius;
                    bool body=Math.Abs(x-320)<halfWidth&&y<shoulder+10&&y>(portrait?0:20);
                    if(head||body)pixels[y*640+x]=head?new Color32(230,184,132,255):new Color32(35,165,230,255);
                }
                texture.SetPixels32(pixels);texture.Apply();
                var points=new PosePoint[33];for(int i=0;i<33;i++)points[i]=new PosePoint(.5f,.5f);
                points[0]=new PosePoint(.5f,1-(headY-10)/480f);
                points[11]=new PosePoint((320+halfWidth)/640f,1-shoulder/480f);
                points[12]=new PosePoint((320-halfWidth)/640f,1-shoulder/480f);
                var pose=new PoseFrame{schema=1,source="synthetic",streamId="photo-proportion",tracked=true,sequence=1,capturedMs=10000,points=points};
                using(var composition=new PhotoComposition())
                {
                    if(!composition.SetPerson(texture,pose))throw new Exception("Body framing rejected test portrait");
                    var shot=composition.Snapshot();File.WriteAllBytes(Path.Combine(folder,portrait?"half-body.png":"full-body.png"),shot.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(shot);
                    // In both shots the hero and person's crown-to-shoulder span should agree.
                    var person=GameObject.Find("Victory photo composition/Person").transform;
                    var hero=GameObject.Find("Victory photo composition/Tiga front victory").transform;
                    var personFrame=person.GetComponent<Renderer>().sharedMaterial.GetVector("_Frame");
                    var heroFrame=hero.GetComponent<Renderer>().sharedMaterial.GetVector("_Frame");
                    float personRatio=(headY+radius-shoulder)/480f/personFrame.w*person.localScale.y;
                    float heroRatio=.17f*.5f/heroFrame.w*hero.localScale.y;
                    if(portrait&&Math.Abs(personRatio-heroRatio)>.12f)throw new Exception("Head/shoulder proportion mismatch");
                    if(Math.Abs((person.localPosition.y-person.localScale.y/2)-(hero.localPosition.y-hero.localScale.y/2))>.04f)
                        throw new Exception("Photo figure floats above the paired bottom edge");
                    Debug.Log($"[PhotoProportions] PASS portrait={portrait} personSpan={personRatio:F3} heroSpan={heroRatio:F3}");
                }
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
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
