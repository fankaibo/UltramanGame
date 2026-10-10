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
            check(!layout.FullBody||Math.Abs((person.Crown-person.Bottom)*layout.PersonScale-(hero.Crown-hero.Bottom)*layout.HeroScale)<.0001f,
                "full-body crown/feet span matches the hero when horizontal space permits");
            float bottom=layout.ShoulderY+(person.Bottom-person.Shoulder)*layout.PersonScale;
            check(layout.FullBody?Math.Abs(bottom+3.9f)<.001f:bottom<=-4.5f&&bottom>=-4.6f,
                "full person aligns feet; cropped torso continues beyond the photograph boundary");
            check(layout.PersonX+(person.Left-person.Center)*layout.PersonScale>=.299f&&
                layout.PersonX+(person.Right-person.Center)*layout.PersonScale<=7.101f,"all visible person pixels fit beside the fixed hero");
            check(layout.HeroShoulderY+(hero.Top-hero.Shoulder)*layout.HeroScale<=3.751f&&
                layout.ShoulderY+(person.Top-person.Shoulder)*layout.PersonScale<=3.751f,"raised hands fit above the paired portraits");
            check(layout.HeroScale==fixedHero.HeroScale&&layout.HeroShoulderY==fixedHero.HeroShoulderY&&layout.HeroBottom==hero.Bottom,
                "hero size, position and full-body crop remain invariant for every person framing");
        }
        PhotoLayout.TryFit(new PhotoBody(180,450,0,425,320,220,400),hero,out var portrait);
        check(portrait.HeroBottom==hero.Bottom,"half-body camera never crops the fixed full-body hero");
        var child=new PhotoBody(180,450,10,430,320,270,425);
        PhotoLayout.TryFit(child,hero,out var childLayout,true);
        check(childLayout.FullBody,"visible ankles identify a child's full body despite adult hero proportions");
        check(Math.Abs(childLayout.ShoulderY+(child.Bottom-child.Shoulder)*childLayout.PersonScale+3.9f)<.001f,
            "child full-body photo is grounded instead of floating at adult shoulder height");
        check(childLayout.HeroScale==fixedHero.HeroScale,"child proportion correction does not resize the hero");
        var family=new PhotoBody(0,610,0,395,160,230,390);
        PhotoLayout.TryFit(family,hero,out var group,false);
        check(Math.Abs(group.PersonX+((family.Left+family.Right)/2-family.Center)*group.PersonScale-3.7f)<.001f,
            "two-person portrait centers the group rather than the only tracked face");
        check(group.ShoulderY+(family.Bottom-family.Shoulder)*group.PersonScale<=-4.5f,
            "wide family portrait cannot float above the photograph edge");
        PhotoLayout.TryFit(new PhotoBody(180,450,0,460,320,320,400),hero,out var cropped,false);
        check(!cropped.FullBody,"camera crop overrides inferred body proportions");
        var invalid=hero;invalid.Crown=invalid.Shoulder;
        check(!PhotoLayout.TryFit(invalid,hero,out _),"zero body reference cannot create infinite photo scaling");
        invalid=hero;invalid.Center=float.NaN;
        check(!PhotoLayout.TryFit(invalid,hero,out _),"non-finite framing landmarks are rejected");

        float scale=.82f,maxScaleStep=0;
        for(int i=0;i<36;i++)
        {
            float next=PhotoLayout.Smooth(scale,1.62f,.20f,.028f);
            maxScaleStep=Math.Max(maxScaleStep,Math.Abs(next-scale));scale=next;
        }
        check(maxScaleStep<=.028001f,"photo scale rejects one-frame matte jumps");
        check(scale>1.60f,"photo scale still converges during sustained real movement");
        foreach(float initial in new[]{.008f,.018f,.04f})
        {
            float value=initial;
            for(int i=0;i<48;i++)
            {
                float next=PhotoLayout.SmoothScale(value,initial*1.6f);
                check(Math.Abs(next/value-1)<=.025001f,"source-pixel photo scale caps each delivery at 2.5 percent");
                value=next;
            }
            check(Math.Abs(value/initial-1.6f)<.003f,"real distance compensation converges at camera cadence");
            check(PhotoLayout.SmoothScale(value,-1)==value,"invalid negative scale is ignored");
            check(PhotoLayout.SmoothScale(value,float.NaN)==value,"invalid scale target is ignored");
        }
        float x=3.7f,maxPositionStep=0;
        foreach(float target in new[]{2.1f,4.9f,2.1f,4.9f,2.1f,4.9f})
        {
            float next=PhotoLayout.Smooth(x,target,.24f,.050f);
            maxPositionStep=Math.Max(maxPositionStep,Math.Abs(next-x));x=next;
        }
        check(maxPositionStep<=.050001f,"photo position rejects alternating matte noise");
        float invalidCurrent=0;check(Math.Abs(PhotoLayout.Smooth(float.NaN,2,.2f,.1f)-2)<.0001f,
            "photo smoothing recovers from an invalid initial state");
        check(Math.Abs(PhotoLayout.Smooth(invalidCurrent,float.NaN,.2f,.1f))<.0001f,
            "photo smoothing ignores an invalid target");
    }
}
