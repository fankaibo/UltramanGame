namespace UltramanGame.Core
{
    public sealed class HeroDefinition
    {
        public readonly string Id,Name,Form,Beam,BeamEnglish,BeamJapanese,BeamVoiceKey;
        public HeroDefinition(string id,string name,string form,string beam,string english,string japanese,string voiceKey)
        {Id=id;Name=name;Form=form;Beam=beam;BeamEnglish=english;BeamJapanese=japanese;BeamVoiceKey=voiceKey;}
    }
    public static class HeroRoster
    {
        static readonly HeroDefinition[] heroes={
            new HeroDefinition("Tiga","迪迦","复合型","哉佩利敖光线","Zeperion Beam","ゼペリオン光線","beam_original"),
            new HeroDefinition("Mebius","梦比优斯","光之勇者","梦比姆射线","Mebium Shoot","メビュームシュート","beam_mebius"),
            new HeroDefinition("Zero","赛罗","光之战士","赛罗集束射线","Wide Zero Shot","ワイドゼロショット","beam_zero"),
            new HeroDefinition("Geed","捷德","机敏型 · Acro Smasher","阿克洛斯梅舍","Acron Smasher","アクロスマッシャー","beam_geed"),
            new HeroDefinition("Grigio","格力乔","治愈之光","格力乔欢呼充能","Grigio Cheer Charge","グリージョチアチャージ","beam_grigio"),
            new HeroDefinition("Zeta","泽塔","原生形态 · Alpha Edge","泽斯帝姆光线","Zestium Beam","ゼスティウム光線","beam_zeta"),
            new HeroDefinition("DeckerStrong","德凯","强劲型 · Strong Type","德凯强力爆破","Dolnade Breaker","ドルネードブレイカー","beam_decker")};
        public static int Count=>heroes.Length;
        public static int Wrap(int index)=>(index%Count+Count)%Count;
        public static HeroDefinition At(int index)=>heroes[Wrap(index)];
        public static int Index(string id){for(int i=0;i<Count;i++)if(heroes[i].Id==id)return i;return 0;}
    }
}
