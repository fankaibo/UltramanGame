using System;
using UltramanGame.Core;

static class LiveReadyPoseChecks
{
    static PoseFrame Frame(long stamp,float leftY=.62f,float rightY=.62f,float scale=1)
    {
        var p=new PosePoint[33];for(int i=0;i<p.Length;i++)p[i]=new PosePoint(.5f,.5f);
        p[0]=new PosePoint(.5f,.15f);p[23]=new PosePoint(.6f,.68f);p[24]=new PosePoint(.4f,.68f);
        p[11]=new PosePoint(.65f,.30f);p[12]=new PosePoint(.35f,.30f);
        p[13]=new PosePoint(.69f,.48f);p[14]=new PosePoint(.31f,.48f);
        p[15]=new PosePoint(.67f,leftY);p[16]=new PosePoint(.33f,rightY);
        for(int i=0;i<p.Length;i++){p[i].x=.5f+(p[i].x-.5f)*scale;p[i].y=.5f+(p[i].y-.5f)*scale;}
        return new PoseFrame{schema=1,streamId="ready",sequence=stamp,capturedMs=stamp,tracked=true,points=p};
    }
    static Battle Start(){var b=new Battle();b.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});while(b.Phase!=GamePhase.Battle)b.Tick(.02f,new PlayerInput{Tracking=true});b.GiveInstructionTime(60);return b;}
    public static void Run(Action<bool,string> check)
    {
        foreach(int hz in new[]{15,30,60})
        {
            BodyChecks(check,hz);
            var b=Start();var a=new LiveReadyPose();var scaled=new LiveReadyPose();float dt=1f/hz;long stamp=100000;var input=new PlayerInput{Tracking=true};
            for(int f=0;f<hz;f++){stamp+=1000/hz;a.Tick(Frame(stamp,.12f),stamp,input,b,dt,true);scaled.Tick(Frame(stamp,.12f,scale:.55f),stamp,input,b,dt,true);}
            check(a.Y(true)>.44f&&a.Y(false)<0&&a.X(true)<0&&a.X(false)>0,"live ready respects separate anatomical left and right "+hz);
            check(Math.Abs(a.Y(true)-scaled.Y(true))<.0001f&&Math.Abs(a.X(false)-scaled.X(false))<.0001f,"live ready is camera-scale independent "+hz);
            float y=a.Y(true);a.Tick(Frame(stamp,.62f),stamp,input,b,0,true);check(a.Y(true)==y,"zero-time live ready cannot accumulate "+hz);
            var held=Frame(stamp,.12f);for(int f=0;f<hz;f++)a.Tick(held,stamp+400+f*1000/hz,input,b,dt,true);
            check(Math.Abs(a.Y(true))<.001f,"stale pose fades live hands instead of replaying "+hz);
            for(int f=0;f<hz;f++){stamp+=1000/hz;var p=Frame(stamp,.12f,.12f);p.points[15].visibility=.1f;a.Tick(p,stamp,input,b,dt,true);}
            check(Math.Abs(a.Y(true))<.001f&&a.Y(false)>.44f,"one occluded wrist preserves the other hand "+hz);
            for(int f=0;f<hz;f++){stamp+=1000/hz;a.Tick(Frame(stamp,.12f),stamp,new PlayerInput{Tracking=true,BeamIntent=true},b,dt,true);}
            check(a.Y(true)>.44f&&!b.Shield&&b.Action==HeroAction.None,"confirmation hold keeps visual feedback without awarding a skill "+hz);
            for(int f=0;f<hz;f++){stamp+=1000/hz;var p=Frame(stamp,.12f);p.streamId="replacement";p.sequence=f+1;a.Tick(p,stamp,input,b,dt,true);}
            check(a.Y(true)>.44f,"new pose worker can animate with a restarted sequence "+hz);
            for(int f=0;f<hz;f++){stamp+=1000/hz;a.Tick(Frame(stamp,.12f),stamp,input,b,dt,false);}
            check(Math.Abs(a.Y(true))<.001f,"keyboard and disabled presentation return to authored ready "+hz);
            var invalid=Frame(stamp);invalid.points[2].x=float.NaN;
            a.Tick(invalid,stamp,input,b,dt,true);check(!float.IsNaN(a.X(true)),"invalid pose data cannot poison arm solve "+hz);
            b.Tick(dt,new PlayerInput{Tracking=true,RightPunch=true});check(!LiveReadyPose.Ready(b),"attack clip takes ownership before live pose "+hz);
            b.Pause();check(!LiveReadyPose.Ready(b),"tracking pause keeps the corrected neutral ready "+hz);
            check(b.EnemyHealth==50&&b.Energy==0&&b.Punches==0,"presentation itself never scores a hit "+hz);
            var gestures=new GestureRecognizer();int accidental=0;
            for(int f=0;f<hz*4;f++)
            {
                // Both wrists clearly below the existing forgiving chest-guard envelope.
                float t=f*dt-1,ly=.76f,ry=.76f;
                bool left=t>=0&&t<.9f,right=t>=1.4f&&t<2.4f;
                if(left||right)
                {float u=Math.Max(0,Math.Min(1,(left?t:t-1.4f)/.35f));u=u*u*(3-2*u);if(left)ly+=(-.64f*u);else ry+=(-.64f*u);}
                stamp+=1000/hz;var frame=Frame(stamp,ly,ry);
                var result=gestures.Update(frame,stamp,false,false);
                if(result.LeftPunch||result.RightPunch||result.Beam||result.Shield)accidental++;
            }
            check(accidental==0,"live ready demonstration preserves real gesture ownership "+hz);
        }
    }
    static PoseFrame Lean(long stamp,float lean=1,float scale=1,float translation=0,bool hips=true)
    {
        var p=Frame(stamp,.76f,.76f);
        foreach(int i in new[]{0,11,12,13,14,15,16})p.points[i].x+=lean*.13f;
        p.points[11].y+=lean*.035f;p.points[12].y-=lean*.035f;
        for(int i=0;i<p.points.Length;i++)
        {p.points[i].x=.5f+(p.points[i].x-.5f)*scale+translation;p.points[i].y=.5f+(p.points[i].y-.5f)*scale;}
        if(!hips)p.points[23].visibility=p.points[24].visibility=.1f;
        return p;
    }
    static void BodyChecks(Action<bool,string> check,int hz)
    {
        var b=Start();var a=new LiveReadyPose();var scaled=new LiveReadyPose();var moved=new LiveReadyPose();
        var input=new PlayerInput{Tracking=true};float dt=1f/hz;long stamp=400000;
        for(int f=0;f<hz;f++)
        {stamp+=1000/hz;a.Tick(Lean(stamp),stamp,input,b,dt,true);scaled.Tick(Lean(stamp,scale:.55f),stamp,input,b,dt,true);moved.Tick(Lean(stamp,translation:.05f),stamp,input,b,dt,true);}
        check(a.BodySide<-.38f&&a.BodyTilt>.19f,"ready body follows lean and shoulder slope "+hz);
        check(Math.Abs(a.BodySide-scaled.BodySide)<.0001f&&Math.Abs(a.BodyTilt-scaled.BodyTilt)<.0001f,"ready body camera scale invariant "+hz);
        check(Math.Abs(a.BodySide-moved.BodySide)<.0001f,"whole-person translation does not change body lean "+hz);
        float held=a.BodySide;
        foreach(float time in new[]{0f,-1f,float.NaN,float.PositiveInfinity})a.Tick(Lean(++stamp,-1),stamp,input,b,time,true);
        check(a.BodySide==held,"invalid delta cannot move ready body "+hz);
        var stale=Lean(stamp);for(int f=0;f<hz;f++)a.Tick(stale,stamp+400+f*1000/hz,input,b,dt,true);
        check(Math.Abs(a.BodySide)<.001f&&Math.Abs(a.BodyTilt)<.001f,"stale body sample releases "+hz);
        for(int f=0;f<hz;f++){stamp+=1000/hz;a.Tick(Lean(stamp,-1),stamp,input,b,dt,true);}
        check(a.BodySide>.38f&&a.BodyTilt<-.19f,"body responds symmetrically to opposite lean "+hz);
        for(int f=0;f<hz;f++){stamp+=1000/hz;a.Tick(Lean(stamp,hips:false),stamp,input,b,dt,true);}
        check(Math.Abs(a.BodySide)<.001f&&a.BodyTilt>.19f,"cropped hips preserve visible shoulder slope "+hz);
        for(int f=0;f<hz;f++){stamp+=1000/hz;a.Tick(Lean(stamp),stamp,input,b,dt,false);}
        check(Math.Abs(a.BodyTilt)<.001f,"keyboard mode releases body "+hz);
        for(int f=0;f<hz;f++){stamp+=1000/hz;a.Tick(Lean(stamp,.02f),stamp,input,b,dt,true);}
        check(Math.Abs(a.BodySide)<.001f&&Math.Abs(a.BodyTilt)<.001f,"camera jitter remains within body dead zone "+hz);
        var invalid=Lean(++stamp);invalid.points[23].x=float.NaN;a.Tick(invalid,stamp,input,b,dt,true);
        check(!float.IsNaN(a.BodySide),"invalid hip cannot poison body "+hz);
        for(int f=0;f<hz;f++){stamp+=1000/hz;a.Tick(Lean(stamp),stamp,new PlayerInput{Tracking=true,BeamIntent=true},b,dt,true);}
        check(a.BodySide<-.38f&&b.Punches==0&&b.Energy==0,"body display cannot score or cancel skill intent "+hz);
        b.Tick(dt,new PlayerInput{Tracking=true,Shield=true});
        for(int f=0;f<hz;f++){stamp+=1000/hz;a.Tick(Lean(stamp),stamp,input,b,dt,true);}
        check(Math.Abs(a.BodySide)<.001f,"accepted guard owns body animation "+hz);
        a.Tick(Lean(++stamp),stamp,input,Start(),dt,false);
        check(a.BodySide==0&&a.BodyTilt==0,"new battle clears body offset "+hz);
    }
}
