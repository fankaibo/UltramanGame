using System;
using UltramanGame.Core;

static class GestureBattleChecks
{
    static PosePoint P(float x,float y)=>new PosePoint(x,y){z=-.1f};
    static PosePoint[] Pose(string kind)
    {
        var p=new PosePoint[33];for(int i=0;i<p.Length;i++)p[i]=P(.5f,.5f);
        p[11]=P(.65f,.35f);p[12]=P(.35f,.35f);
        p[13]=P(.68f,.50f);p[14]=P(.32f,.50f);
        p[15]=P(.58f,kind=="neutral"?.75f:.44f);
        p[16]=P(.42f,kind=="neutral"?.75f:.44f);
        if(kind=="beam"){p[15]=P(.60f,.30f);p[16]=P(.44f,.46f);}
        return p;
    }
    static Battle Started(bool charged)
    {
        var b=new Battle();b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
        for(int i=0;i<120;i++)b.Tick(.02f,new PlayerInput{Tracking=true});
        b.GiveInstructionTime(20);
        if(charged)for(int n=0;n<Battle.MaxEnergy;n++)
        {
            b.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});
            for(int i=0;i<25;i++)b.Tick(.02f,new PlayerInput{Tracking=true});
        }
        return b;
    }
    public static void Run(Action<bool,string> check)
    {
        foreach(int rate in new[]{15,30,60})foreach(string kind in new[]{"guard","beam"})
        {
            float dt=1f/rate;long stamp=700000,sequence=0;
            var recognizer=new GestureRecognizer{Difficulty=1};
            PlayerInput Read(string pose)
            {
                stamp+=(long)Math.Round(dt*1000);
                return recognizer.Update(new PoseFrame{schema=1,streamId="battle-intent",sequence=++sequence,
                    capturedMs=stamp,tracked=true,points=Pose(pose)},stamp,kind=="beam",false);
            }
            for(int i=0;i<rate;i++)Read("neutral");
            var battle=Started(kind=="beam");int priorPunches=battle.Punches;
            battle.Tick(dt,new PlayerInput{Tracking=true,LeftPunch=true});
            while(battle.ActionAge<.21f)battle.Tick(dt,new PlayerInput{Tracking=true});
            battle.Tick(dt,new PlayerInput{Tracking=true,RightPunch=true});
            check(battle.BufferedPunch==HeroAction.RightPunch,$"{rate} Hz {kind} test begins with a real buffered follow-up");
            var initial=Read(kind);battle.Tick(dt,initial);
            check(!initial.Shield&&!initial.Beam,$"{rate} Hz entering {kind} still requires its hold confirmation");
            check(battle.BufferedPunch==HeroAction.None,$"{rate} Hz {kind} acquisition cancels old buffered punch before confirmation");
            for(int i=0;i<rate*1.4f;i++)battle.Tick(dt,Read(kind));
            check(battle.Punches==priorPunches+1,$"{rate} Hz holding {kind} cannot execute the old follow-up punch");
            check(kind=="guard"?battle.Shield:battle.Energy==0&&battle.EnemyHealth==battle.MaxHealth-priorPunches-10,
                $"{rate} Hz confirmed {kind} still guards or fires with correct damage");
            for(int i=0;i<rate*2;i++)battle.Tick(dt,Read("neutral"));
            check(!battle.Shield&&battle.Action==HeroAction.None,$"{rate} Hz releasing {kind} leaves no queued action");
            battle.Tick(dt,new PlayerInput{Tracking=true,RightPunch=true});
            for(int i=0;i<rate;i++)battle.Tick(dt,new PlayerInput{Tracking=true});
            check(battle.Punches==priorPunches+2,$"{rate} Hz deliberate punch works after {kind} is released");
        }
    }
}
