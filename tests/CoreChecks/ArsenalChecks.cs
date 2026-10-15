using System;
using UltramanGame.Core;

static class ArsenalChecks
{
    static Battle Start()
    {
        var b=new Battle();b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
        while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});return b;
    }
    static void Rock(Battle b,float dt)
    {for(int n=0;n<4000&&!(b.Enemy==EnemyPhase.Attack&&b.EnemyAttackCount==2);n++)b.Tick(dt,new PlayerInput{Tracking=true,Shield=true});}
    public static void Run(Action<bool,string> check)
    {
        check(!MonsterStepMotion.ClawLeft(1)&&MonsterStepMotion.ClawLeft(5)&&!MonsterStepMotion.ClawLeft(7)&&MonsterStepMotion.ClawLeft(11),"claws still alternate when even attack slots use ranged weapons");
        for(int i=1;i<=12;i++)
            check((MonsterRockMotion.Variant(i)?1:0)+(MonsterRayMotion.Variant(i)?1:0)+(MonsterSlamMotion.Variant(i)?1:0)<=1,"exclusive enemy weapon cycle "+i);
        foreach(int fps in new[]{15,30,60})foreach(bool shield in new[]{false,true})
        {
            float dt=1f/fps;var b=Start();Rock(b,dt);int hits=b.HitsTaken,blocks=b.Blocks;
            check(MonsterRockMotion.Active(b)&&b.EnemyContactSeconds==MonsterRockMotion.Contact,"second attack selects rock at "+fps);
            while(b.EnemyAge+dt<MonsterRockMotion.Contact)b.Tick(dt,new PlayerInput{Tracking=true,Shield=shield});
            check(b.HitsTaken==hits&&b.Blocks==blocks,"stone in flight cannot hit early "+fps+shield);
            b.Tick(dt,new PlayerInput{Tracking=true,Shield=shield});
            check(b.HitsTaken==hits+(shield?0:1)&&b.Blocks==blocks+(shield?1:0),"stone resolves once on arrival "+fps+shield);
            for(int i=0;i<120;i++)b.Tick(dt,new PlayerInput{Tracking=true,Shield=shield});
            check(b.HitsTaken==hits+(shield?0:1)&&b.Blocks==blocks+(shield?1:0),"fragments do not add damage "+fps+shield);
        }
        var cancel=Start();Rock(cancel,.02f);
        for(int i=0;i<18;i++)cancel.Tick(.02f,new PlayerInput{Tracking=true});
        check(cancel.EnemyAge>MonsterRockMotion.Launch&&cancel.EnemyAge<MonsterRockMotion.Contact,"pause fixture reaches in-flight stone");
        cancel.Pause();
        int old=cancel.HitsTaken;for(int i=0;i<160;i++)cancel.Tick(.02f,new PlayerInput{Tracking=true});
        check(cancel.HitsTaken==old&&!MonsterRockMotion.Active(cancel),"pause discards thrown stone without delayed damage");
        var beam=Start();
        for(int hit=0;hit<15;hit++)
        {beam.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});for(int i=0;i<20;i++)beam.Tick(.02f,new PlayerInput{Tracking=true,Shield=false});}
        Rock(beam,.02f);for(int i=0;i<18;i++)beam.Tick(.02f,new PlayerInput{Tracking=true});
        old=beam.HitsTaken;beam.Tick(.02f,new PlayerInput{Tracking=true,Beam=true});
        check(beam.Action==HeroAction.Beam&&!MonsterRockMotion.Active(beam),"beam interrupts an already launched stone");
        for(int i=0;i<120;i++)beam.Tick(.02f,new PlayerInput{Tracking=true});
        check(beam.HitsTaken==old,"beam-canceled stone cannot hit later");
        var b2=Start();b2.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});
        check(HeroArsenal.Blade("Mebius",b2)&&!HeroArsenal.Blade("Tiga",b2),"only Mebius left melee equips blade");
        var range=Start();range.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true,RangedAttack=true});
        check(!HeroArsenal.Blade("Mebius",range),"forward ranged gesture never equips wrist sword");
        check(HeroArsenal.Sluggers("Zero")&&!HeroArsenal.Sluggers("Geed"),"only Zero equips twin sluggers");
        var shot=new RangedShot();shot.Launch(1,HeroAction.RightPunch,.12f,1);int contacts=0;
        while(shot.Active)if(shot.Tick(.01f))contacts++;
        check(contacts==1,"returning head blades do not award a second hit");
        check(CombatSpacing.Approach(CombatSpacing.StandingDistance)>1&&CombatSpacing.Approach(1)==0,"wider spacing adds approach distance without negative travel");
        check(Math.Abs(MonsterRockMotion.Arc(MonsterRockMotion.Contact))<.00001f,"stone ends exactly at contact height");
    }
}
