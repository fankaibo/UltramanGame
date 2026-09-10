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
    // Match anatomical landmarks, then fit both figures together. Raised arms and a cropped camera frame are not body height.
    public struct PhotoLayout
    {
        public float PersonScale,HeroScale,ShoulderY,HeroShoulderY,HeroBottom;
        public bool FullBody;
        public static bool TryFit(PhotoBody person,PhotoBody hero,out PhotoLayout result)
        {
            result=default;if(!person.Valid||!hero.Valid)return false;
            float ratio=(person.Crown-person.Shoulder)/(hero.Crown-hero.Shoulder);
            float below=person.Shoulder-person.Bottom;
            bool full=below>(hero.Shoulder-hero.Bottom)*ratio;
            // If both full silhouettes fit, anchor feet and crown; shoulder alignment would make one figure float.
            if(full)ratio=(person.Crown-person.Bottom)/(hero.Crown-hero.Bottom);
            float heroBottom=full?hero.Bottom:Math.Max(hero.Bottom,hero.Shoulder-below/ratio);
            float height=Math.Max(person.Top-person.Bottom,(hero.Top-heroBottom)*ratio);
            float extent=Math.Max(Math.Max(person.Center-person.Left,person.Right-person.Center),
                Math.Max(hero.Center-hero.Left,hero.Right-hero.Center)*ratio);
            float scale=Math.Min(8.25f/height,3.4f/extent);
            result=new PhotoLayout{PersonScale=scale,HeroScale=scale*ratio,ShoulderY=-4.5f+below*scale,
                HeroBottom=heroBottom,HeroShoulderY=-4.5f+(hero.Shoulder-heroBottom)*scale*ratio,FullBody=full};
            return true;
        }
    }
}
