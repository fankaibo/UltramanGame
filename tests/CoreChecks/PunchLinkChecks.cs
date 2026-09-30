using System;
using UltramanGame.Core;

static class PunchLinkChecks
{
    static Battle Started()
    {
        var state=new Battle(50);state.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
        for(int i=0;i<(Battle.TransformationSeconds+0.2f)/(.02f);i++)state.Tick(.02f,new PlayerInput{Tracking=true});
        state.GiveInstructionTime(20);return state;
    }
    public static void Run(Action<bool,string> check)
    {
        foreach(int rate in new[]{15,30,60})
        {
            var state=Started();var control=Started();var link=new HeroPunchLink();float dt=1f/rate;
            int queuedFor=0,linked=0;bool stable=true,unchanged=true,contactClean=true;
            HeroAction prior=HeroAction.None;
            for(int frame=0;frame<rate*4;frame++)
            {
                var input=new PlayerInput{Tracking=true,LeftPunch=frame==0};
                bool punch=state.Action==HeroAction.LeftPunch||state.Action==HeroAction.RightPunch;
                if(punch&&state.ActionAge>=.24f&&state.Punches<6&&queuedFor!=state.Punches)
                {queuedFor=state.Punches;input.LeftPunch=state.Action==HeroAction.RightPunch;input.RightPunch=!input.LeftPunch;}
                float oldHealth=state.EnemyHealth;
                state.Tick(dt,input);control.Tick(dt,input);link.Tick(state,dt);
                if(state.Action!=prior&&(state.Action==HeroAction.LeftPunch||state.Action==HeroAction.RightPunch)&&link.Weight>.05f)linked++;
                if(state.EnemyHealth<oldHealth)contactClean&=link.Weight==0;
                float weight=link.Weight;HeroAction side=link.Side;link.Tick(state,0);
                stable&=weight==link.Weight&&side==link.Side&&weight>=0&&weight<=1;
                unchanged&=state.Phase==control.Phase&&state.Action==control.Action&&state.ActionAge==control.ActionAge&&
                    state.EnemyHealth==control.EnemyHealth&&state.Energy==control.Energy&&state.BufferedPunch==control.BufferedPunch;
                prior=state.Action;
            }
            check(linked==5&&state.Punches==6&&state.EnemyHealth==44,$"{rate} Hz accepted alternate punches preload through all five handoffs");
            check(contactClean&&unchanged,$"{rate} Hz anticipation cannot change damage, energy, timing or the contact pose");
            check(stable&&link.Weight==0,$"{rate} Hz repeated zero-time sampling is stable and completed string clears");
        }
        foreach(string interruption in new[]{"guard","pause","new-round","preview"})
        {
            var state=Started();var link=new HeroPunchLink();const float dt=1/60f;
            for(int frame=0;frame<24;frame++)
            {
                state.Tick(dt,new PlayerInput{Tracking=true,LeftPunch=frame==0,RightPunch=frame==16});link.Tick(state,dt);
                if(link.Weight>.5f)break;
            }
            check(link.Weight>.5f&&state.BufferedPunch==HeroAction.RightPunch,"interruption starts with accepted next fist: "+interruption);
            if(interruption=="guard")state.Tick(dt,new PlayerInput{Tracking=true,Shield=true});
            else if(interruption=="pause")state.Pause();
            else if(interruption=="new-round")state=new Battle();
            link.Tick(state,dt,interruption!="preview");
            check(link.Weight==0&&link.Side==HeroAction.None,"guard, pause, new round or preview cancels anticipation: "+interruption);
        }
        {
            var state=Started();var link=new HeroPunchLink();bool prepared=false;
            for(int frame=0;frame<60;frame++)
            {state.Tick(1/60f,new PlayerInput{Tracking=true,LeftPunch=frame==0});link.Tick(state,1/60f);prepared|=link.Weight>0;}
            check(!prepared&&state.Punches==1,"a single punch never predicts or automatically performs another fist");
        }
    }
}
