// Optional forensic replay using the production PhotoChoiceGesture. Not a relaxed player acceptance gate.
using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using UltramanGame.Core;
static class PhotoResponseAudit
{
    public static void Run(string path)
    {
        var options=new JsonSerializerOptions{IncludeFields=true};
        using var doc=JsonDocument.Parse(File.ReadAllText(path));
        long start=(long)doc.RootElement.GetProperty("saved_timestamp_ms").GetDouble();
        var frames=new List<PoseFrame>();
        foreach(var e in doc.RootElement.GetProperty("frames").EnumerateArray())
        {var f=JsonSerializer.Deserialize<PoseFrame>(e.GetRawText(),options);if(f.capturedMs>=start)frames.Add(f);}
        var gesture=new PhotoChoiceGesture();double rawAccepted=-1;long last=0;int gaps=0;long maxGap=0;
        foreach(var f in frames)
        {
            if(last>0){long gap=f.capturedMs-last;maxGap=Math.Max(maxGap,gap);if(gap>1000)gaps++;}last=f.capturedMs;
            double elapsed=(f.capturedMs-start)/1000.0;
            var choice=gesture.Update(f,f.capturedMs,elapsed>=PhotoChoiceGesture.PreviewSeconds);
            if(choice!=PhotoChoice.None){if(choice!=PhotoChoice.Retake)throw new Exception("Unexpected raw choice");rawAccepted=elapsed;break;}
        }
        var template=frames[0];gesture=new PhotoChoiceGesture();double continuous=-1;
        for(int i=0;i<300;i++)
        {
            double elapsed=i/30.0;var points=(PosePoint[])template.points.Clone();
            points[15].y=elapsed<2.8?.61f:.06f;points[16].y=.61f;
            points[15].visibility=points[16].visibility=elapsed<2.8?.1f:1f;
            var f=new PoseFrame{schema=1,source="synthetic",streamId="continuous-control",sequence=i+1,capturedMs=start+(long)Math.Round(elapsed*1000),tracked=true,points=points};
            if(gesture.Update(f,f.capturedMs,elapsed>=PhotoChoiceGesture.PreviewSeconds)==PhotoChoice.Retake){continuous=elapsed;break;}
        }
        if(continuous<4.7||continuous>5.1)throw new Exception("Continuous control did not respond at the expected hold time");
        Console.WriteLine(JsonSerializer.Serialize(new {rawAcceptedSeconds=rawAccepted,continuousControlSeconds=continuous,gapsOverOneSecond=gaps,maxGapMs=maxGap,receiver="ideal: every published frame delivered immediately; production C# PhotoChoiceGesture unchanged",note="Continuous control is generated independently, not an interpolation or restamping of missing trace frames"}));
    }
}
