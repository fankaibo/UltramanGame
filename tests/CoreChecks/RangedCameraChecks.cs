using System;
using UltramanGame.Core;

static class RangedCameraChecks
{
    static Battle Start(bool guide=true)
    {var b=new Battle(200);b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});if(guide)b.GiveInstructionTime(20);return b;}
    public static void Run(Action<bool,string> check)
    {
        foreach(int hz in new[]{15,30,60})
        {
            float dt=1f/hz;var b=Start();var camera=new RangedCameraMotion();
            void Step(PlayerInput input){b.Tick(dt,input);camera.Tick(b,dt);}
            float held=1;bool sideStable=true;
            for(int f=0;f<hz*4;f++)
            {
                int interval=Math.Max(1,(int)(hz*.8f));bool fire=f%interval==0;
                Step(new PlayerInput{Tracking=true,RangedAttack=true,LeftPunch=fire&&(f/interval)%2==0,RightPunch=fire&&(f/interval)%2==1});
                if(f>=hz*2){held=Math.Min(held,camera.Focus);sideStable&=camera.Side==-1;}
            }
            check(held>.95f&&sideStable,"ranged volley holds one composition across alternating casts "+hz);
            float focus=camera.Focus,travel=camera.Travel;camera.Tick(b,0);
            check(camera.Focus==focus&&camera.Travel==travel,"zero-time ranged camera sample is stable "+hz);
            for(int f=0;f<hz*3;f++)Step(new PlayerInput{Tracking=true});
            check(camera.Focus==0&&b.Punches==5&&b.Energy==5,"volley returns after idle without awarding attacks "+hz);
            Step(new PlayerInput{Tracking=true,RightPunch=true,RangedAttack=true});
            for(int f=0;f<hz/2;f++)Step(new PlayerInput{Tracking=true});
            check(camera.Focus>0&&camera.Focus<.5f&&camera.Side==1,"isolated cast makes a restrained new approach "+hz);
            Step(new PlayerInput{Tracking=true,Shield=true});float previous=camera.Focus;bool monotonic=true;
            for(int f=0;f<hz;f++){Step(new PlayerInput{Tracking=true,Shield=true});monotonic&=camera.Focus<=previous;previous=camera.Focus;}
            check(b.Shield&&camera.Focus<.001f&&monotonic,"shield takes input immediately and eases volley out "+hz);
            Step(new PlayerInput{Tracking=true,LeftPunch=true,RangedAttack=true});
            for(int f=0;f<hz/4;f++)Step(new PlayerInput{Tracking=true});
            camera.Clear();camera.Tick(b,dt);
            check(camera.Focus==0,"presentation reset cannot replay a consumed cast "+hz);
            camera.Tick(new Battle(),dt);check(camera.Focus==0,"new round clears ranged camera "+hz);

            foreach(string mode in new[]{"pause","tracking","showcase","beam","melee","warning","hurt"})
            {
                b=Start(mode!="warning"&&mode!="hurt");camera=new RangedCameraMotion();
                if(mode=="beam")for(int n=0;n<15;n++){b.Tick(dt,new PlayerInput{Tracking=true,LeftPunch=true});for(int f=0;f<hz;f++)b.Tick(dt,new PlayerInput{Tracking=true});b.GiveInstructionTime(20);}
                for(int f=0;f<hz*2;f++)Step(new PlayerInput{Tracking=true,RangedAttack=true,LeftPunch=f==0,RightPunch=f==hz});
                check(camera.Focus>.9f,"exclusive camera scenario starts in volley "+mode+"/"+hz);
                if(mode=="pause")b.Pause();
                bool exercised=mode=="pause"||mode=="showcase",beam=false,hurt=false;int eventFrame=-1;
                for(int f=0;f<hz*12;f++)
                {
                    var input=new PlayerInput{Tracking=mode!="tracking",Beam=mode=="beam"&&f==0,LeftPunch=mode=="melee"&&f==0};
                    if((mode=="warning"||mode=="hurt")&&b.Enemy==EnemyPhase.Rest){input.RangedAttack=true;input.RightPunch=f%Math.Max(1,hz/2)==0;}
                    b.Tick(dt,input);camera.Tick(b,dt,mode=="showcase");
                    beam|=b.Action==HeroAction.Beam;hurt|=b.Action==HeroAction.Hurt;
                    exercised|=b.Phase==GamePhase.Paused||b.IsPunch&&!b.IsRangedPunch||beam||b.Enemy==EnemyPhase.Windup||hurt;
                    if(exercised&&eventFrame<0)eventFrame=f;
                    if((mode=="beam"&&b.Action==HeroAction.Beam||mode=="pause"||mode=="showcase"||mode=="tracking"&&b.Phase==GamePhase.Paused)&&camera.Focus!=0)throw new Exception("Exclusive shot retained volley");
                    if(mode!="hurt"&&eventFrame>=0&&f-eventFrame>=hz||mode=="hurt"&&hurt)break;
                }
                check(exercised&&camera.Focus<.001f&&(mode!="beam"||beam)&&(mode!="hurt"||hurt),"ranged camera yields to "+mode+"/"+hz);
            }
        }
    }
}
