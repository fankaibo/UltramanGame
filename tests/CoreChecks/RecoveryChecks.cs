using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UltramanGame.Core;

static class RecoveryChecks
{
    static PoseFrame Frame(string stream,long sequence,long stamp)
    {
        var points=new PosePoint[33];
        for(int i=0;i<33;i++)points[i]=new PosePoint(.5f,.5f);
        points[11]=new PosePoint(.65f,.3f);points[12]=new PosePoint(.35f,.3f);
        points[13]=new PosePoint(.7f,.48f);points[14]=new PosePoint(.3f,.48f);
        points[15]=new PosePoint(.68f,.6f);points[16]=new PosePoint(.32f,.6f);
        return new PoseFrame{schema=1,streamId=stream,sequence=sequence,capturedMs=stamp,tracked=true,points=points};
    }
    static void Step(Battle battle,float seconds)
    {for(float t=0;t<seconds;t+=.02f)battle.Tick(.02f,new PlayerInput{Tracking=true});}
    public static void Run(Action<bool,string> check,bool network=true)
    {
        var frame=Frame("first",1,100000);
        frame.tracked=false;frame.points=Array.Empty<PosePoint>();
        check(PoseQuality.Fresh(frame,100100)&&!PoseQuality.Present(frame,100100),"fresh empty camera frame means no person, not a broken service");
        check(!PoseQuality.Fresh(frame,100351)&&!PoseQuality.Fresh(frame,99949),"stale or future frames cannot display a healthy camera connection");
        frame.schema=2;
        check(!PoseQuality.Fresh(frame,100100)&&!PoseQuality.Fresh(null,100100),"invalid envelope and absent frame are not healthy camera data");

        var battle=new Battle(50);battle.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});Step(battle,2.5f);
        for(int i=0;i<4;i++){battle.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});Step(battle,.6f);}
        float health=battle.EnemyHealth,energy=battle.Energy;
        var presence=new PlayerPresence();var recognizer=new GestureRecognizer();
        frame=Frame("first",100,100000);presence.Update(frame,100000);recognizer.Update(frame,100000);
        battle.Tick(.02f,new PlayerInput{Tracking=presence.Update(frame,101000)});
        check(battle.Phase==GamePhase.Paused,"lost worker pauses the existing battle after presence grace");
        for(int i=0;i<100;i++)battle.Tick(.1f,default);
        check(battle.EnemyHealth==health&&battle.Energy==energy&&battle.HitsTaken==0,"ten-second outage preserves health, accumulated energy and damage taken");
        frame=Frame("replacement",1,111000);frame.points[13]=new PosePoint(.8f,.31f);frame.points[15]=new PosePoint(.98f,.3f);
        var first=recognizer.Update(frame,frame.capturedMs);
        check(!first.LeftPunch&&!first.RightPunch&&!first.Beam,"replacement worker's first extended arm cannot create a phantom attack");
        for(int i=0;i<45;i++)
        {
            frame=Frame("replacement",i+2,111033+i*33);
            var input=recognizer.Update(frame,frame.capturedMs);input.Tracking=presence.Update(frame,frame.capturedMs);
            battle.Tick(.033f,input);
        }
        check(battle.Phase==GamePhase.Battle&&battle.EnemyHealth==health&&battle.Energy==energy&&battle.Punches==4,
            "new stream resumes the same round with prior punches and energy intact");
        check(battle.InstructionRemaining>2&&battle.Enemy==EnemyPhase.Rest,"reconnection gives the child fresh reaction time before an enemy attack");

        if(!network)return;
        var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
        try
        {
            using(var client=new PoseClient(((IPEndPoint)listener.LocalEndpoint).Port))
            {
                var connect=listener.AcceptTcpClientAsync();
                if(!connect.Wait(4000))throw new Exception("First pose connection timed out");
                using(var peer=connect.Result)
                {
                    byte[] bytes=Encoding.UTF8.GetBytes("first\n");peer.GetStream().Write(bytes,0,bytes.Length);
                    check(SpinWait.SpinUntil(()=>client.TakeLatest()=="first",3000),"pose client receives from first worker");
                }
                check(SpinWait.SpinUntil(()=>client.Status!="动作服务已连接",1000),"clean worker EOF clears the connected status");
                connect=listener.AcceptTcpClientAsync();
                if(!connect.Wait(4000))throw new Exception("Pose reconnection timed out");
                using(var peer=connect.Result)
                {
                    byte[] bytes=Encoding.UTF8.GetBytes("replacement\n");peer.GetStream().Write(bytes,0,bytes.Length);
                    check(SpinWait.SpinUntil(()=>client.TakeLatest()=="replacement",3000),"existing pose client automatically receives from replacement worker");
                }
            }
        }
        finally{listener.Stop();}
    }
}
