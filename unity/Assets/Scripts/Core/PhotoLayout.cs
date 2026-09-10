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
        public float PersonScale,HeroScale,ShoulderY,HeroShoulderY,HeroBottom;
        public bool FullBody;
        public static bool TryFit(PhotoBody person,PhotoBody hero,out PhotoLayout result)
        {
            result=default;if(!person.Valid||!hero.Valid)return false;
            float heroScale=Math.Min(6.5f/(hero.Right-hero.Left),7.45f/(hero.Top-hero.Bottom));
            float heroShoulder=-3.9f+(hero.Shoulder-hero.Bottom)*heroScale;
            float below=person.Shoulder-person.Bottom;
            bool full=below/(person.Crown-person.Shoulder)>(hero.Shoulder-hero.Bottom)/(hero.Crown-hero.Shoulder);
            float scale=full?(hero.Crown-hero.Bottom)*heroScale/(person.Crown-person.Bottom):
                (hero.Crown-hero.Shoulder)*heroScale/(person.Crown-person.Shoulder);
            float extent=Math.Max(person.Center-person.Left,person.Right-person.Center);
            scale=Math.Min(scale,3.4f/extent);
            scale=Math.Min(scale,full?7.45f/(person.Top-person.Bottom):
                Math.Min((3.75f-heroShoulder)/(person.Top-person.Shoulder),(heroShoulder+4.5f)/below));
            result=new PhotoLayout{PersonScale=scale,HeroScale=heroScale,ShoulderY=full?-3.9f+below*scale:heroShoulder,
                HeroBottom=hero.Bottom,HeroShoulderY=heroShoulder,FullBody=full};
            return true;
        }
    }
}
