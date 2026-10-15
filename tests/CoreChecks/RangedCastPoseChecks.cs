using System;
using UltramanGame.Core;

static class RangedCastPoseChecks
{
    static Battle Start(bool left,float speed=1)
    {
        var b=new Battle();b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
        while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});
        b.GiveInstructionTime(60);
        b.Tick(.01f,new PlayerInput{Tracking=true,RangedAttack=true,LeftPunch=left,RightPunch=!left,AttackSpeed=speed});return b;
    }
    static PoseFrame Frame(bool left,float y,float scale=1)
    {
        var p=new PosePoint[33];for(int n=0;n<33;n++)p[n]=new PosePoint(.5f,.5f);
        p[11]=new PosePoint(.65f,.3f);p[12]=new PosePoint(.35f,.3f);
        p[15]=new PosePoint(.65f,left?y:.65f);p[16]=new PosePoint(.35f,left?.65f:y);
        for(int n=0;n<33;n++){p[n].x=.5f+(p[n].x-.5f)*scale;p[n].y=.5f+(p[n].y-.5f)*scale;}
        return new PoseFrame{schema=1,source="synthetic",streamId="height-check",sequence=1,capturedMs=100000,tracked=true,points=p};
    }
    public static void Run(Action<bool,string> check)
    {
        foreach(int hz in new[]{15,30,60})foreach(bool left in new[]{false,true})foreach(float speed in new[]{.65f,1.7f})
        {
            string label=$"{hz}/{left}/{speed}";float dt=1f/hz;
            var b=Start(left,speed);var pose=new RangedCastPose();var input=new PlayerInput{Tracking=true};
            pose.Tick(Frame(left,.15f),100000,input,b,dt,true);
            check(Math.Abs(pose.Elevation-.45f)<.00001f,"accepted high cast follows its own anatomical wrist "+label);
            var scaled=new RangedCastPose();scaled.Tick(Frame(left,.15f,.55f),100000,input,b,dt,true);
            check(Math.Abs(pose.Elevation-scaled.Elevation)<.00001f,"cast height ignores camera distance "+label);
            var noisy=Frame(left,.65f);noisy.points[11].z=10;noisy.points[12].z=-10;noisy.sequence=2;
            pose.Tick(noisy,100000,input,b,dt,true);
            check(Math.Abs(pose.Elevation-.45f)<.00001f,"accepted cast does not chase recovering wrist or depth noise "+label);
            var low=new RangedCastPose();low.Tick(noisy,100000,input,b,dt,true);
            check(Math.Abs(low.Elevation+.10f)<.00001f,"low cast remains bounded with noisy shoulder depth "+label);
            check(b.Punches==0&&b.EnemyHealth==50&&b.Energy==0&&b.AttackSpeed==speed,"height sampling cannot change combat receipts or speed "+label);
            foreach(string mode in new[]{"disabled","stale","hidden","invalid","no-tracking"})
            {
                var fallback=new RangedCastPose();var frame=Frame(left,.15f);
                if(mode=="hidden")frame.points[left?15:16].visibility=.1f;
                if(mode=="invalid")frame.points[11].y=float.NaN;
                fallback.Tick(frame,mode=="stale"?101000:100000,new PlayerInput{Tracking=mode!="no-tracking"},b,dt,mode!="disabled");
                check(fallback.Elevation==0,"unreliable/keyboard cast retains authored direction "+mode+" "+label);
                fallback.Tick(Frame(left,.15f),100000,input,b,dt,true);
                check(fallback.Elevation==0,"missing first sample cannot change direction midcast "+mode+" "+label);
            }
            foreach(float invalidDt in new[]{0f,-1,float.NaN,float.PositiveInfinity})
            {pose.Tick(null,0,default,null,invalidDt,false);check(pose.Elevation>.44f,"nonadvancing height observation freezes "+label);}
            b.Pause();pose.Tick(null,100000,default,b,dt,false);check(pose.Elevation==0,"pause releases captured cast height "+label);
            pose.Tick(Frame(!left,.15f),100000,input,Start(!left),dt,true);check(pose.Elevation>.44f,"new battle samples its own cast "+label);
            pose.Tick(null,0,default,new Battle(),dt,false);check(pose.Elevation==0,"waiting/new-round cannot retain cast height "+label);
        }
    }
}
