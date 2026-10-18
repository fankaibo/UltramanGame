using System;
using UltramanGame.Core;

static class BattleCaptionChecks
{
    public static void Run(Action<bool,string> check)
    {
        foreach(int fps in new[]{15,30,60})
        {
            float dt=1f/fps;
            var b=new Battle();
            void Tick(bool guard=false)=>b.Tick(dt,new PlayerInput{Tracking=true,Shield=guard});
            void Until(Func<bool> done,bool guard=false)
            {for(int n=0;n<fps*60&&!done();n++)Tick(guard);check(done(),"caption scenario reaches requested state "+fps);}
            check(!BattleCaption.Visible(BattleCaptionKind.Shield,b),"new round cannot display previous shield receipt "+fps);
            b.Tick(dt,new PlayerInput{Tracking=true,Transform=true});Until(()=>b.Phase==GamePhase.Battle);
            Until(()=>b.Blocks>0,true);Until(()=>b.Enemy==EnemyPhase.Recover,true);
            check(BattleCaption.Visible(BattleCaptionKind.Shield,b)&&BattleCaption.Gesture(BattleCaptionKind.Shield)=="shield",
                "successful block retains a shield illustration after warning "+fps);
            check(!BattleCaption.Visible(BattleCaptionKind.Punch,b),"held shield cannot request another punch "+fps);
            Tick();b.Tick(dt,new PlayerInput{Tracking=true,RightPunch=true});
            check(b.IsPunch&&!BattleCaption.Visible(BattleCaptionKind.Shield,b),"counterpunch immediately replaces old block receipt "+fps);
            Until(()=>b.HitsTaken>0);
            check(BattleCaption.Visible(BattleCaptionKind.Recovery,b),"recovery text belongs to actual hurt phase "+fps);
            Until(()=>b.Action==HeroAction.None);
            check(!BattleCaption.Visible(BattleCaptionKind.Recovery,b),"standing hero cannot retain recovery instruction "+fps);
            b.GiveInstructionTime(100);
            for(int k=0;k<15;k++)
            {b.Tick(dt,new PlayerInput{Tracking=true,LeftPunch=true});for(int n=0;n<(int)Math.Ceiling(.5f/dt);n++)Tick();}
            check(b.Energy==Battle.MaxEnergy&&BattleCaption.Visible(BattleCaptionKind.Beam,b),"earned full energy permits beam guidance "+fps);
            b.Tick(dt,new PlayerInput{Tracking=true,Beam=true});
            check(b.Action==HeroAction.Beam&&BattleCaption.Visible(BattleCaptionKind.Beam,b),"released beam keeps beam illustration "+fps);
            Until(()=>b.Action==HeroAction.None);
            check(!BattleCaption.Visible(BattleCaptionKind.Beam,b),"spent beam cannot reappear beside punch illustration "+fps);
            b.Tick(dt,new PlayerInput{Tracking=false});
            check(!BattleCaption.Visible(BattleCaptionKind.Resume,b),"tracking recovery owns paused screen "+fps);
        }
    }
}
