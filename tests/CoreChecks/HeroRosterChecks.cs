using System;
using UltramanGame.Core;

static class HeroRosterChecks
{
    public static void Run(Action<bool,string> check)
    {
        var geed=HeroRoster.At(HeroRoster.Index("Geed"));
        check(geed.Form.Contains("Acro Smasher"),"Geed roster keeps the Agile Acro Smasher form");
        check(geed.BeamEnglish=="Atmos Impact"&&geed.BeamJapanese=="アトモスインパクト"&&geed.Beam=="阿托莫斯冲击",
            "Geed Acro Smasher uses Atmos Impact rather than the form name or Primitive Wrecking Burst");
        check(HeroRoster.At(HeroRoster.Index("Tiga")).BeamJapanese=="ゼペリオン光線","Tiga keeps Zeperion Beam label");
        check(HeroRoster.At(HeroRoster.Index("Mebius")).BeamJapanese=="メビュームシュート","Mebius keeps Mebium Shoot label");
        check(HeroRoster.At(HeroRoster.Index("Zero")).BeamJapanese=="ゼロツインシュート","Zero keeps Twin Shoot label");
        check(HeroRoster.At(HeroRoster.Index("Zeta")).BeamJapanese=="ゼスティウム光線","Zeta keeps Zestium Beam label");
        check(HeroRoster.At(HeroRoster.Index("DeckerStrong")).BeamJapanese=="ドルネードブレイカー","Decker Strong keeps Dolnade Breaker label");
        var grigio=HeroRoster.At(HeroRoster.Index("Grigio"));
        check(grigio.Beam=="格力乔射线"&&grigio.BeamEnglish=="Grigio Shot"&&grigio.BeamJapanese=="グリージョショット",
            "Grigio damage finisher is Shot rather than the restorative Cheer Charge");
        var zero=HeroRoster.At(HeroRoster.Index("Zero"));
        check(zero.BeamDual&&zero.BeamTint=="#67C8FF","Zero finisher keeps a distinct twin-beam presentation profile");
        check(HeroRoster.At(HeroRoster.Index("Geed")).BeamTint!="#5CCBFF"&&
            HeroRoster.At(HeroRoster.Index("Grigio")).BeamTint!="#5CCBFF",
            "non-Tiga finishers do not silently fall back to the generic blue presentation");
    }
}
