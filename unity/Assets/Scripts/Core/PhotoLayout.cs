using System;

namespace UltramanGame.Core
{
    public struct PhotoBody
    {
        public float Left,Right,Bottom,Top,Center,Shoulder,Crown;
        public PhotoBody(float left,float right,float bottom,float top,float center,float shoulder,float crown)
        {Left=left;Right=right;Bottom=bottom;Top=top;Center=center;Shoulder=shoulder;Crown=crown;}
        public bool Valid=>Finite(Left)&&Finite(Right)&&Finite(Bottom)&&Finite(Top)&&Finite(Center)&&Finite(Shoulder)&&Finite(Crown)&&
            Right>Left&&Top>Bottom&&Center>=Left&&Center<=Right&&Shoulder>Bottom&&Crown>Shoulder&&Crown<=Top;
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }
    // The hero is a fixed photo landmark. Only the camera person adapts to distance and framing.
    public struct PhotoLayout
    {
        public float PersonScale,PersonX,HeroScale,ShoulderY,HeroShoulderY,HeroBottom;
        public bool FullBody;
        // Camera cutouts arrive much more slowly than the render loop.  Smooth
        // the accepted target first, then cap the single-delivery delta so a
        // noisy matte cannot make the person visibly jump in size or position.
        public static float Smooth(float current,float target,float response,float maxStep)
        {
            if(float.IsNaN(current)||float.IsInfinity(current))return target;
            if(float.IsNaN(target)||float.IsInfinity(target))return current;
            float mix=Math.Max(0,Math.Min(1,response));
            float cap=Math.Max(0,maxStep);
            float proposed=current+(target-current)*mix;
            float delta=proposed-current;
            if(delta>cap)return current+cap;
            if(delta<-cap)return current-cap;
            return proposed;
        }
        public static bool TryFit(PhotoBody person,PhotoBody hero,out PhotoLayout result,bool? fullBodyHint=null)
        {
            result=default;if(!person.Valid||!hero.Valid)return false;
            float heroScale=Math.Min(6.5f/(hero.Right-hero.Left),7.45f/(hero.Top-hero.Bottom));
            float heroShoulder=-3.9f+(hero.Shoulder-hero.Bottom)*heroScale;
            float below=person.Shoulder-person.Bottom;
            bool full=fullBodyHint??(below/(person.Crown-person.Shoulder)>(hero.Shoulder-hero.Bottom)/(hero.Crown-hero.Shoulder));
            float scale,x=3.7f,bottom;
            if(full)
            {
                scale=(hero.Crown-hero.Bottom)*heroScale/(person.Crown-person.Bottom);
                float extent=Math.Max(person.Center-person.Left,person.Right-person.Center);
                scale=Math.Min(scale,Math.Min(3.4f/extent,7.45f/(person.Top-person.Bottom)));
                bottom=-3.9f;
            }
            else
            {
                // A cropped torso cannot stand on the same floor as a whole
                // hero. Frame it as a foreground portrait ending at the photo
                // edge, without creating legs or resizing/cropping the hero.
                scale=Math.Min(6.8f/(person.Right-person.Left),7.9f/(person.Top-person.Bottom));
                bottom=-4.53f;
                // Pose detects only one face in a family group. Center the
                // entire silhouette so the second person is not squeezed out.
                x+=(person.Center-(person.Left+person.Right)/2)*scale;
            }
            result=new PhotoLayout{PersonScale=scale,PersonX=x,HeroScale=heroScale,ShoulderY=bottom+below*scale,
                HeroBottom=hero.Bottom,HeroShoulderY=heroShoulder,FullBody=full};
            return true;
        }
    }
}
