using System;
using UltramanGame.Core;

static class CameraGuidanceChecks
{
    static PosePoint[] Pose(bool beam)
    {
        var p=new PosePoint[33];for(int i=0;i<p.Length;i++)p[i]=new PosePoint(.5f,.5f){z=-.1f};
        p[11]=new PosePoint(.65f,.35f){z=-.1f};p[12]=new PosePoint(.35f,.35f){z=-.1f};
        p[15]=new PosePoint(beam?.56f:.65f,beam?.27f:.44f){z=-.2f};
        p[16]=new PosePoint(beam?.44f:.35f,beam?.47f:.44f){z=-.2f};return p;
    }
    public static void Run(Action<bool,string> check)
    {
        foreach(int fps in new[]{15,30,60})
        {
            float dt=1f/fps;var b=new Battle();var r=new GestureRecognizer{Difficulty=1};
            string Status(PlayerInput i=default,bool present=true,string old="护盾已展开")=>CameraGuidance.Status(b,r,i,present,old);
            r.Reset(requireTransformRelease:true);
            check(Status()=="先放下双手","replay camera cue matches actual release gate "+fps);
            check(Status(present:false)=="请站进镜头","lost tracking overrides old shield and replay cue "+fps);
            r.Reset();check(Status()=="看见你了 · 举高双手","waiting cannot repeat old combat receipt "+fps);
            b.Tick(dt,new PlayerInput{Tracking=true,Transform=true});
            check(Status()=="变身成功","transformation cannot show old shield "+fps);
            for(int n=0;n<1000&&b.Phase!=GamePhase.Battle;n++)b.Tick(dt,new PlayerInput{Tracking=true});
            b.GiveInstructionTime(100);
            for(int k=0;k<15;k++)
            {b.Tick(dt,new PlayerInput{Tracking=true,LeftPunch=true});for(int n=0;n<Math.Ceiling(.5f/dt);n++)b.Tick(dt,new PlayerInput{Tracking=true});}
            check(b.Energy==Battle.MaxEnergy,"camera guide test earned full energy "+fps);
            long stamp=100000,seq=0;PlayerInput held=default;
            bool pendingSeen=false,pendingAccurate=true;
            void Hold(bool beam,float seconds,bool enabled=true)
            {
                for(int n=0;n<Math.Ceiling(seconds/dt);n++)
                {stamp+=(long)Math.Round(dt*1000);held=r.Update(new PoseFrame{schema=1,tracked=true,streamId="camera-guide",sequence=++seq,capturedMs=stamp,points=Pose(beam)},stamp,enabled,false);b.Tick(dt,held);
                    if(held.GuardIntent&&!b.Shield)
                    {pendingSeen=true;pendingAccurate&=CameraGuidance.GuardInstruction(b,held,false)=="看见防御姿势 · 保持一下";}
                }
            }
            // Measure an observed neutral-to-guard transition at standard difficulty.
            for(int n=0;n<fps;n++)
            {
                var rest=Pose(false);rest[15].y=rest[16].y=.75f;
                stamp+=(long)Math.Round(dt*1000);
                held=r.Update(new PoseFrame{schema=1,tracked=true,streamId="camera-guide",sequence=++seq,capturedMs=stamp,points=rest},stamp,true,false);
                b.Tick(dt,held);
            }
            Hold(false,.7f);check(b.Shield&&Status(held)=="护盾已展开","active shield still confirmed "+fps+" phase="+b.Phase+" action="+b.Action+" shield="+held.Shield+" guide="+Status(held));
            check(pendingSeen&&pendingAccurate,"guard acquisition never claims active protection before confirmation "+fps);
            check(CameraGuidance.GuardInstruction(b,held,false)=="护盾已展开 · 保持住！","confirmed guard replaces acquisition text "+fps);
            check(CameraGuidance.GuardInstruction(b,held,true)=="护盾已展开 · 继续按住 S","keyboard active guard retains its own key instruction "+fps);
            Hold(true,.3f);
            check(r.BeamProgress>0&&Status(held)=="光线姿势 · 保持","guard to beam immediately replaces stale shield receipt "+fps);
            check(Status(held,false)=="请站进镜头","lost tracking overrides beam progress "+fps);
            Hold(true,.6f);check(b.Action==HeroAction.Beam&&Status(held)=="光线释放中","release replaces hold cue "+fps);
            for(int n=0;n<fps*2;n++)b.Tick(dt,new PlayerInput{Tracking=true});
            check(Status()=="看见你了","expired shield is not claimed after action "+fps);
            check(CameraGuidance.GuardInstruction(b,default,false)=="双手交叉护在胸前或脸前","released guard returns to child pose instruction "+fps);
            check(CameraGuidance.GuardInstruction(b,new PlayerInput{GuardIntent=true},true)=="按住 S，展开护盾！","camera candidate cannot claim keyboard protection "+fps);
            check(Status(old:"已识别：向前挥拳")=="已识别：向前挥拳","ordinary punch receipt remains available "+fps);
            b.Tick(dt,new PlayerInput{Tracking=false});
            check(Status()=="看见你了 · 准备继续","paused phase ignores old combat receipt "+fps);
            // Arm the release gate with a pre-held finisher before energy becomes available.
            b=new Battle();b.Tick(dt,new PlayerInput{Tracking=true,Transform=true});
            for(int n=0;n<1000&&b.Phase!=GamePhase.Battle;n++)b.Tick(dt,new PlayerInput{Tracking=true});
            b.GiveInstructionTime(100);r.Reset();Hold(true,1,false);
            for(int k=0;k<15;k++)
            {b.Tick(dt,new PlayerInput{Tracking=true,LeftPunch=true});for(int n=0;n<Math.Ceiling(.5f/dt);n++)b.Tick(dt,new PlayerInput{Tracking=true});}
            Hold(true,.3f);
            check(r.BeamNeedsRelease&&Status(held)=="先收手，再摆光线姿势","pre-held finisher explains release instead of stale shield "+fps);
        }
    }
}
