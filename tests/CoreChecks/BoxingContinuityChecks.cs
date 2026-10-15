using System;
using UltramanGame.Core;

static class BoxingContinuityChecks
{
    static Battle Start()
    {
        var b=new Battle(50);b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
        for(int i=0;i<300;i++)b.Tick(.02f,new PlayerInput{Tracking=true});
        b.GiveInstructionTime(30);return b;
    }
    public static void Run(Action<bool,string> check)
    {
        foreach(bool ranged in new[]{false,true})
        {
            float end=ranged?AttackTempo.RangedSeconds:Battle.PunchSeconds;
            check(BoxingBodyMotion.Hip(0,ranged)==0&&BoxingBodyMotion.Chest(0,ranged)==0&&
                BoxingBodyMotion.Hip(end,ranged)==0&&BoxingBodyMotion.Chest(end,ranged)==0,
                "boxing torso returns to live pose at both boundaries: "+ranged);
            float maxHip=0,maxChest=0;
            for(float age=0;age<end;age+=.001f)
            {maxHip=Math.Max(maxHip,Math.Abs(BoxingBodyMotion.Hip(age,ranged)));maxChest=Math.Max(maxChest,Math.Abs(BoxingBodyMotion.Chest(age,ranged)));}
            check(maxHip>15&&maxHip<25&&maxChest>4&&maxChest<9,"captured hip and chest remain bounded: "+ranged);
            check(BoxingBodyMotion.Hip(.10f,ranged)>BoxingBodyMotion.Chest(.10f,ranged)*2,
                "hip leads chest before the .12 second contact: "+ranged);
        }
        foreach(int rate in new[]{15,30,60})
        {
            var b=Start();var control=Start();var camera=new MeleeCameraMotion();float dt=1f/rate;
            int queued=0,side=0;bool continuous=true,unmodified=true,zeroStable=true;float prior=0;
            for(int frame=0;frame<rate*4;frame++)
            {
                var input=new PlayerInput{Tracking=true,LeftPunch=frame==0};
                if(b.IsPunch&&b.ActionAge>=.24f&&b.Punches<5&&queued!=b.Punches)
                {queued=b.Punches;input.LeftPunch=b.Action==HeroAction.RightPunch;input.RightPunch=!input.LeftPunch;}
                b.Tick(dt,input);control.Tick(dt,input);camera.Tick(b,dt);
                if(camera.Focus>.01f&&side==0)side=camera.Side;
                if(frame<rate*1.7f&&camera.Focus>.01f)
                    continuous&=camera.Side==side&&camera.Focus>=prior-.001f;
                prior=camera.Focus;camera.Tick(b,0);zeroStable&=camera.Focus==prior;
                unmodified&=b.EnemyHealth==control.EnemyHealth&&b.ActionAge==control.ActionAge&&b.Energy==control.Energy;
            }
            check(continuous&&side==-1&&b.Punches==5,$"{rate} Hz alternate fists share one orbit without resetting zoom");
            check(zeroStable&&unmodified&&camera.Focus<.001f,$"{rate} Hz camera is presentation-only and returns after the string");
        }
        foreach(string interruption in new[]{"guard","pause","new-round","showcase"})
        {
            var b=Start();var camera=new MeleeCameraMotion();
            for(int i=0;i<15;i++){b.Tick(1/60f,new PlayerInput{Tracking=true,LeftPunch=i==0});camera.Tick(b,1/60f);}
            check(camera.Focus>.1f,"camera starts before interruption: "+interruption);
            if(interruption=="pause")b.Pause();
            if(interruption=="new-round")b=new Battle();
            for(int i=0;i<60;i++)
            {b.Tick(1/60f,new PlayerInput{Tracking=true,Shield=interruption=="guard"});camera.Tick(b,1/60f,interruption=="showcase");}
            check(camera.Focus<.001f,"camera gives up attack framing: "+interruption);
        }
    }
}
