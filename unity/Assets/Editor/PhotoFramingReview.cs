using System;
using System.IO;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    // Repeatable cutout inputs exercise the real offscreen camera. No family
    // pictures, model service or live camera is involved in this review.
    public static class PhotoFramingReview
    {
        public static void Before()=>Run(true);
        public static void After()=>Run(false);
        static void Run(bool before)
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/photo-framing/"+(before?"before":"after")));
            Directory.CreateDirectory(folder);File.Delete(folder+"/validation.txt");var report=new StringBuilder();
            foreach(string id in before?new[]{"Tiga"}:new[]{"Tiga","Mebius","Zero","Geed","Grigio"})
            foreach(int width in id=="Tiga"?new[]{1920,2560}:new[]{1920})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                using(var photo=new PhotoComposition(width,width*9/16,id))
                {
                    var hero=photo.Hero.Root;var scale=hero.localScale;var position=hero.localPosition;var rotation=hero.localRotation;
                    photo.Render(true);byte[] firstPlate=photo.CleanPlate();
                    foreach(string framing in new[]{"half","group","full","raised"})
                    {
                        var source=Person(framing,out var pose);
                        try
                        {
                            if(!before&&framing=="raised")
                            {
                                // The previous case left a proven full-body
                                // pose cached. A newly cropped image wins even
                                // when its accompanying pose is delayed.
                                if(!photo.SetPerson(source)||photo.FullBody)throw new Exception("Stale full-body pose overrode camera crop");
                            }
                            photo.ResetFraming();
                            for(int i=0;i<5;i++)if(!photo.SetPerson(source,pose))throw new Exception("Rejected "+framing);
                            string name=id+"-"+width+"-"+framing;
                            var picture=photo.Snapshot();File.WriteAllBytes(folder+"/"+name+".png",picture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(picture);
                            var matte=new Texture2D(2,2);matte.LoadImage(photo.PersonMatte());
                            var pixels=matte.GetPixels32();int minX=width,minY=matte.height,maxX=0,maxY=0;
                            for(int y=0;y<matte.height;y++)for(int x=0;x<width;x++)if(pixels[y*width+x].r>100)
                            {minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);}
                            if(!before)
                            {
                                if(minX<width/2||maxX>=width-5)throw new Exception("Person overlaps hero or horizontal frame: "+name);
                                if(framing!="full"&&minY>2)throw new Exception("Portrait still floats: "+name+" bottom="+minY);
                                if(framing=="full"&&Math.Abs(minY-matte.height*.6/9)>12*width/1920f)throw new Exception("Feet are not grounded: "+name);
                                if(maxY>matte.height*.91f)throw new Exception("Raised hand/head cropped: "+name);
                                if(photo.FullBody!=(framing=="full"))throw new Exception("Incorrect body classification: "+name);
                            }
                            report.AppendLine($"{name}: full={photo.FullBody} personPixels={minX},{minY}..{maxX},{maxY} fixedHero=passed");
                            UnityEngine.Object.DestroyImmediate(matte);
                            photo.HidePerson();photo.Render();photo.ResetFraming();photo.SetPerson(source,pose);photo.Render();
                            if(hero.localPosition!=position||hero.localScale!=scale||hero.localRotation!=rotation)
                                throw new Exception("Person framing or retake changed hero");
                            byte[] nextPlate=photo.CleanPlate();
                            CheckPlate(firstPlate,nextPlate,folder,name);
                            File.WriteAllBytes(folder+"/input-"+framing+".png",source.EncodeToPNG());
                        }
                        finally{UnityEngine.Object.DestroyImmediate(source);}
                    }
                }
            }
            var sources=new StringBuilder();using(var sha=System.Security.Cryptography.SHA256.Create())
                foreach(string path in new[]{"Scripts/Runtime/PhotoComposition.cs","Scripts/Runtime/PhotoHero.cs","Scripts/Runtime/PhotoLighting.cs","Scripts/Core/PhotoLayout.cs","Editor/PhotoFramingReview.cs"})
                    sources.AppendLine(path+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Application.dataPath,path)))).Replace("-","").ToLowerInvariant());
            File.WriteAllText(folder+"/sources.txt",sources.ToString());File.WriteAllText(folder+"/validation.txt",report.ToString());
            Debug.Log("[PhotoFramingReview] "+(before?"BASELINE":"PASS")+"\n"+report);
        }
        static void CheckPlate(byte[] a,byte[] b,string folder,string name)
        {
            var first=new Texture2D(2,2);var next=new Texture2D(2,2);
            try
            {
                first.LoadImage(a);next.LoadImage(b);var p=first.GetPixels32();var q=next.GetPixels32();
                // GPU shadows have sparse byte rounding at edges. Bound both
                // the affected area and mean error, as in the comparison tool.
                int changed=0,maximum=0;long sum=0;
                for(int i=0;i<p.Length;i++)
                {
                    int r=Math.Abs(p[i].r-q[i].r),g=Math.Abs(p[i].g-q[i].g),blue=Math.Abs(p[i].b-q[i].b);
                    int delta=Math.Max(r,Math.Max(g,blue));maximum=Math.Max(maximum,delta);sum+=r+g+blue;if(delta>2)changed++;
                }
                if(maximum>32||changed>128||sum/(double)(p.Length*3)>.001)
                {File.WriteAllBytes(folder+"/plate-first.png",a);File.WriteAllBytes(folder+"/plate-next.png",b);throw new Exception("Person altered hero/background pixels: "+name);}
            }
            finally{UnityEngine.Object.DestroyImmediate(first);UnityEngine.Object.DestroyImmediate(next);}
        }
        internal static Texture2D Person(string kind,out PoseFrame pose)
        {
            var pixels=new Color32[640*480];bool full=kind=="full",group=kind=="group",raised=kind=="raised";
            int cx=group?210:320,cy=full?382:330,r=full?36:65,shoulder=full?320:230;
            void Circle(int x,int y,int radius,Color32 color)
            {for(int py=Math.Max(0,y-radius);py<Math.Min(480,y+radius);py++)for(int px=Math.Max(0,x-radius);px<Math.Min(640,x+radius);px++)
                if((px-x)*(px-x)+(py-y)*(py-y)<radius*radius)pixels[py*640+px]=color;}
            void Rect(int x,int y,int w,int h,Color32 color)
            {for(int py=Math.Max(0,y);py<Math.Min(480,y+h);py++)for(int px=Math.Max(0,x);px<Math.Min(640,x+w);px++)pixels[py*640+px]=color;}
            var skin=new Color32(230,184,132,255);var shirt=new Color32(35,165,230,255);var dark=new Color32(35,45,65,255);
            int half=full?56:95;
            Rect(cx-half,full?105:0,half*2,shoulder+6-(full?105:0),shirt);Rect(cx-r/2,shoulder,r,cy-shoulder,skin);Circle(cx,cy,r,skin);
            Circle(cx-r/3,cy+8,full?3:5,dark);Circle(cx+r/3,cy+8,full?3:5,dark);Rect(cx-r/4,cy-r/3,r/2,4,dark);
            if(full){Rect(cx-52,22,37,100,dark);Rect(cx+15,22,37,100,dark);}
            if(raised){Rect(cx+100,shoulder-25,34,205,shirt);Circle(cx+117,shoulder+185,23,skin);Rect(cx+half-10,shoulder-25,50,34,shirt);}
            if(group){Rect(361,0,160,191,new Color32(245,175,70,255));Rect(415,184,45,55,skin);Circle(438,273,55,skin);Circle(420,280,4,dark);Circle(455,280,4,dark);}
            var texture=new Texture2D(640,480,TextureFormat.RGBA32,false);texture.SetPixels32(pixels);texture.Apply();
            var p=new PosePoint[33];for(int i=0;i<33;i++)p[i]=new PosePoint(.5f,.5f,0);
            p[0]=new PosePoint(1-cx/640f,1-(cy-10)/480f);p[11]=new PosePoint(1-(cx-half)/640f,1-shoulder/480f);p[12]=new PosePoint(1-(cx+half)/640f,1-shoulder/480f);
            if(full){p[25]=new PosePoint(.44f,.74f);p[26]=new PosePoint(.56f,.74f);p[27]=new PosePoint(.44f,.94f);p[28]=new PosePoint(.56f,.94f);}
            pose=new PoseFrame{schema=1,source="synthetic",streamId="photo-framing",sequence=1,capturedMs=10000,tracked=true,points=p};return texture;
        }
    }
}
