using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    // Synthetic matte/landmark sequences through the real composition; no family photos.
    public static class PhotoMotionReview
    {
        public static void Before()=>Run(false);
        public static void After()=>Run(true);
        static float Value(PhotoComposition photo,string name)=>(float)typeof(PhotoComposition).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(photo);
        static void Run(bool verify)
        {
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--photo-motion-output");
            string folder=Path.GetFullPath(args[at+1]);Directory.CreateDirectory(folder);
            var csv=new StringBuilder("case,frame,scale,crown,shoulder,heroScale,stepFraction,bottom\n");
            foreach(string scenario in new[]{"overhead","matte-noise","head-gap","distance","pose-gap"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                using(var photo=new PhotoComposition(1280,720))
                {
                    var heroScale=photo.Hero.Root.localScale;var heroPosition=photo.Hero.Root.localPosition;
                    float previous=0,reference=0,maxStep=0,maxCrownError=0,maxFloorError=0;
                    for(int frame=0;frame<48;frame++)
                    {
                        var texture=Input(scenario,frame,out var pose);
                        try
                        {
                            if(!photo.SetPerson(texture,scenario=="pose-gap"&&frame>=8?null:pose))throw new Exception("Input rejected");
                            float scale=Value(photo,"stablePersonScale"),crown=Value(photo,"lastCrown");
                            float step=previous==0?0:Math.Abs(scale/previous-1);maxStep=Math.Max(maxStep,step);
                            if(frame==0)reference=scale;
                            if((scenario=="overhead"||scenario=="head-gap"||scenario=="pose-gap"))maxCrownError=Math.Max(maxCrownError,Math.Abs(crown-329));
                            var quad=(Transform)typeof(PhotoComposition).GetField("personQuad",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(photo);
                            float bottom=quad.localPosition.y-quad.localScale.y/2;maxFloorError=Math.Max(maxFloorError,Math.Abs(bottom+3.9f));
                            csv.AppendLine($"{scenario},{frame},{scale:R},{crown:R},{Value(photo,"lastShoulder"):R},{heroScale.x:R},{step:R},{bottom:R}");
                            if(frame==0||frame==23||frame==47)
                            {var shot=photo.Snapshot();File.WriteAllBytes(Path.Combine(folder,$"{scenario}-{frame:D2}.jpg"),shot.EncodeToJPG(90));UnityEngine.Object.DestroyImmediate(shot);}
                            if(photo.Hero.Root.localScale!=heroScale||photo.Hero.Root.localPosition!=heroPosition)throw new Exception("Hero changed");
                            previous=scale;
                        }
                        finally{UnityEngine.Object.DestroyImmediate(texture);}
                    }
                    Debug.Log($"[PhotoMotionReview] {scenario} maxStep={maxStep:R} crownError={maxCrownError:R} finalScaleRatio={previous/reference:R} floorError={maxFloorError:R}");
                    if(verify&&(maxFloorError>.001f||maxStep>.02501f||((scenario=="overhead"||scenario=="head-gap"||scenario=="pose-gap")&&(maxCrownError>2||Math.Abs(previous/reference-1)>.001))))
                        throw new Exception("Photo scale/crown stability failed: "+scenario);
                }
            }
            File.WriteAllText(Path.Combine(folder,"measurements.csv"),csv.ToString());
            Debug.Log("[PhotoMotionReview] "+(verify?"PASS":"BASELINE"));
        }
        static Texture2D Input(string scenario,int frame,out PoseFrame pose)
        {
            var pixels=new Color32[640*480];var skin=new Color32(235,188,140,255);var shirt=new Color32(20,175,230,255);
            void Rect(int x,int y,int w,int h,Color32 c)
            {for(int j=y;j<Math.Min(480,y+h);j++)for(int i=x;i<Math.Min(640,x+w);i++)pixels[j*640+i]=c;}
            Rect(272,100,96,155,shirt);Rect(292,250,56,30,skin);
            for(int y=270;y<330;y++)for(int x=290;x<350;x++)if((x-320)*(x-320)+(y-300)*(y-300)<900)pixels[y*640+x]=skin;
            Rect(278,25,32,90,shirt);Rect(330,25,32,90,shirt);
            // A lifted arm remains beside the head. The other hand crosses above
            // it, separated by clear air; body size and total bounds stay fixed.
            Rect(395,240,25,175,shirt);Rect(390,415,35,20,skin);
            if(scenario!="matte-noise"&&scenario!="distance"&&frame>=8&&frame<32)Rect(292,392,56,28,skin);
            // Alternating segmentation outliers widen the matte, not the body.
            if(scenario=="matte-noise"&&frame%2==1)Rect(40,240,30,25,shirt);
            if(scenario=="head-gap"&&frame>=8&&frame<32)Rect(290,310,60,2,new Color32(0,0,0,0));
            var p=new PosePoint[33];for(int i=0;i<33;i++)p[i]=new PosePoint(.5f,.5f,0);
            p[0]=new PosePoint(.5f,1-290f/480);p[11]=new PosePoint(1-272f/640,1-250f/480);p[12]=new PosePoint(1-368f/640,1-250f/480);
            p[25]=new PosePoint(.45f,.72f);p[26]=new PosePoint(.55f,.72f);p[27]=new PosePoint(.45f,.94f);p[28]=new PosePoint(.55f,.94f);
            if(scenario=="distance"&&frame>=8&&frame<32)
            {
                const float zoom=.85f;var moved=new Color32[pixels.Length];
                for(int y=0;y<480;y++)for(int x=0;x<640;x++)
                {int sx=Mathf.RoundToInt((x-320)/zoom+320),sy=Mathf.RoundToInt((y-240)/zoom+240);if(sx>=0&&sx<640&&sy>=0&&sy<480)moved[y*640+x]=pixels[sy*640+sx];}
                pixels=moved;
                foreach(int id in new[]{0,11,12,25,26,27,28})p[id]=new PosePoint(.5f+(p[id].x-.5f)*zoom,.5f+(p[id].y-.5f)*zoom);
            }
            pose=new PoseFrame{schema=1,source="synthetic",streamId="photo-motion",sequence=frame+1,capturedMs=10000+frame*125,tracked=true,points=p};
            var image=new Texture2D(640,480,TextureFormat.RGBA32,false);image.SetPixels32(pixels);image.Apply();return image;
        }
    }
}
