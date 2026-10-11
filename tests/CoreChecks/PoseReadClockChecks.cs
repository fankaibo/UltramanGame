using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using UltramanGame.Core;

static class PoseReadClockChecks
{
    public static void Run(Action<bool,string> check)
    {
        long clock=100000;
        var options=new JsonSerializerOptions {IncludeFields=true};
        var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
        try
        {
            using(var client=new PoseClient(((IPEndPoint)listener.LocalEndpoint).Port,()=>clock))
            using(var sender=listener.AcceptTcpClient())
            using(var stream=sender.GetStream())
            {
                var points=new PosePoint[33];for(int i=0;i<33;i++)points[i]=new PosePoint(.5f,.5f);
                points[11]=new PosePoint(.65f,.3f);points[12]=new PosePoint(.35f,.3f);
                long sequence=0;
                PoseFrame Send(long captured,int decodeMs,out long observed)
                {
                    var frame=new PoseFrame {schema=1,streamId="clock",sequence=++sequence,capturedMs=captured,tracked=true,points=points};
                    var bytes=Encoding.UTF8.GetBytes(JsonSerializer.Serialize(frame,options)+"\n");stream.Write(bytes,0,bytes.Length);
                    PoseFrame found=null;long time=0;
                    bool received=SpinWait.SpinUntil(()=>
                    {
                        found=client.TakeLatestPose(line=>{clock+=decodeMs;return JsonSerializer.Deserialize<PoseFrame>(line,options);},out time);
                        return found!=null;
                    },2000);
                    if(!received)throw new Exception("Pose clock loopback did not receive packet");observed=time;return found;
                }
                // A real background socket delivers while the UI clock is old.
                // Advance only the injected clock: the test is independent of
                // CPU scheduling and does not weaken production freshness.
                long renderStart=clock;clock+=160;
                var fresh=Send(clock-10,80,out var readAt);
                check(!PoseQuality.Fresh(fresh,renderStart),"old render clock falsely rejects newly received pose as future");
                check(readAt==100240&&PoseQuality.Present(fresh,readAt),"consume clock follows preview work and JSON decoding");
                check(fresh.capturedMs==100150,"consumption does not rewrite camera capture time");
                var presence=new PlayerPresence();check(presence.Update(fresh,readAt),"fresh consumed pose keeps player present");

                var stale=Send(clock-100,400,out readAt);
                check(!PoseQuality.Fresh(stale,readAt),"pose expiring during parse is still rejected");
                var future=Send(clock+1000,0,out readAt);
                check(!PoseQuality.Fresh(future,readAt),"genuinely future pose is still rejected");
                clock+=700;
                check(client.TakeLatestPose(_=>throw new Exception("Empty slot parsed"),out readAt)==null&&readAt==clock,"empty slot still samples current time");
                check(!presence.Update(fresh,readAt),"empty/stale stream cannot extend presence grace");
                var resumed=Send(clock,0,out readAt);
                check(PoseQuality.Present(resumed,readAt),"new live packet recovers after genuine stale input");
                var oldRecognizer=new GestureRecognizer();var fixedRecognizer=new GestureRecognizer();
                int oldBeams=0,newBeams=0;
                for(int i=0;i<60;i++)
                {
                    clock+=40;long frameStart=clock-120;
                    points[13]=new PosePoint(.69f,.48f);points[14]=new PosePoint(.31f,.48f);
                    points[15]=new PosePoint(.67f,.61f);points[16]=new PosePoint(.33f,.61f);
                    if(i>=20)
                    {
                        points[13]=new PosePoint(.54f,.50f);points[15]=new PosePoint(.54f,.27f);
                        points[14]=new PosePoint(.28f,.41f);points[16]=new PosePoint(.53f,.41f);
                    }
                    var pose=Send(clock,0,out readAt);
                    if(oldRecognizer.Update(pose,frameStart,true,false).Beam)oldBeams++;
                    if(fixedRecognizer.Update(pose,readAt,true,false).Beam)newBeams++;
                }
                check(oldBeams==0&&newBeams==1,"same delayed-consumption beam hold fires once without stale-clock resets");
            }
        }
        finally{listener.Stop();}
    }
}
