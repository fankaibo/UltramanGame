namespace UltramanGame.Core
{
    public sealed class HeroDefinition
    {
        public readonly string Id,Name,Form,Beam,BeamEnglish,BeamJapanese,BeamVoiceKey;
        // Presentation metadata is deliberately plain data so the Unity-free
        // roster and its checks can lock the identity of each finisher without
        // pulling renderer types into the combat rules assembly.
        public readonly string BeamTint,BeamAccent;
        public readonly bool BeamDual;
        public HeroDefinition(string id,string name,string form,string beam,string english,string japanese,string voiceKey)
            :this(id,name,form,beam,english,japanese,voiceKey,"#5CCBFF","#E9FBFF",false){}
        public HeroDefinition(string id,string name,string form,string beam,string english,string japanese,string voiceKey,string tint,string accent,bool dual)
        {Id=id;Name=name;Form=form;Beam=beam;BeamEnglish=english;BeamJapanese=japanese;BeamVoiceKey=voiceKey;BeamTint=tint;BeamAccent=accent;BeamDual=dual;}
    }
    public static class HeroRoster
    {
        static readonly HeroDefinition[] heroes={
            new HeroDefinition("Tiga","迪迦","复合型","哉佩利敖光线","Zeperion Beam","ゼペリオン光線","beam_original","#52C8FF","#F1FCFF",false),
            new HeroDefinition("Mebius","梦比优斯","光之勇者","梦比姆射线","Mebium Shoot","メビュームシュート","beam_mebius","#FF8A68","#FFF2D6",false),
            new HeroDefinition("Zero","赛罗","光之战士","赛罗双射击","Zero Twin Shoot","ゼロツインシュート","beam_zero","#67C8FF","#FFFFFF",true),
            new HeroDefinition("Geed","捷德","机敏型 · Acro Smasher","阿托莫斯冲击","Atmos Impact","アトモスインパクト","beam_geed","#B79BFF","#F4E7FF",false),
            new HeroDefinition("Grigio","格力乔","治愈之光","格力乔欢呼充能","Grigio Cheer Charge","グリージョチアチャージ","beam_grigio","#FF8BC7","#FFF0FA",false),
            new HeroDefinition("Zeta","泽塔","原生形态 · Alpha Edge","泽斯帝姆光线","Zestium Beam","ゼスティウム光線","beam_zeta","#72D5FF","#FFE16B",false),
            new HeroDefinition("DeckerStrong","德凯","强劲型 · Strong Type","德凯强力爆破","Dolnade Breaker","ドルネードブレイカー","beam_decker","#FFD15A","#FFF4BC",false)};
        public static int Count=>heroes.Length;
        public static int Wrap(int index)=>(index%Count+Count)%Count;
        public static HeroDefinition At(int index)=>heroes[Wrap(index)];
        public static int Index(string id){for(int i=0;i<Count;i++)if(heroes[i].Id==id)return i;return 0;}
    }
}
