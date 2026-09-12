using System;
using UltramanGame.Core;

static class PhotoLayoutChecks
{
    public static void Run(Action<bool,string> check)
    {
        var hero=new PhotoBody(1120,1390,70,435,1280,309,402);
        PhotoLayout.TryFit(new PhotoBody(180,450,0,425,320,220,400),hero,out var fixedHero);
        foreach(var person in new[]{new PhotoBody(180,450,0,425,320,220,400),new PhotoBody(200,420,20,420,320,340,415),
            new PhotoBody(80,590,0,470,320,220,400)})
        {
            check(PhotoLayout.TryFit(person,hero,out var layout),"portrait, full-body and raised-arm compositions are measurable");
            check(layout.FullBody?Math.Abs((person.Crown-person.Bottom)*layout.PersonScale-(hero.Crown-hero.Bottom)*layout.HeroScale)<.0001f:
                Math.Abs((person.Crown-person.Shoulder)*layout.PersonScale-(hero.Crown-hero.Shoulder)*layout.HeroScale)<.0001f,
                "portrait head/shoulder span or full-body crown/feet span matches between figures");
            check(layout.FullBody?Math.Abs(layout.ShoulderY+(person.Bottom-person.Shoulder)*layout.PersonScale+3.9f)<.001f:
                Math.Abs(layout.ShoulderY-layout.HeroShoulderY)<.001f,"full person aligns feet; portrait aligns shoulders without enlarging the hero");
            check(layout.HeroShoulderY+(hero.Top-hero.Shoulder)*layout.HeroScale<=3.751f&&
                layout.ShoulderY+(person.Top-person.Shoulder)*layout.PersonScale<=3.751f,"raised hands fit above the paired portraits");
            check(layout.HeroScale==fixedHero.HeroScale&&layout.HeroShoulderY==fixedHero.HeroShoulderY&&layout.HeroBottom==hero.Bottom,
                "hero size, position and full-body crop remain invariant for every person framing");
        }
        PhotoLayout.TryFit(new PhotoBody(180,450,0,425,320,220,400),hero,out var portrait);
        check(portrait.HeroBottom==hero.Bottom,"half-body camera never crops the fixed full-body hero");
        var invalid=hero;invalid.Crown=invalid.Shoulder;
        check(!PhotoLayout.TryFit(invalid,hero,out _),"zero body reference cannot create infinite photo scaling");
        invalid=hero;invalid.Center=float.NaN;
        check(!PhotoLayout.TryFit(invalid,hero,out _),"non-finite framing landmarks are rejected");
    }
}
