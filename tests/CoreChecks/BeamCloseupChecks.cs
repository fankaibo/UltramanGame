using System;
using UltramanGame.Core;

static class BeamCloseupChecks
{
    static Battle Ready(int health=50)
    {
        var b=new Battle(health);b.Tick(.02f,new PlayerInput {Tracking=true,Transform=true});
        for(int i=0;i<120;i++)b.Tick(.02f,new PlayerInput {Tracking=true});
        for(int attempt=0;attempt<80&&b.Energy<Battle.MaxEnergy;attempt++)
        {
            b.Tick(.02f,new PlayerInput {Tracking=true,LeftPunch=true});
            for(int i=0;i<25;i++)b.Tick(.02f,new PlayerInput {Tracking=true,Shield=true});
        }
        if(b.Energy!=Battle.MaxEnergy)throw new Exception("Closeup test could not fill energy");
        while(b.TryCue(out _)){}return b;
    }
    static void Frame(Battle b,BeamCloseup shot,float dt,PlayerInput input)
    {
        b.Tick(shot.Active?0:dt,input);
        while(b.TryCue(out var cue))if(cue==GameCue.Beam)shot.Begin();
        shot.Tick(dt,b);
    }
    public static void Run(Action<bool,string> check)
    {
        foreach(int fps in new[]{15,30,60})
        {
            var b=Ready();var shot=new BeamCloseup();float health=b.EnemyHealth,dt=1f/fps;
            Frame(b,shot,dt,new PlayerInput {Tracking=true,Beam=true});
            float actionAge=b.ActionAge;int hurts=b.HitsTaken;
            bool allHeld=true;
            for(int i=0;i<fps*2&&shot.Active;i++)
            {
                Frame(b,shot,dt,new PlayerInput {Tracking=true,LeftPunch=true,Beam=true});
                allHeld&=b.EnemyHealth==health&&b.ActionAge==actionAge&&b.EnemyAge==0&&b.HitsTaken==hurts;
            }
            check(allHeld&&!shot.Active&&b.Energy==0,$"{fps} FPS closeup holds damage, enemy and action clocks without recharging");
            while(b.ActionAge<.25f)Frame(b,shot,dt,new PlayerInput {Tracking=true});
            check(b.EnemyHealth==health,$"{fps} FPS cut back precedes beam hit");
            for(int i=0;i<fps*2;i++)Frame(b,shot,dt,new PlayerInput {Tracking=true});
            check(b.EnemyHealth==health-9&&!shot.Active,$"{fps} FPS beam damages exactly once after closeup");
        }
        {
            var b=Ready();var shot=new BeamCloseup();float health=b.EnemyHealth;
            Frame(b,shot,.02f,new PlayerInput {Tracking=true,Beam=true});
            for(int i=0;i<15;i++)Frame(b,shot,.02f,new PlayerInput {Tracking=true});
            check(shot.Focus>.99f,"Beam pose has a sustained closeup hold");
            Frame(b,shot,.02f,default);
            check(!shot.Active&&shot.Focus==0&&b.Phase==GamePhase.Paused,"Tracking loss or pause immediately clears the closeup");
            for(int i=0;i<110;i++)Frame(b,shot,.02f,new PlayerInput {Tracking=true});
            check(b.Phase==GamePhase.Battle&&b.EnemyHealth==health&&b.Action==HeroAction.None,"Resume does not replay a cancelled beam or stay frozen");
        }
        {
            var b=Ready();var shot=new BeamCloseup();Frame(b,shot,.02f,new PlayerInput {Tracking=true,Beam=true});
            shot.Tick(.02f,new Battle());
            check(!shot.Active&&shot.Focus==0,"Restart clears the cinematic framing");
            Frame(b,shot,.02f,new PlayerInput {Tracking=true});shot.Begin();shot.Tick(.02f,b,true);
            check(!shot.Active,"Character showcase or modal suppression clears the closeup");
        }
        {
            var b=Ready(20);var shot=new BeamCloseup();Frame(b,shot,.02f,new PlayerInput {Tracking=true,Beam=true});
            bool victoryDuringShot=false;
            for(int i=0;i<120;i++)
            {
                Frame(b,shot,.02f,new PlayerInput {Tracking=true});
                victoryDuringShot|=shot.Active&&b.Phase==GamePhase.Victory;
            }
            check(!victoryDuringShot&&b.Phase==GamePhase.Victory&&b.EnemyHealth==0&&!shot.Active,"Finishing beam completes the closeup before victory");
        }
    }
}
