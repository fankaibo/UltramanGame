using System;
using UltramanGame.Core;

static class RangedReactionChecks
{
    static Battle Start()
    {var b=new Battle(200);b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});b.GiveInstructionTime(20);return b;}
    public static void Run(Action<bool,string> check)
    {
        {
            var b=Start();b.Tick(.1f,new PlayerInput{Tracking=true,LeftPunch=true,RangedAttack=true,AttackSpeed=1.7f});
            b.Tick(.001f,new PlayerInput{Tracking=true,GuardIntent=true});
            b.Tick(.1f,new PlayerInput{Tracking=true,RightPunch=true,AttackSpeed=1.7f});
            check(b.Punches==2&&b.EnemyHealth==198&&b.HitSequence==2&&b.LastHitAction==HeroAction.RightPunch&&!b.LastHitRanged&&!b.LastDamageRanged,
                "coalesced remote and melee hits keep damage but classify final contact as a right fist, not a beam");
            b=new Battle(16);b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});
            for(int n=0;n<15;n++){b.GiveInstructionTime(20);b.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});for(int f=0;f<30;f++)b.Tick(.02f,new PlayerInput{Tracking=true});}
            b.Tick(.02f,new PlayerInput{Tracking=true,Beam=true});for(int f=0;f<25;f++)b.Tick(.02f,new PlayerInput{Tracking=true});
            check(b.EnemyHealth==0&&b.Finishing&&b.LastHitAction==HeroAction.Beam&&!b.LastHitRanged&&b.HitSequence==16,
                "beam against final single health point retains special presentation identity");
        }
        foreach(int hz in new[]{15,30,60})foreach(float speed in new[]{.65f,1f,1.7f})foreach(bool left in new[]{false,true})
        {
            float dt=1f/hz;var b=Start();bool saw=false;
            for(int f=0;f<hz*2;f++)
            {
                bool guarding=b.Shot.Active;
                b.Tick(dt,new PlayerInput{Tracking=true,LeftPunch=left&&f==0,RightPunch=!left&&f==0,RangedAttack=true,AttackSpeed=speed,Shield=guarding,GuardIntent=guarding});
                if(b.LastDamageRanged){saw=true;check(b.Shield&&b.LastHitAction==(left?HeroAction.LeftPunch:HeroAction.RightPunch)&&b.HitSequence==1&&b.LastHitRanged,$"released shot retains hand while defending {hz}/{speed}/{left}");}
            }
            check(saw&&b.Punches==1&&b.LastHitRanged&&b.HitSequence==1,"hit receipt survives ordinary ticks without duplicate damage "+hz+"/"+speed+"/"+left);
        }
        foreach(int hz in new[]{15,30,60})
        {
            float dt=1f/hz;var b=Start();var pressure=new MonsterRangedPressure();int steps=0;
            void Tick(PlayerInput input,bool available=true,bool suppressed=false)
            {b.Tick(dt,input);pressure.Tick(b,dt,available,suppressed);if(pressure.Step)steps++;}
            void Fire(bool left=true,bool ranged=true,bool available=true)
            {Tick(new PlayerInput{Tracking=true,LeftPunch=left,RightPunch=!left,RangedAttack=ranged},available);for(int f=1;f<hz;f++)Tick(new PlayerInput{Tracking=true},available);}
            Fire();Fire(false);check(steps==0&&pressure.Hits==2,"first two impacts stay planted "+hz);
            Fire();check(steps==1&&pressure.Left,"third impact steps on actual shooting side "+hz);
            pressure.Tick(b,0,true,false);check(!pressure.Step,"repeat sample does not replay pressure step "+hz);
            Fire(false);Fire();Fire(false);check(steps==2&&!pressure.Left,"next volley steps opposite side "+hz);
            Fire();Fire(false);for(int f=0;f<hz*3;f++)Tick(new PlayerInput{Tracking=true});Fire();check(steps==2&&pressure.Hits==1,"isolated shots do not accumulate an old volley "+hz);
            Fire(false,false);check(!b.LastHitRanged&&b.LastHitAction==HeroAction.RightPunch&&pressure.Hits==0,"melee clears ranged pressure and retains actual hand "+hz);
            Fire();Fire(false);Fire(true,true,false);check(steps==2,"active animation is not restarted by a third impact "+hz);
            pressure.Clear();pressure.Tick(b,dt,true,false);check(!pressure.Step&&pressure.Hits==0,"presentation reset consumes old hit "+hz);
            Fire();Fire(false);b.Pause();pressure.Tick(b,dt,true,false);check(pressure.Hits==0&&!pressure.Step,"pause clears pressure "+hz);
            b=Start();pressure.Tick(b,dt,true,false);check(pressure.Hits==0&&!pressure.Step,"new battle starts without old pressure "+hz);
            Fire();Fire(false);pressure.Tick(b,dt,true,true);Fire();check(steps==2&&pressure.Hits==1,"showcase consumes prior pressure "+hz);
            for(int i=0;i<15;i++)Fire(true,false);
            b.Tick(dt,new PlayerInput{Tracking=true,Beam=true});for(int f=0;f<hz;f++)Tick(new PlayerInput{Tracking=true});
            check(b.LastHitAction==HeroAction.Beam&&!b.LastHitRanged&&pressure.Hits==0,"beam owns its distinct reaction and clears pressure "+hz);
            b=Start();pressure=new MonsterRangedPressure();bool warning=false,enemyAttack=false,unsafeStep=false;
            for(int f=0;f<hz*45;f++)
            {
                b.Tick(dt,new PlayerInput{Tracking=true,RangedAttack=true,LeftPunch=f%Math.Max(1,hz/2)==0});pressure.Tick(b,dt,true,false);
                bool danger=b.Enemy==EnemyPhase.Attack||b.Enemy==EnemyPhase.Windup&&b.WarningDuration-b.EnemyAge<=1.1f;
                warning|=b.Enemy==EnemyPhase.Windup&&danger;enemyAttack|=b.Enemy==EnemyPhase.Attack;unsafeStep|=pressure.Step&&danger;
            }
            check(warning&&enemyAttack&&!unsafeStep,"ranged barrage preserves final warning and enemy attack ownership "+hz);
        }
    }
}
