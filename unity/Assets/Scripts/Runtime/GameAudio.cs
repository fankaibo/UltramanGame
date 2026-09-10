using System.Collections.Generic;
using UnityEngine;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    public sealed class GameAudio
    {
        readonly AudioSource calm,battle,voice,effects;
        AudioClip localMusic;
        readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
        struct Line { public string Key;public int Priority;public float Expires;public GamePhase Phase; }
        readonly List<Line> pending=new List<Line>();
        int priority;
        bool muted,beamVoice;
        GamePhase phase;
        float phaseAge;
        bool phaseReported;
        public bool VoicePlaying=>voice.isPlaying;
        public float VoiceLength(string key) {var clip=Clip("Voice/"+key);return clip?clip.length:0;}
        public bool MusicEnabled=true;
        public float Volume=.75f,MusicVolume=.45f;
        public event System.Action<string,float> InstructionStarted;
        public bool HasOriginalBeamVoice => clips.TryGetValue("Voice/beam_original",out var original) && original!=null;
        public string Diagnostics => $"calmPlaying={calm.isPlaying} battlePlaying={battle.isPlaying} voicePlaying={voice.isPlaying} beamOriginal={HasOriginalBeamVoice} calmVolume={calm.volume:F3} battleVolume={battle.volume:F3} localMusic={localMusic!=null} muted={muted}";
        AudioSource Source(GameObject owner)
        { var s=owner.AddComponent<AudioSource>();s.playOnAwake=false;s.spatialBlend=0;s.dopplerLevel=0;return s; }
        public GameAudio(GameObject owner)
        {
            calm=Source(owner);battle=Source(owner);voice=Source(owner);effects=Source(owner);
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
        public void UseLocalMusic(AudioClip clip)
        {
            battle.Stop();var previous=localMusic;localMusic=clip;
            battle.clip=clip?clip:Clip("Audio/music_battle");battle.loop=true;battle.Play();
            if(previous)Object.Destroy(previous);
            MusicEnabled=true;
        }
        public void Effect(string key,float gain=1)
        { if(!muted) { var clip=Clip("Audio/"+key);if(clip)effects.PlayOneShot(clip,gain); } }
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
            if(key=="warning"||key=="battle"||key=="energy"||key=="resume"||key=="tutorial"||key=="beam_help"||key=="arcade_final")
                InstructionStarted?.Invoke(key,seconds);
        }
        public void Cue(GameCue cue,GamePhase state)
        {
            switch(cue)
            {
                case GameCue.Transform:Effect("transform");Speak("transform",4,state);break;
                case GameCue.BattleStart:Speak("battle",3,state);break;
                case GameCue.Punch:Effect("swing",.7f);break;
                case GameCue.Warning:Speak("warning",5,state);break;
                case GameCue.EnemyAttack:Effect("enemy_rush",.85f);break;
                case GameCue.Block:Effect("shield");Speak("block",2,state);break;
                case GameCue.Hurt:Effect("impact",.7f);Effect("recover");Speak("recover",2,state);break;
                case GameCue.EnergyReady:
                    pending.Clear();Effect("shield",.45f);Speak("energy",5,state);break;
                case GameCue.Beam:
                    pending.Clear();
                    Speak(HasOriginalBeamVoice?"beam_original":"beam",5,state);break;
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
        public void Reset() { voice.Stop();effects.Stop();pending.Clear();priority=0;beamVoice=false; }
        public void Save()
        {
            PlayerPrefs.SetFloat("sound.master",Volume);PlayerPrefs.SetFloat("sound.music",MusicVolume);
            PlayerPrefs.SetInt("sound.musicEnabled",MusicEnabled?1:0);PlayerPrefs.Save();
        }
    }
}
