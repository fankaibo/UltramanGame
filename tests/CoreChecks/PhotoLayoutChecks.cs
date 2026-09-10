using System;
using UltramanGame.Core;

static class PhotoLayoutChecks
{
    public static void Run(Action<bool,string> check)
    {
        var hero=new PhotoBody(1120,1390,70,435,1280,309,402);
        foreach(var person in new[]{new PhotoBody(180,450,0,425,320,220,400),new PhotoBody(200,420,20,420,320,340,415),
            new PhotoBody(80,590,0,470,320,220,400)})
        {
            check(PhotoLayout.TryFit(person,hero,out var layout),"portrait, full-body and raised-arm compositions are measurable");
            check(layout.FullBody?Math.Abs((person.Crown-person.Bottom)*layout.PersonScale-(hero.Crown-hero.Bottom)*layout.HeroScale)<.0001f:
                Math.Abs((person.Crown-person.Shoulder)*layout.PersonScale-(hero.Crown-hero.Shoulder)*layout.HeroScale)<.0001f,
                "portrait head/shoulder span or full-body crown/feet span matches between figures");
            check(Math.Abs(layout.ShoulderY+(person.Bottom-person.Shoulder)*layout.PersonScale+4.5f)<.001f,
                "cropped camera body meets the frame edge rather than floating above the floor");
            check(layout.HeroShoulderY+(hero.Top-hero.Shoulder)*layout.HeroScale<=3.751f&&
                layout.ShoulderY+(person.Top-person.Shoulder)*layout.PersonScale<=3.751f,"raised hands fit above the paired portraits");
            check(Math.Abs(layout.HeroShoulderY+(layout.HeroBottom-hero.Shoulder)*layout.HeroScale+4.5f)<.001f,
                "hero feet or portrait crop share the person's bottom edge without floating");
        }
        PhotoLayout.TryFit(new PhotoBody(180,450,0,425,320,220,400),hero,out var portrait);
        check(portrait.HeroBottom>hero.Bottom,"half-body person crops hero to a matching portrait");
        var invalid=hero;invalid.Crown=invalid.Shoulder;
        check(!PhotoLayout.TryFit(invalid,hero,out _),"zero body reference cannot create infinite photo scaling");
        invalid=hero;invalid.Center=float.NaN;
        check(!PhotoLayout.TryFit(invalid,hero,out _),"non-finite framing landmarks are rejected");
    }
}
