// Offline observations from the actual recognizer, not a substitute recognition rule.
using System;
using System.IO;
using System.Text.Json;
using UltramanGame.Core;

static class PoseReplayAudit
{
    static readonly JsonSerializerOptions Json=new JsonSerializerOptions{IncludeFields=true};
    public static void Run(string input,string output,int difficulty)
    {
        if(difficulty<0||difficulty>2)throw new ArgumentOutOfRangeException(nameof(difficulty));
        var recognizer=new GestureRecognizer{Difficulty=difficulty};
        long previous=-1;int rows=0;
        using var writer=new StreamWriter(new FileStream(output,FileMode.CreateNew,FileAccess.Write));
        foreach(string line in File.ReadLines(input))
        {
            using var doc=JsonDocument.Parse(line);var root=doc.RootElement;
            long mediaMs=root.GetProperty("media_ms").GetInt64();
            if(mediaMs<0||mediaMs<=previous)throw new InvalidDataException("Replay requires strictly increasing media timestamps.");
            previous=mediaMs;
            var frame=JsonSerializer.Deserialize<PoseFrame>(root.GetProperty("frame").GetRawText(),Json);
            // The file has media time, not wall time. Do not manufacture additional
            // observations or sleep according to inference / rendering performance.
            if(frame.capturedMs!=100000+mediaMs||frame.sequence!=++rows)
                throw new InvalidDataException("Pose and source frame are not aligned.");
            bool beam=root.GetProperty("beam_available").GetBoolean();
            bool transform=root.GetProperty("transform_available").GetBoolean();
            bool priority=root.GetProperty("shield_priority").GetBoolean();
            var result=recognizer.Update(frame,frame.capturedMs,beam,transform,priority);
            writer.WriteLine(JsonSerializer.Serialize(new{media_ms=mediaMs,input=result,
                reference_pose=recognizer.ReferencePose,beam_progress=recognizer.BeamProgress,
                shield_progress=recognizer.ShieldProgress,transform_progress=recognizer.TransformProgress,
                beam_needs_release=recognizer.BeamNeedsRelease,
                transform_needs_release=recognizer.TransformNeedsRelease},Json));
        }
        if(rows==0)throw new InvalidDataException("Replay has no frames.");
    }

    public static void Checks(Action<bool,string> check)
    {
        string directory=Path.Combine(Path.GetTempPath(),"pose-replay-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string input=Path.Combine(directory,"input.jsonl"),output=Path.Combine(directory,"output.jsonl");
            var points=new PosePoint[33];for(int i=0;i<33;i++)points[i]=new PosePoint(.5f,.5f);
            points[11]=new PosePoint(.65f,.35f);points[12]=new PosePoint(.35f,.35f);
            points[13]=new PosePoint(.68f,.39f);points[14]=new PosePoint(.32f,.39f);
            points[15]=new PosePoint(.58f,.20f);points[16]=new PosePoint(.42f,.20f);
            using(var writer=new StreamWriter(input))
            for(int i=0;i<35;i++)
            {
                int ms=i*33+(i>=30?500:0);
                writer.WriteLine(JsonSerializer.Serialize(new{media_ms=ms,beam_available=false,
                    transform_available=false,shield_priority=false,
                    frame=new PoseFrame{schema=1,source="synthetic",streamId="replay-check",sequence=i+1,
                        capturedMs=100000+ms,tracked=i<34,points=i<34?points:Array.Empty<PosePoint>()}},Json));
            }
            Run(input,output,1);var lines=File.ReadAllLines(output);
            check(lines.Length==35,"offline replay neither fills capture gaps nor drops samples");
            using(var row=JsonDocument.Parse(lines[29]))
            {
                var value=row.RootElement.GetProperty("input");
                check(value.GetProperty("Shield").GetBoolean(),"offline production classifier confirms a high guard");
                check(!value.GetProperty("Beam").GetBoolean()&&!value.GetProperty("Transform").GetBoolean(),
                    "offline context does not implicitly enable beam or transform");
            }
            using(var row=JsonDocument.Parse(lines[30]))
                check(row.RootElement.GetProperty("media_ms").GetInt64()==1490,"offline capture gap retains source clock");
            using(var row=JsonDocument.Parse(lines[34]))
                check(!row.RootElement.GetProperty("input").GetProperty("Tracking").GetBoolean(),"offline no-person frame remains untracked");
            bool refused=false;try{Run(input,output,1);}catch(IOException){refused=true;}
            check(refused,"offline replay will not overwrite previous evidence");
            var first=File.ReadAllLines(input)[0];File.WriteAllText(input,first+"\n"+first+"\n");
            refused=false;try{Run(input,Path.Combine(directory,"bad.jsonl"),1);}catch(InvalidDataException){refused=true;}
            check(refused,"offline replay rejects duplicated media timestamps");
        }
        finally{Directory.Delete(directory,true);}
    }
}
