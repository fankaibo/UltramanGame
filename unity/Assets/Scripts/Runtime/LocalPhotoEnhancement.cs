using System;
using System.IO;
using System.Threading;
using System.Diagnostics;
using UnityEngine;

namespace UltramanGame.Runtime
{
    // Model credentials remain in the user's local configuration. A bounded
    // background worker never blocks the guided game or replaces the original.
    public sealed class LocalPhotoEnhancement
    {
        static readonly SemaphoreSlim Slot=new SemaphoreSlim(1,1);
        public volatile bool Done;
        public byte[] ResultPng {get;private set;}
        public string Status {get;private set;}="原图已保存 · AI 融合处理中";
        readonly string source,folder,python,script;
        [Serializable] sealed class Result {public bool ok;public string output,status;}
        public LocalPhotoEnhancement(string source,byte[] plate,byte[] mask)
        {
            this.source=source;
            var root=new DirectoryInfo(Application.dataPath);
            while(root!=null&&!File.Exists(Path.Combine(root.FullName,"scripts","enhance_photo.py")))root=root.Parent;
            if(root==null){Status="原图已保存 · 未找到 AI 美化程序";Done=true;return;}
            script=Path.Combine(root.FullName,"scripts","enhance_photo.py");python=Path.Combine(root.FullName,".venv","bin","python");
            folder=Path.Combine(Path.GetTempPath(),"tiga-photo-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder,"plate.png"),plate);File.WriteAllBytes(Path.Combine(folder,"mask.png"),mask);
            new Thread(Run){IsBackground=true,Name="Photo AI harmony"}.Start();
        }
        static string Quote(string text)=>"\""+text.Replace("\\","\\\\").Replace("\"","\\\"")+"\"";
        void Run()
        {
            bool entered=false;
            try
            {
                entered=Slot.Wait(TimeSpan.FromSeconds(90));if(!entered){Status="原图已保存 · AI 繁忙，请稍后重试";return;}
                string statusPath=Path.Combine(folder,"result.json");
                var start=new ProcessStartInfo(python,Quote(script)+" --input "+Quote(source)+" --plate "+Quote(Path.Combine(folder,"plate.png"))+" --mask "+Quote(Path.Combine(folder,"mask.png"))+" --status "+Quote(statusPath))
                    {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
                using(var process=Process.Start(start))
                {
                    // Both streams are drained; no exception text or gateway body is logged.
                    process.BeginOutputReadLine();process.BeginErrorReadLine();
                    if(!process.WaitForExit(95000)){process.Kill();Status="原图已保存 · AI 超时，稍后再试";return;}
                }
                if(File.Exists(statusPath))
                {
                    var result=JsonUtility.FromJson<Result>(File.ReadAllText(statusPath));Status=result.status;
                    string expected=Path.Combine(Path.GetDirectoryName(source),Path.GetFileNameWithoutExtension(source)+"_AI.png");
                    if(result.ok&&result.output==expected&&File.Exists(expected)&&new FileInfo(expected).Length<32*1024*1024)ResultPng=File.ReadAllBytes(expected);
                }
                else Status="原图已保存 · AI 暂未完成";
            }
            catch(Exception){Status="原图已保存 · AI 暂不可用";}
            finally
            {
                if(entered)Slot.Release();
                try{if(folder!=null&&Directory.Exists(folder))Directory.Delete(folder,true);}catch(IOException){}
                Done=true;
            }
        }
    }
}
