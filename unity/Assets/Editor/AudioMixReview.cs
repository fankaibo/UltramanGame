using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UltramanGame.Core;
using UltramanGame.Runtime;

namespace UltramanGame.Editor
{
    [InitializeOnLoad]
    public static class AudioMixReview
    {
        const string Pending="UltramanGame.AudioMixReview.Pending";
        static AudioMixReview()
        {if(SessionState.GetBool(Pending,false))EditorApplication.playModeStateChanged+=Entered;}
        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            SessionState.SetBool(Pending,true);
            EditorApplication.playModeStateChanged+=Entered;
            EditorApplication.isPlaying=true;
        }
        static void Entered(PlayModeStateChange change)
        {
            if(change!=PlayModeStateChange.EnteredPlayMode)return;
            EditorApplication.playModeStateChanged-=Entered;
            SessionState.SetBool(Pending,false);
            new GameObject("Audio review").AddComponent<AudioMixRecording>();
        }
    }

    public sealed class AudioMixRecording : MonoBehaviour
    {
        GameAudio sound;
        float[] samples;
        readonly object gate=new object();
        readonly StringBuilder events=new StringBuilder("frame,event\n");
        int frame,rate,channels,written;
        double began;
        string folder;
        volatile bool recording;
        void Start()
        {
            try
            {
                var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--audio-version");
                string version=at>=0?args[at+1]:"after";
                if(version!="before"&&version!="after")throw new Exception("Invalid review version");
                folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/combat-audio/"+version));Directory.CreateDirectory(folder);
                gameObject.AddComponent<AudioListener>();
                gameObject.AddComponent<Camera>();
                rate=AudioSettings.outputSampleRate;channels=AudioSettings.speakerMode==AudioSpeakerMode.Mono?1:2;
                if(AudioSettings.speakerMode!=AudioSpeakerMode.Stereo&&channels!=1)throw new Exception("Review requires stereo or mono output");
                Application.runInBackground=true;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;
                AudioListener.pause=false;AudioListener.volume=1;
                samples=new float[rate*channels*20];recording=true;began=Time.realtimeSinceStartupAsDouble;
                sound=new GameAudio(gameObject){Volume=.75f,MusicEnabled=false};
                var tone=AudioClip.Create("Review600Hz",rate*2,1,rate,false);var data=new float[tone.samples];
                for(int i=0;i<data.Length;i++)data[i]=Mathf.Sin(2*Mathf.PI*600*i/rate)*.2f;
                tone.SetData(data,0);RuntimeResources.Own(transform,tone);
                var clips=(Dictionary<string,AudioClip>)typeof(GameAudio).GetField("clips",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(sound);
                clips["Audio/probe"]=tone;
                Debug.Log($"[AudioMixReview] started rate={rate} channels={channels} dsp={AudioSettings.dspTime}");
            }
            catch(Exception e){Fail(e);}
        }
        void Event(string name,Action play){events.AppendLine(frame+","+name);play();}
        void Hit(bool heavy=false,bool beam=false)
        {
            var hit=typeof(GameAudio).GetMethod("Hit");
            if(hit!=null)hit.Invoke(sound,new object[]{heavy,beam});
            else {sound.Effect("impact",beam?1:.8f);if(heavy)sound.Effect("combo",.82f);}
        }
        void Update()
        {
            if(!recording)return;
            try
            {
                while(frame<(Time.realtimeSinceStartupAsDouble-began)*60&&frame<=900)Step();
            }
            catch(Exception e){Fail(e);}
        }
        void OnAudioFilterRead(float[] data,int countChannels)
        {
            lock(gate)
            {
                if(!recording||samples==null||countChannels!=channels)return;
                int count=Math.Min(data.Length,samples.Length-written);Array.Copy(data,0,samples,written,count);written+=count;
            }
        }
        void Step()
        {
                sound.Tick(GamePhase.Battle,false,1/60f);
                if(frame==12)Event("overlap-probe",()=>sound.Effect("probe"));
                if(frame==36)Event("overlapping-swing",()=>sound.Effect("swing",.7f));
                if(frame==100)Event("reset",()=>sound.Reset());
                if(frame==132||frame==180||frame==228)Event("normal-swing",()=>sound.Cue(GameCue.Punch,GamePhase.Battle));
                if(frame==144||frame==192||frame==240)Event("normal-hit",()=>Hit());
                if(frame==282)Event("heavy-swing",()=>sound.Cue(GameCue.Punch,GamePhase.Battle));
                if(frame==294)Event("heavy-hit",()=>Hit(true));
                if(frame==360)Event("speech-probe",()=>sound.Effect("probe"));
                if(frame==390)Event("warning-voice",()=>sound.Speak("warning",5,GamePhase.Battle));
                if(frame==570)Event("reset",()=>sound.Reset());
                if(frame>=600&&frame<660)sound.SetChargePower((frame-600)/60f);
                if(frame==660){sound.SetChargePower(0);Event("original-beam-voice",()=>sound.Cue(GameCue.Beam,GamePhase.Battle));}
                if(frame==678)Event("beam-launch",()=>sound.Effect("beam",.4f));
                if(frame==708)Event("beam-contact",()=>Hit(false,true));
                if(frame==840)Event("reset",()=>sound.Reset());
                if(frame>=900){frame++;Finish();return;}
                frame++;
        }
        void Finish()
        {
            lock(gate){recording=false;Array.Resize(ref samples,written);}sound.Reset();
            float peak=0;double power=0;foreach(float s in samples){if(float.IsNaN(s)||float.IsInfinity(s))throw new Exception("Nonfinite mix");peak=Mathf.Max(peak,Mathf.Abs(s));power+=s*s;}
            if(peak<.01f)throw new Exception("Unity captured silence");
            using(var writer=new BinaryWriter(File.Create(folder+"/mix.wav")))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+samples.Length*4);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)3);writer.Write((short)channels);
                writer.Write(rate);writer.Write(rate*channels*4);writer.Write((short)(channels*4));writer.Write((short)32);writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(samples.Length*4);foreach(float s in samples)writer.Write(s);
            }
            File.WriteAllText(folder+"/events.csv",events.ToString());
            File.WriteAllText(folder+"/capture.txt",FormattableString.Invariant($"rate={rate} channels={channels} frames={frame} samples={samples.Length} seconds={samples.Length/(float)(rate*channels):F4} peak={peak:F6} rms={Math.Sqrt(power/samples.Length):F6}\n"));
            if(folder.EndsWith("/after"))Checks();
            var hashes=new StringBuilder();using(var sha=System.Security.Cryptography.SHA256.Create())
            foreach(string file in new[]{"Runtime/GameAudio.cs","Runtime/CombatAudioVoices.cs","Runtime/CombatImpactSounds.cs","Runtime/ArenaController.cs"})
            {string path=Path.Combine(Application.dataPath,"Scripts",file);hashes.AppendLine(file+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant());}
            File.WriteAllText(folder+"/sources.txt",hashes.ToString());
            Debug.Log("[AudioMixReview] captured "+folder);EditorApplication.Exit(0);
        }
        void Checks()
        {
            var report=new StringBuilder();var probe=AudioClip.Create("Pool probe",rate,1,rate,false);RuntimeResources.Own(transform,probe);
            var owner=new GameObject("Silent pool checks");var pool=new CombatAudioVoices(owner,3);pool.SetVolume(0);
            pool.Play(probe,.8f,.7f,3);pool.Play(probe,.5f,1.4f,0);pool.Play(probe,.5f,1,0);
            var sources=owner.GetComponents<AudioSource>();
            if(sources.Length!=3||!sources[0].isPlaying||Mathf.Abs(sources[0].pitch-.7f)>.001f)throw new Exception("Pool retuned or lost its first voice");
            for(int i=0;i<30;i++)pool.Play(probe,.5f,1.1f,0);
            if(owner.GetComponents<AudioSource>().Length!=3||!sources[0].isPlaying||Mathf.Abs(sources[0].pitch-.7f)>.001f)throw new Exception("Pool saturation stole an important tail or allocated sources");
            pool.SetVolume(.2f);
            if(Mathf.Abs(sources[0].volume-.16f)>.001f)throw new Exception("Mix gain lost individual clip gain");
            pool.Stop();foreach(var source in sources)if(source.isPlaying)throw new Exception("Reset left a voice playing");
            report.AppendLine("independent pitch, bounded polyphony, priority stealing, per-clip mix gain and stop: passed");
            Destroy(owner);
            sound.Tick(GamePhase.Battle,true,1/60f);sound.Hit(true,false);sound.Effect("swing");
            foreach(var source in GetComponents<AudioSource>())if(!source.loop&&source.isPlaying)throw new Exception("Mute allowed a transient");
            sound.Tick(GamePhase.Battle,false,1/60f);sound.Hit(false,false);sound.Tick(GamePhase.Paused,false,1/60f);
            foreach(var source in GetComponents<AudioSource>())if(!source.loop&&source.isPlaying)throw new Exception("Pause retained an effect or voice");
            report.AppendLine("mute rejects new transients and pause clears active transients: passed");
            File.WriteAllText(folder+"/checks.txt",report.ToString());
        }
        void Fail(Exception e){Debug.LogException(e);recording=false;EditorApplication.Exit(1);}
    }
}
