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
        public static void VoiceCoverage()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/combat-audio/voice-coverage"));
            Directory.CreateDirectory(folder);
            var report=new StringBuilder("heroId\tname\tbeam\trequestedClip\tresolvedClip\tsource\tseconds\n");
            bool tigaOriginal=false,neutralFallback=false;
            for(int i=0;i<HeroRoster.Count;i++)
            {
                var hero=HeroRoster.At(i);
                var requested=Resources.Load<AudioClip>("Voice/"+hero.BeamVoiceKey);
                var fallback=Resources.Load<AudioClip>("Voice/beam");
                var resolved=requested?requested:fallback;
                string source=requested?"hero-specific/original":resolved?"neutral fallback":"missing";
                if(hero.Id=="Tiga"&&hero.BeamVoiceKey=="beam_original"&&requested)tigaOriginal=true;
                if(fallback)neutralFallback=true;
                report.Append(hero.Id).Append('\t').Append(hero.Name).Append('\t').Append(hero.BeamJapanese).Append('\t')
                    .Append(requested?hero.BeamVoiceKey:"-").Append('\t').Append(resolved?resolved.name:"-").Append('\t').Append(source).Append('\t')
                    .AppendLine(resolved?resolved.length.ToString("F3",System.Globalization.CultureInfo.InvariantCulture):"0.000");
            }
            report.AppendLine("policy\tOnly imported hero-specific/original clips are exact; missing licensed recordings use neutral beam.aiff and remain visible in this report.");
            File.WriteAllText(folder+"/voice-coverage.tsv",report.ToString());
            File.WriteAllText(folder+"/summary.txt",FormattableString.Invariant($"tigaOriginal={(tigaOriginal?"passed":"failed")} neutralFallback={(neutralFallback?"passed":"failed")} heroes={HeroRoster.Count}\n"));
            if(!tigaOriginal||!neutralFallback)throw new Exception("Beam voice coverage lost the confirmed Tiga original or neutral fallback clip");
            Debug.Log("[AudioVoiceCoverage] passed heroes="+HeroRoster.Count+" folder="+folder);
        }
        public static void ProjectMusic()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/combat-audio/project-music"));
            Directory.CreateDirectory(folder);
            const string assetPath="Assets/Resources/Audio/miracle_reappearance.mp3";
            var clip=Resources.Load<AudioClip>("Audio/miracle_reappearance");
            var importer=AssetImporter.GetAtPath(assetPath) as AudioImporter;
            if(!clip||importer==null)
            {
                File.WriteAllText(folder+"/validation.txt","result=unavailable local user-provided MP3 is not present\n");
                Debug.Log("[ProjectMusicReview] unavailable local MP3 not present; public checkout remains valid");
                return;
            }
            var settings=importer.defaultSampleSettings;
            if(settings.loadType!=AudioClipLoadType.CompressedInMemory||settings.compressionFormat!=AudioCompressionFormat.Vorbis||!importer.loadInBackground)
                throw new Exception("Project music must remain compressed, Vorbis and loaded in background");
            if(clip.length<1||clip.channels<1||clip.frequency<8000)throw new Exception("Project music clip is empty or invalid");
            string report=FormattableString.Invariant($"result=passed clip={clip.name} seconds={clip.length:F3} channels={clip.channels} frequency={clip.frequency} loadType={settings.loadType} compression={settings.compressionFormat} loadInBackground={importer.loadInBackground}\n");
            File.WriteAllText(folder+"/validation.txt",report);
            Debug.Log("[ProjectMusicReview] "+report.Trim());
        }
        public static void Arrival()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/monster-entrance"));Directory.CreateDirectory(folder);
            var clip=GameAudio.CreateArrivalRoar();var samples=new float[clip.samples];clip.GetData(samples,0);
            float peak=0;double power=0;
            try
            {
                foreach(float value in samples)
                {if(float.IsNaN(value)||float.IsInfinity(value))throw new Exception("Invalid arrival sound");peak=Mathf.Max(peak,Mathf.Abs(value));power+=value*value;}
                if(peak>.8f||peak<.1f||Math.Abs(samples[0])>.001f||Math.Abs(samples[samples.Length-1])>.001f)throw new Exception("Arrival sound clips or pops");
                using(var writer=new BinaryWriter(File.Create(folder+"/arrival-roar.wav")))
                {
                    writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+samples.Length*2);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);
                    writer.Write(clip.frequency);writer.Write(clip.frequency*2);writer.Write((short)2);writer.Write((short)16);writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(samples.Length*2);
                    foreach(float value in samples)writer.Write((short)Mathf.RoundToInt(value*32767));
                }
                string report=FormattableString.Invariant($"passed samples={samples.Length} frequency={clip.frequency} seconds={clip.length:F3} peak={peak:F6} rms={Math.Sqrt(power/samples.Length):F6}");
                File.WriteAllText(folder+"/audio-validation.txt",report);Debug.Log("[ArrivalAudio] "+report);
            }
            finally{UnityEngine.Object.DestroyImmediate(clip);}
        }
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
        Battle projectileBattle;
        int shotSequence;
        float lastHealth=50;
        bool projectileReview,spatialReview,spatialBaseline,finisherReview,clawReview;
        GameWorld finisherWorld;
        AnimatedActor finisherHero,finisherEnemy;
        Camera spatialCamera;
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
                int outputAt=Array.IndexOf(args,"--audio-output");
                projectileReview=Array.IndexOf(args,"--audio-projectiles")>=0;
                clawReview=Array.IndexOf(args,"--audio-claws")>=0;
                finisherReview=Array.IndexOf(args,"--audio-finisher")>=0;
                spatialReview=Array.IndexOf(args,"--audio-spatial")>=0;spatialBaseline=version=="before";
                folder=Path.GetFullPath(outputAt>=0?Path.Combine(args[outputAt+1],version):Path.Combine(Application.dataPath,"../../artifacts/combat-audio/"+version));Directory.CreateDirectory(folder);
                if(projectileReview)
                {
                    projectileBattle=new Battle(50);projectileBattle.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
                    while(projectileBattle.Phase!=GamePhase.Battle)projectileBattle.Tick(.02f,new PlayerInput{Tracking=true});
                    projectileBattle.GiveInstructionTime(30);while(projectileBattle.TryCue(out _)){}
                }
                gameObject.AddComponent<AudioListener>();
                spatialCamera=gameObject.AddComponent<Camera>();spatialCamera.aspect=16f/9;
                rate=AudioSettings.outputSampleRate;channels=AudioSettings.speakerMode==AudioSpeakerMode.Mono?1:2;
                if(AudioSettings.speakerMode!=AudioSpeakerMode.Stereo&&channels!=1)throw new Exception("Review requires stereo or mono output");
                Application.runInBackground=true;Application.targetFrameRate=60;QualitySettings.vSyncCount=0;
                AudioListener.pause=false;AudioListener.volume=1;
                samples=new float[rate*channels*20];recording=true;began=Time.realtimeSinceStartupAsDouble;
                sound=new GameAudio(gameObject){Volume=.75f,MusicEnabled=false};
                if(finisherReview)
                {
                    projectileBattle=new Battle(50);projectileBattle.Tick(.02f,new PlayerInput{Tracking=true,Transform=true});
                    while(projectileBattle.Phase!=GamePhase.Battle)projectileBattle.Tick(.02f,new PlayerInput{Tracking=true});
                    for(int hit=0;hit<15;hit++){projectileBattle.Tick(.02f,new PlayerInput{Tracking=true,LeftPunch=true});for(int i=0;i<22;i++)projectileBattle.Tick(.02f,new PlayerInput{Tracking=true});}
                    projectileBattle.GiveInstructionTime(20);while(projectileBattle.TryCue(out _)){}lastHealth=projectileBattle.EnemyHealth;
                    finisherWorld=new GameWorld();finisherWorld.SetHeroProfile("Tiga");
                    finisherHero=new AnimatedActor("Tiga",finisherWorld.HeroHome,finisherWorld.EnemyHome);
                    finisherEnemy=new AnimatedActor("Golza",finisherWorld.EnemyHome,finisherWorld.HeroHome,true);
                    finisherWorld.BindActors(finisherHero,finisherEnemy);
                    finisherHero.Update(projectileBattle,finisherWorld.Camera,0,0);finisherEnemy.Update(projectileBattle,finisherWorld.Camera,0,0);finisherWorld.Tick(projectileBattle,1,0);
                    began=Time.realtimeSinceStartupAsDouble;
                }
                var tone=AudioClip.Create("Review600Hz",rate*2,1,rate,false);var data=new float[tone.samples];
                for(int i=0;i<data.Length;i++)data[i]=Mathf.Sin(2*Mathf.PI*600*i/rate)*.2f;
                tone.SetData(data,0);RuntimeResources.Own(transform,tone);
                var clips=(Dictionary<string,AudioClip>)typeof(GameAudio).GetField("clips",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(sound);
                clips["Audio/probe"]=tone;
                if(finisherReview)lock(gate){written=0;began=Time.realtimeSinceStartupAsDouble;}
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
                while(frame<(Time.realtimeSinceStartupAsDouble-began)*60&&frame<=(finisherReview?252:900))Step();
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
                if(clawReview){ClawStep();return;}
                if(finisherReview){FinisherStep();return;}
                if(spatialReview){SpatialStep();return;}
                if(projectileReview){ProjectileStep();return;}
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
        void PlayClaw()
        {
            var method=typeof(GameAudio).GetMethod("EnemyAttack");
            if(method==null)sound.Cue(GameCue.EnemyAttack,GamePhase.Battle);
            else method.Invoke(sound,new object[]{true});
        }
        void ClawStep()
        {
            sound.Tick(GamePhase.Battle,false,1/60f);
            sound.SetStage(spatialCamera,new Vector3(-2,0,5),new Vector3(2,0,5));
            if(frame==30||frame==150||frame==270)Event("claw-start",PlayClaw);
            if(frame==54||frame==174||frame==294)Event("shield-contact",()=>sound.Effect("shield",1,sound.HeroPan));
            if(frame==245)Event("warning-voice",()=>sound.Speak("warning",5,GamePhase.Battle));
            if(frame==390){sound.Reset();Event("legacy-slam-start",()=>sound.Cue(GameCue.EnemyAttack,GamePhase.Battle));}
            if(frame>=480)
            {
                var field=typeof(GameAudio).GetField("clawSweep",BindingFlags.Instance|BindingFlags.NonPublic);
                WriteClip(field!=null?(AudioClip)field.GetValue(sound):Resources.Load<AudioClip>("Audio/enemy_rush"),"claw-source.wav",field!=null);
                frame++;Finish();return;
            }
            frame++;
        }
        void FinisherStep()
        {
            const float dt=1/60f;
            sound.Tick(GamePhase.Battle,false,dt);
            projectileBattle.Tick(finisherWorld.BattleDelta(dt,projectileBattle),new PlayerInput{Tracking=true,Beam=frame==12});
            while(projectileBattle.TryCue(out var cue))
            {
                finisherWorld.Cue(cue,projectileBattle);
                if(cue==GameCue.Beam)Event("beam-cry",()=>sound.Cue(cue,GamePhase.Battle));
            }
            finisherHero.Update(projectileBattle,finisherWorld.Camera,dt,frame*dt);
            finisherEnemy.Update(projectileBattle,finisherWorld.Camera,dt,frame*dt);
            bool hit=projectileBattle.EnemyHealth<lastHealth;lastHealth=projectileBattle.EnemyHealth;
            if(hit)finisherWorld.Hit(true,projectileBattle);
            finisherWorld.Tick(projectileBattle,dt,frame*dt);
            sound.SetStage(finisherWorld.Camera,finisherHero.Root.position+Vector3.up*1.7f,finisherWorld.BeamTarget);
            if(hit)Event("beam-contact",()=>sound.Hit(false,true));
            sound.SetChargePower(finisherWorld.ChargePower);
            if(finisherWorld.BeamStarted)Event("beam-launch",()=>
            {
                var release=typeof(GameAudio).GetMethod("BeamRelease");
                if(release!=null)release.Invoke(sound,null);
                else sound.Effect("beam",sound.HasOriginalBeamVoice?.4f:.7f);
            });
            if(frame>=252)
            {
                var release=typeof(GameAudio).GetField("beamRelease",BindingFlags.Instance|BindingFlags.NonPublic);
                WriteClip(release!=null?(AudioClip)release.GetValue(sound):Resources.Load<AudioClip>("Audio/beam"),"beam-release.wav");
                if(lastHealth!=26)throw new Exception("Finisher did not apply exactly one 9-point hit");
                frame++;Finish();return;
            }
            frame++;
        }
        void SpatialStep()
        {
            sound.Tick(GamePhase.Battle,false,1/60f);
            if(channels!=2)throw new Exception("Stereo output is required to verify direction");
            float side=frame<160?1:-1;
            if(!spatialBaseline)sound.SetStage(spatialCamera,new Vector3(-2*side,0,5),new Vector3(2*side,0,5));
            if(frame==30||frame==170)Event("launch-"+(frame==30?"left":"right"),()=>sound.ProjectileLaunch(1));
            if(frame==90||frame==230)Event("contact-"+(frame==90?"right":"left"),()=>sound.ProjectileImpact());
            if(frame==300)Event("center-probe",()=>sound.Effect("probe"));
            if(frame==430)Event("reset",()=>sound.Reset());
            if(frame>=480){frame++;Finish();return;}
            frame++;
        }
        void SpatialChecks()
        {
            sound.Reset();sound.Tick(GamePhase.Battle,false,1/60f);
            sound.SetStage(spatialCamera,new Vector3(-2,0,5),new Vector3(2,0,5));
            if(sound.HeroPan>=-.1f||sound.EnemyPan<=.1f)throw new Exception("Screen space sides are reversed");
            if(GameAudio.ScreenPan(spatialCamera,new Vector3(0,0,-1))!=0||GameAudio.ScreenPan(null,Vector3.zero)!=0||Mathf.Abs(GameAudio.ScreenPan(spatialCamera,new Vector3(500,0,1)))>.45f)throw new Exception("Invalid/hidden source or pan limit failed");
            sound.Effect("probe",1,sound.HeroPan);
            AudioSource held=null;foreach(var src in GetComponents<AudioSource>())if(src.isPlaying&&src.clip.name=="Review600Hz")held=src;
            if(!held)throw new Exception("Missing tail probe");float original=held.panStereo;
            sound.SetStage(spatialCamera,new Vector3(2,0,5),new Vector3(-2,0,5));sound.ProjectileLaunch(1);
            if(held.panStereo!=original)throw new Exception("New lens retuned a playing tail");
            bool right=false;foreach(var src in GetComponents<AudioSource>())if(src.isPlaying&&(src.clip.name=="Hero light bullet"||src.clip.name.StartsWith("TigaDischarge"))&&src.panStereo>0)right=true;
            if(!right)throw new Exception("Mirrored launch did not change sides");
            foreach(string field in new[]{"calm","battle","voice"})
            {
                var src=(AudioSource)typeof(GameAudio).GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(sound);
                if(src.panStereo!=0)throw new Exception("Music or narration panned");
            }
            sound.Reset();int count=GetComponents<AudioSource>().Length;
            for(int i=0;i<40;i++){sound.SetStage(spatialCamera,new Vector3(i%2==0?-2:2,0,5),new Vector3(i%2==0?2:-2,0,5));sound.ProjectileImpact();}
            if(GetComponents<AudioSource>().Length!=count)throw new Exception("Spatial effects allocated sources");
            sound.Tick(GamePhase.Paused,false,.02f);
            foreach(var src in GetComponents<AudioSource>())if(!src.loop&&src.isPlaying)throw new Exception("Pause retained positional effect");
            File.WriteAllText(folder+"/spatial-checks.txt","camera sides, behind-camera fallback, null fallback, pan bound .45, mirrored shot, tail retained, music/voice centered, fixed pool and pause clear: passed\n");
        }
        void ProjectileStep()
        {
            sound.Tick(GamePhase.Battle,false,1/60f);
            var input=new PlayerInput{Tracking=true};
            if(frame==30||frame==130||frame==210||frame==240||frame==270||frame==390)
            {
                input.RangedAttack=true;input.LeftPunch=frame==130||frame==240;input.RightPunch=!input.LeftPunch;
                input.AttackSpeed=frame==30?.65f:frame==130?1f:1.7f;
                events.AppendLine(frame+",input-"+input.AttackSpeed);
            }
            projectileBattle.Tick(1/60f,input);
            while(projectileBattle.TryCue(out var cue))
                if(cue==GameCue.Punch)sound.Cue(cue,GamePhase.Battle);
            if(projectileBattle.EnemyHealth<lastHealth)
                Event("projectile-contact",()=>sound.ProjectileImpact());
            lastHealth=projectileBattle.EnemyHealth;
            if(projectileBattle.Shot.Sequence>shotSequence)
            {shotSequence=projectileBattle.Shot.Sequence;Event("projectile-launch",()=>sound.ProjectileLaunch(projectileBattle.Shot.Speed));}
            if(frame==360)Event("warning-voice",()=>sound.Speak("warning",5,GamePhase.Battle));
            if(frame==460)Event("reset",()=>sound.Reset());
            if(frame==480)Event("beam-contact-reference",()=>Hit(false,true));
            if(frame>=600)
            {
                if(lastHealth!=44||projectileBattle.Energy!=6)throw new Exception("Projectile review did not deliver six actual hits");
                frame++;Finish();return;
            }
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
            if(projectileReview)
            {
                var legacy=(AudioClip)typeof(GameAudio).GetField("projectileLaunch",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(sound);
                WriteClip(legacy,"legacy-launch.wav");
                var field=typeof(GameAudio).GetField("projectileLaunches",BindingFlags.Instance|BindingFlags.NonPublic);
                var launches=field==null?new[]{legacy}:(AudioClip[])field.GetValue(sound);
                for(int i=0;i<launches.Length;i++)WriteClip(launches[i],"launch-"+i+".wav");
            }
            if(spatialReview)SpatialChecks();
            else if(folder.EndsWith("/after"))Checks();
            if(clawReview&&folder.EndsWith("/after"))ClawChecks();
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
            sound.Tick(GamePhase.Battle,true,1/60f);sound.Hit(true,false);sound.Effect("swing");sound.ProjectileLaunch(1);sound.ProjectileImpact();
            foreach(var source in GetComponents<AudioSource>())if(!source.loop&&source.isPlaying)throw new Exception("Mute allowed a transient");
            sound.Tick(GamePhase.Battle,false,1/60f);sound.Hit(false,false);sound.ProjectileLaunch(1);sound.ProjectileImpact();sound.Tick(GamePhase.Paused,false,1/60f);
            foreach(var source in GetComponents<AudioSource>())if(!source.loop&&source.isPlaying)throw new Exception("Pause retained an effect or voice");
            report.AppendLine("mute rejects new transients and pause clears active transients: passed");
            if(finisherReview)
            {
                var release=typeof(GameAudio).GetMethod("BeamRelease");
                if(release==null)throw new Exception("Missing release dispatcher");
                Action fire=()=>release.Invoke(sound,null);
                AudioSource ActiveRelease()
                {foreach(var source in GetComponents<AudioSource>())if(source.isPlaying&&source.clip&&source.clip.name=="Tiga beam discharge")return source;return null;}
                sound.Reset();sound.HeroId="Tiga";sound.Tick(GamePhase.Battle,false,1/60f);fire();
                var open=ActiveRelease();if(!open||Mathf.Abs(open.volume-.75f*.75f*.65f)>.001f)throw new Exception("Speech-free release still attenuated");
                sound.Reset();sound.Speak("beam_original",5,GamePhase.Battle);fire();
                var ducked=ActiveRelease();if(!ducked||Mathf.Abs(ducked.volume-.75f*.75f*.42f*.4f)>.001f)throw new Exception("Release masked the original cry");
                sound.Tick(GamePhase.Paused,false,1/60f);
                foreach(var source in GetComponents<AudioSource>())if(!source.loop&&source.isPlaying)throw new Exception("Pause retained discharge");
                sound.Tick(GamePhase.Battle,true,1/60f);fire();if(ActiveRelease())throw new Exception("Muted discharge played");
                sound.Tick(GamePhase.Battle,false,1/60f);sound.Reset();sound.HeroId="Zero";fire();
                bool legacy=false;foreach(var source in GetComponents<AudioSource>())if(source.isPlaying&&source.clip==Resources.Load<AudioClip>("Audio/beam"))legacy=true;
                if(!legacy||ActiveRelease())throw new Exception("Other hero release replaced");
                sound.Reset();sound.HeroId="Tiga";
                report.AppendLine("Tiga immediate discharge, speech-free gain .65, speaking gain .4 plus duck .42, pause/mute clear and other hero sample preserved: passed");
            }
            if(projectileReview)
            {
                var original=(AudioClip)typeof(GameAudio).GetField("beamContact",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(sound);
                var contacts=(AudioClip[])typeof(GameAudio).GetField("projectileContacts",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(sound);
                WriteClip(original,"legacy-contact.wav");
                for(int i=0;i<contacts.Length;i++)WriteClip(contacts[i],"projectile-contact-"+i+".wav");
                sound.Reset();sound.HeroId="Tiga";sound.Tick(GamePhase.Battle,false,1/60f);
                int count=GetComponents<AudioSource>().Length;
                for(int i=0;i<40;i++)sound.ProjectileImpact();
                int active=0;
                foreach(var source in GetComponents<AudioSource>())
                    if(!source.loop&&source.isPlaying)
                    {active++;if(!source.clip.name.StartsWith("ProjectileContact"))throw new Exception("Tiga bullet reused a finisher sound");}
                if(active==0||active>10||count!=GetComponents<AudioSource>().Length)throw new Exception("Projectile voices escaped their fixed pool");
                sound.Reset();sound.HeroId="Zero";sound.ProjectileImpact();
                bool legacy=false;foreach(var source in GetComponents<AudioSource>())if(source.isPlaying&&source.clip==original)legacy=true;
                if(!legacy)throw new Exception("Other hero contact unexpectedly changed");
                sound.Reset();sound.HeroId="Tiga";
                report.AppendLine("Tiga dedicated contact, fixed pool under 40 hits, other hero contact preserved: passed");
                var launchField=typeof(GameAudio).GetField("projectileLaunches",BindingFlags.Instance|BindingFlags.NonPublic);
                if(launchField!=null)
                {
                    var launches=(AudioClip[])launchField.GetValue(sound);var used=new HashSet<AudioClip>();
                    for(int i=0;i<40;i++)
                    {
                        sound.Reset();sound.Tick(GamePhase.Battle,false,1/60f);
                        float speed=i%2==0?AttackTempo.Min:AttackTempo.Max;sound.ProjectileLaunch(speed);
                        foreach(var src in GetComponents<AudioSource>())if(!src.loop&&src.isPlaying)
                        {
                            if(Array.IndexOf(launches,src.clip)<0||Mathf.Abs(src.pitch-(i%2==0?.90f:1.12f))>.001f)
                                throw new Exception("Tiga launch bypassed cached variants or changed tempo mapping");
                            used.Add(src.clip);
                        }
                    }
                    if(used.Count!=3||GetComponents<AudioSource>().Length!=count)throw new Exception("Launch variation or fixed pool failed");
                    sound.Reset();sound.HeroId="Zero";sound.ProjectileLaunch(1);
                    var old=(AudioClip)typeof(GameAudio).GetField("projectileLaunch",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(sound);
                    bool kept=false;foreach(var src in GetComponents<AudioSource>())if(src.isPlaying&&src.clip==old)kept=true;
                    if(!kept)throw new Exception("Other hero launch changed");
                    sound.Reset();sound.HeroId="Tiga";
                    report.AppendLine("three cached launch variants, 40 launches, original tempo pitch, fixed source pool, other hero launch preserved: passed");
                }
            }
            File.WriteAllText(folder+"/checks.txt",report.ToString());
        }
        void ClawChecks()
        {
            var method=typeof(GameAudio).GetMethod("EnemyAttack");if(method==null)throw new Exception("Missing typed enemy sound");
            sound.Reset();sound.Tick(GamePhase.Battle,false,.02f);int count=GetComponents<AudioSource>().Length;
            PlayClaw();bool active=false;
            foreach(var src in GetComponents<AudioSource>())if(!src.loop&&src.isPlaying)
            {if(src.clip.name!="Golza claw sweep"||src.pitch!=1)throw new Exception("Claw used generic rush or retimed its contact");active=true;}
            if(!active)throw new Exception("No claw sound");
            sound.Reset();method.Invoke(sound,new object[]{false});bool legacy=false;
            foreach(var src in GetComponents<AudioSource>())if(!src.loop&&src.isPlaying)legacy|=src.clip==Resources.Load<AudioClip>("Audio/enemy_rush");
            if(!legacy)throw new Exception("Slam sound changed");
            sound.Reset();for(int i=0;i<40;i++)PlayClaw();if(GetComponents<AudioSource>().Length!=count)throw new Exception("Claw allocated sources");
            sound.Tick(GamePhase.Paused,false,.02f);foreach(var src in GetComponents<AudioSource>())if(!src.loop&&src.isPlaying)throw new Exception("Paused claw continued");
            sound.Tick(GamePhase.Battle,true,.02f);PlayClaw();foreach(var src in GetComponents<AudioSource>())if(!src.loop&&src.isPlaying)throw new Exception("Muted claw played");
            sound.Tick(GamePhase.Battle,false,.02f);PlayClaw();sound.Reset();foreach(var src in GetComponents<AudioSource>())if(!src.loop&&src.isPlaying)throw new Exception("Reset claw continued");
            File.WriteAllText(folder+"/claw-checks.txt","claw-specific/fixed-pitch/legacy-slam/fixed-pool/pause/mute/reset=passed\n");
        }
        void WriteClip(AudioClip clip,string name,bool checkEndpoints=true)
        {
            var data=new float[clip.samples*clip.channels];clip.GetData(data,0);
            foreach(float value in data)if(float.IsNaN(value)||float.IsInfinity(value)||Mathf.Abs(value)>=.98f)throw new Exception("Contact sample clips or is invalid");
            File.WriteAllText(folder+"/"+name+".endpoints.txt",FormattableString.Invariant($"first={data[0]:R} last={data[data.Length-1]:R} enforced={checkEndpoints}\n"));
            if(checkEndpoints&&(Mathf.Abs(data[0])>.001f||Mathf.Abs(data[data.Length-1])>.001f))throw new Exception("Contact has an abrupt endpoint");
            using(var writer=new BinaryWriter(File.Create(folder+"/"+name)))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+data.Length*4);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)3);writer.Write((short)clip.channels);
                writer.Write(clip.frequency);writer.Write(clip.frequency*clip.channels*4);writer.Write((short)(clip.channels*4));writer.Write((short)32);writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(data.Length*4);
                foreach(float value in data)writer.Write(value);
            }
        }
        void Fail(Exception e){Debug.LogException(e);recording=false;EditorApplication.Exit(1);}
    }
}
