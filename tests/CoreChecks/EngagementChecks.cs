using System;
using UltramanGame.Core;

static class EngagementChecks
{
    static Battle Start(int hp=50)
    {var b=new Battle(hp);b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});b.GiveInstructionTime(20);return b;}
    static void Advance(Battle b,HeroEngagementMotion m,float seconds,float dt=.02f,bool shield=false)
    {for(int i=0;i<(int)Math.Ceiling(seconds/dt);i++){b.Tick(dt,new PlayerInput{Tracking=true,Shield=shield});m.Tick(b,dt);}}
    public static void Run(Action<bool,string> check)
    {
        foreach(int hz in new[]{15,30,60})foreach(float speed in new[]{.65f,1,1.7f})
        {
            float dt=1f/hz;var b=Start();var m=new HeroEngagementMotion();
            b.Tick(dt,new PlayerInput{Tracking=true,LeftPunch=true,AttackSpeed=speed});m.Tick(b,dt);
            Advance(b,m,.18f/speed,dt);check(m.Weight>.99f,"approach reaches contact "+hz+"/"+speed);
            while(b.IsPunch){b.Tick(dt,new PlayerInput{Tracking=true});m.Tick(b,dt);}
            Advance(b,m,.1f,dt);check(m.Weight>.99f,"short gap retains melee stance "+hz+"/"+speed);
            b.Tick(dt,new PlayerInput{Tracking=true,RightPunch=true,AttackSpeed=speed});m.Tick(b,dt);
            check(m.Weight>.99f,"opposite fist continues without resetting approach "+hz+"/"+speed);
            Advance(b,m,2,dt);check(m.Weight==0&&m.LeftLift==0&&m.RightLift==0,"idle settles both feet at home "+hz+"/"+speed);
            check(b.Punches==2&&b.Energy==2&&b.EnemyHealth==48,"engagement cannot add hits or energy "+hz+"/"+speed);
        }
        var guard=Start();var motion=new HeroEngagementMotion();guard.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});Advance(guard,motion,.2f);
        float previous=motion.Weight;bool staggered=false;
        for(int i=0;i<40;i++)
        {
            guard.Tick(.02f,new PlayerInput{Tracking=true,Shield=true});motion.Tick(guard,.02f);
            check(motion.Weight<=previous+.00001f&&previous-motion.Weight<.06f,"shield retreat remains continuous "+i);previous=motion.Weight;
            if(Math.Abs(motion.LeftFoot-motion.RightFoot)>.2f)staggered=true;
        }
        check(guard.Shield&&motion.Weight==0&&staggered,"shield owns input immediately while retreat uses separate steps");
        var ranged=Start();var rm=new HeroEngagementMotion();ranged.Tick(.02f,new PlayerInput{Tracking=true,RightPunch=true,RangedAttack=true});Advance(ranged,rm,1);
        check(rm.Weight==0&&ranged.Punches==1,"ranged-only input stays at distance");
        var beam=Start(200);var bm=new HeroEngagementMotion();
        for(int i=0;i<15;i++){beam.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});Advance(beam,bm,.5f);}
        beam.Tick(.02f,new PlayerInput{Tracking=true,Beam=true});bm.Tick(beam,.02f);float held=bm.Weight;Advance(beam,bm,.5f);
        check(held>.99f&&bm.Weight==held&&beam.Action==HeroAction.Beam,"beam holds actual close stance without snapping home");
        beam.Pause();bm.Tick(beam,.02f);check(bm.Weight==held,"pause freezes actual stance instead of teleporting home");
        Advance(beam,bm,2);check(bm.Weight==0,"resume returns an interrupted approach to home");
        bm.Tick(new Battle(),.02f);check(bm.Weight==0,"new round clears approach");
        var win=Start(10);var wm=new HeroEngagementMotion();for(int i=0;i<10;i++){win.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});Advance(win,wm,.5f);}
        check(win.Phase==GamePhase.Victory&&wm.Weight==0,"final strike returns before victory staging");
    }
}
