namespace UltramanGame.Core
{
    public sealed class HeroDefinition
    {
        public readonly string Id,Name,Form,Beam;
        public HeroDefinition(string id,string name,string form,string beam){Id=id;Name=name;Form=form;Beam=beam;}
    }
    public static class HeroRoster
    {
        static readonly HeroDefinition[] heroes={
            new HeroDefinition("Tiga","迪迦","复合型","哉佩利敖光线"),
            new HeroDefinition("Mebius","梦比优斯","光之勇者","梦比姆射线"),
            new HeroDefinition("Zero","赛罗","光之战士","赛罗集束射线"),
            new HeroDefinition("Geed","捷德","光之英雄","毁灭爆裂"),
            new HeroDefinition("Grigio","格力乔","治愈之光","格力乔光线")};
        public static int Count=>heroes.Length;
        public static int Wrap(int index)=>(index%Count+Count)%Count;
        public static HeroDefinition At(int index)=>heroes[Wrap(index)];
        public static int Index(string id){for(int i=0;i<Count;i++)if(heroes[i].Id==id)return i;return 0;}
    }
}
