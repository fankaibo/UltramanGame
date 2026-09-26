using System.Collections.Generic;
using UnityEngine;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    public sealed class GameAudio
    {
        readonly AudioSource calm,battle,voice,effects;
        AudioClip localMusic;
        readonly AudioClip battleStinger,landingThud;
        readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
        struct Line { public string Key;public int Priority;public float Expires;public GamePhase Phase; }
        readonly List<Line> pending=new List<Line>();
        int priority;
        bool muted,beamVoice;
        GamePhase phase;
        float phaseAge;
        bool phaseReported;
        int effectSequence;
        public bool VoicePlaying=>voice.isPlaying;
        public bool HasVoice(string key)=>clips.TryGetValue("Voice/"+key,out var clip)&&clip;
        public float VoiceLength(string key) {var clip=Clip("Voice/"+key);return clip?clip.length:0;}
        public bool MusicEnabled=true;
        public float Volume=.75f,MusicVolume=.45f;
        public event System.Action<string,float> InstructionStarted;
        public string HeroId="Tiga";
        public bool HasOriginalBeamVoice => clips.TryGetValue("Voice/beam_original",out var original) && original!=null;
        public string Diagnostics => $"calmPlaying={calm.isPlaying} battlePlaying={battle.isPlaying} voicePlaying={voice.isPlaying} beamOriginal={HasOriginalBeamVoice} battleStinger={battleStinger!=null} calmVolume={calm.volume:F3} battleVolume={battle.volume:F3} localMusic={localMusic!=null} effectsPitch={effects.pitch:F2} muted={muted}";
        AudioSource Source(GameObject owner)
        { var s=owner.AddComponent<AudioSource>();s.playOnAwake=false;s.spatialBlend=0;s.dopplerLevel=0;return s; }
        public GameAudio(GameObject owner)
        {
            calm=Source(owner);battle=Source(owner);voice=Source(owner);effects=Source(owner);
            battleStinger=CreateBattleStinger();
            landingThud=CreateLandingThud();
            // Load once at startup so a first punch/voice line does not perform resource I/O mid-fight.
            foreach(var clip in Resources.LoadAll<AudioClip>("Audio"))clips["Audio/"+clip.name]=clip;
            foreach(var clip in Resources.LoadAll<AudioClip>("Voice"))clips["Voice/"+clip.name]=clip;
            calm.clip=Clip("Audio/music_ready");battle.clip=Clip("Audio/music_battle");
            calm.loop=battle.loop=true;calm.volume=battle.volume=0;calm.Play();battle.Play();
            Volume=PlayerPrefs.GetFloat("sound.master",.75f);MusicVolume=PlayerPrefs.GetFloat("sound.music",.45f);
            MusicEnabled=PlayerPrefs.GetInt("sound.musicEnabled",1)==1;
            if(Debug.isDebugBuild) Debug.Log($"[Audio] musicReady={calm.clip!=null} musicBattle={battle.clip!=null} voice={Clip("Voice/welcome")!=null}");
        }
        AudioClip Clip(string key)
        {
            if(!clips.TryGetValue(key,out var clip))
            { clip=Resources.Load<AudioClip>(key);clips[key]=clip;if(!clip)Debug.LogWarning("Missing audio: "+key); }
            return clip;
        }
        static AudioClip CreateBattleStinger()
        {
            const int sampleRate=22050;const float duration=.52f;int count=Mathf.RoundToInt(sampleRate*duration);
            var clip=AudioClip.Create("BattleStartStinger",count,1,sampleRate,false);var samples=new float[count];
            for(int i=0;i<count;i++)
            {
                float t=i/(float)sampleRate;
                float boom=Mathf.Sin(2*Mathf.PI*(74+18*t)*t)*Mathf.Exp(-5.2f*t)*.34f;
                float rise=Mathf.Sin(2*Mathf.PI*(185+120*t)*t)*Mathf.Exp(-4.4f*t)*.22f;
                float hit=t<.055f?Mathf.Sin(2*Mathf.PI*920*t)*Mathf.Exp(-55*t)*.16f:0;
                samples[i]=Mathf.Clamp(boom+rise+hit,-.9f,.9f);
            }
            clip.SetData(samples,0);return clip;
        }
        void PlayBattleStinger()
        {
            if(muted||battleStinger==null)return;
            effects.pitch=1;effects.PlayOneShot(battleStinger,.68f);
        }
        static AudioClip CreateLandingThud()
        {
            const int rate=22050;int count=(int)(rate*.62f);var samples=new float[count];
            var noise=new System.Random(260926);float gravel=0;
            for(int i=0;i<count;i++)
            {
                float t=i/(float)rate;gravel=Mathf.Lerp(gravel,(float)noise.NextDouble()*2-1,.16f);
                float low=Mathf.Sin(2*Mathf.PI*(62*t-20*t*t))*Mathf.Exp(-7*t)*.48f;
                samples[i]=(low+gravel*Mathf.Exp(-14*t)*.38f)*Mathf.Min(1,t/.006f);
            }
            var clip=AudioClip.Create("MonsterLandingThud",count,1,rate,false);clip.SetData(samples,0);return clip;
        }
        public void MonsterLanding()
        {
            if(muted||!landingThud)return;
            effects.pitch=1;effects.PlayOneShot(landingThud,.72f);
            if(Debug.isDebugBuild)Debug.Log("[VictoryStage] landing-thud playing=True");
        }
        public void UseLocalMusic(AudioClip clip)
        {
            battle.Stop();var previous=localMusic;localMusic=clip;
            battle.clip=clip?clip:Clip("Audio/music_battle");battle.loop=true;battle.Play();
            if(previous)Object.Destroy(previous);
            MusicEnabled=true;
        }
        public void Effect(string key,float gain=1)
        {
            if(muted)return;
            var clip=Clip("Audio/"+key);if(!clip)return;
            // Small deterministic pitch changes keep repeated punches and hits
            // from sounding machine-perfect while preserving the authored cue.
            int n=effectSequence++;
            effects.pitch=key=="swing"?(n%3==0?1.04f:n%3==1?.96f:1f):
                key=="impact"?(n%2==0?1.03f:.97f):
                key=="enemy_rush"?(n%2==0?.94f:1.02f):1f;
            effects.PlayOneShot(clip,gain);
        }
        public void Speak(string key,int importance,GamePhase expected)
        {
            if(muted) {NotifyInstruction(key,0);return;}
            var clip=Clip("Voice/"+key);if(!clip) {NotifyInstruction(key,0);return;}
            // Let the finisher cry complete even when its hit immediately wins the round.
            if(!voice.isPlaying || (!beamVoice && (importance>priority || importance>=5)))
            {
                voice.Stop();voice.clip=clip;priority=importance;beamVoice=key=="beam"||key=="beam_original";voice.Play();
                NotifyInstruction(key,clip.length);
                if(key=="victory")Effect("victory");
                if(Debug.isDebugBuild)Debug.Log($"[Voice] key={key} playing={voice.isPlaying} length={clip.length:F2}");
            }
            else
            {
                pending.RemoveAll(line=>line.Key==key);
                if(pending.Count<4) pending.Add(new Line { Key=key,Priority=importance,Expires=Time.unscaledTime+4,Phase=expected });
                if(Debug.isDebugBuild&&beamVoice)Debug.Log($"[Voice] deferred={key} until=beam-finished");
            }
        }
        void NotifyInstruction(string key,float seconds)
        {
            if(key=="warning"||key=="battle"||key=="energy"||key=="resume"||key=="tutorial"||key=="beam_help"||key=="beam_reset"||key=="arcade_final")
                InstructionStarted?.Invoke(key,seconds);
        }
        public void Cue(GameCue cue,GamePhase state)
        {
            switch(cue)
            {
                case GameCue.Transform:Effect("transform");Speak("transform",4,state);break;
                case GameCue.BattleStart:PlayBattleStinger();Speak("battle",3,state);break;
                case GameCue.Punch:Effect("swing",.7f);break;
                case GameCue.Warning:Effect("warning",.72f);Speak("warning",5,state);break;
                case GameCue.EnemyAttack:Effect("enemy_rush",.85f);break;
                case GameCue.Block:Effect("shield");Speak("block",2,state);break;
                case GameCue.Hurt:Effect("impact",.7f);break;
                case GameCue.HeroLanded:Effect("impact",.45f);Effect("recover");Speak("recover",2,state);break;
                case GameCue.EnergyReady:
                    pending.Clear();Effect("shield",.45f);Speak("energy",5,state);break;
                case GameCue.Beam:
                    pending.Clear();
                    Speak(HeroId=="Tiga"&&HasOriginalBeamVoice?"beam_original":"beam",5,state);break;
                case GameCue.Victory:effects.Stop();pending.Clear();Speak("victory",6,state);break;
                case GameCue.Resume:Speak("resume",3,state);break;
            }
        }
        public void Tick(GamePhase state,bool silence,float dt)
        {
            if(silence&&!muted) Reset();
            muted=silence;
            if(state==GamePhase.Paused && phase!=state) Reset();
            if(phase!=state) {phaseAge=0;phaseReported=false;}
            phase=state;phaseAge+=dt;
            pending.RemoveAll(line=>line.Expires<Time.unscaledTime || line.Phase!=state);
            if(!muted && !voice.isPlaying && pending.Count>0)
            { var line=pending[0];pending.RemoveAt(0);Speak(line.Key,line.Priority,state); }
            float master=muted?0:Mathf.Clamp01(Volume);
            voice.volume=master;effects.volume=master*.75f;
            bool active=localMusic || state==GamePhase.Battle || state==GamePhase.Transforming;
            float music=MusicEnabled?master*Mathf.Clamp01(MusicVolume)*(voice.isPlaying?.23f:.65f):0;
            if(state==GamePhase.Paused)music*=.35f;
            if(state==GamePhase.Victory)music*=.5f;
            float blend=1-Mathf.Exp(-dt*(voice.isPlaying?12:3));
            calm.volume=Mathf.Lerp(calm.volume,active?0:music,blend);
            battle.volume=Mathf.Lerp(battle.volume,active?music:0,blend);
            if(Debug.isDebugBuild&&!phaseReported&&phaseAge>1)
            {phaseReported=true;Debug.Log($"[AudioState] phase={state} {Diagnostics}");}
        }
        public void Reset() { voice.Stop();effects.Stop();effects.pitch=1;pending.Clear();priority=0;beamVoice=false; }
        public void Save()
        {
            PlayerPrefs.SetFloat("sound.master",Volume);PlayerPrefs.SetFloat("sound.music",MusicVolume);
            PlayerPrefs.SetInt("sound.musicEnabled",MusicEnabled?1:0);PlayerPrefs.Save();
        }
    }
}
