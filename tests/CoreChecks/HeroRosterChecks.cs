using System;
using UltramanGame.Core;

static class HeroRosterChecks
{
    public static void Run(Action<bool,string> check)
    {
        var geed=HeroRoster.At(HeroRoster.Index("Geed"));
        check(geed.Form.Contains("Acro Smasher"),"Geed roster keeps the Agile Acro Smasher form");
        check(geed.BeamEnglish=="Acron Smasher"&&geed.BeamJapanese=="アクロスマッシャー",
            "Geed Agile form uses Acron Smasher rather than Primitive Wrecking Burst");
        check(HeroRoster.At(HeroRoster.Index("Tiga")).BeamJapanese=="ゼペリオン光線","Tiga keeps Zeperion Beam label");
        check(HeroRoster.At(HeroRoster.Index("Mebius")).BeamJapanese=="メビュームシュート","Mebius keeps Mebium Shoot label");
        check(HeroRoster.At(HeroRoster.Index("Zero")).BeamJapanese=="ゼロツインシュート","Zero keeps Twin Shoot label");
        check(HeroRoster.At(HeroRoster.Index("Zeta")).BeamJapanese=="ゼスティウム光線","Zeta keeps Zestium Beam label");
        check(HeroRoster.At(HeroRoster.Index("DeckerStrong")).BeamJapanese=="ドルネードブレイカー","Decker Strong keeps Dolnade Breaker label");
    }
}
