using System.Collections.Generic;
using UnityEngine;
using UltramanGame.Core;

namespace UltramanGame.Runtime
{
    public sealed class GameAudio
    {
        readonly AudioSource calm,battle,voice,charge,monsterRay;
        readonly CombatAudioVoices effects,debris;
        readonly AudioClip[] fistContacts=new AudioClip[3];
        readonly AudioClip heavyContact,beamContact;
        AudioClip localMusic;
        readonly AudioClip battleStinger,landingThud,groundCrunch,dissolveShimmer,arrivalRoar,defeatSurge;
        readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
        struct Line { public string Key;public int Priority;public float Expires;public GamePhase Phase; }
        readonly List<Line> pending=new List<Line>();
        int priority;
        bool muted,beamVoice;
        GamePhase phase;
        float phaseAge;
        bool phaseReported;
        int effectSequence,contactSequence;
        float effectsDuck=1;
        public bool VoicePlaying=>voice.isPlaying;
        public bool HasVoice(string key)=>clips.TryGetValue("Voice/"+key,out var clip)&&clip;
        public float VoiceLength(string key) {var clip=Clip("Voice/"+key);return clip?clip.length:0;}
        public bool MusicEnabled=true;
        public float Volume=.75f,MusicVolume=.45f;
        public event System.Action<string,float> InstructionStarted;
        public string HeroId="Tiga";
        public string MusicSource {get;private set;}="内置原创战斗循环（未包含《奇迹再现》）";
        public bool ProjectMusicLoaded {get;private set;}
        public bool HasOriginalBeamVoice => clips.TryGetValue("Voice/beam_original",out var original) && original!=null;
        public string RequestedBeamVoiceKey {get;private set;}="beam";
        public string ResolvedBeamVoiceKey {get;private set;}="beam";
        public bool BeamVoiceExact {get;private set;}
        public string BeamVoiceSource => BeamVoiceExact?"hero-specific/original":ResolvedBeamVoiceKey=="missing"?"missing":"neutral fallback";
        public string Diagnostics => $"calmPlaying={calm.isPlaying} battlePlaying={battle.isPlaying} voicePlaying={voice.isPlaying} musicProject={ProjectMusicLoaded} musicSource={MusicSource} beamOriginal={HasOriginalBeamVoice} beamVoice={ResolvedBeamVoiceKey} beamVoiceSource={BeamVoiceSource} battleStinger={battleStinger!=null} calmVolume={calm.volume:F3} battleVolume={battle.volume:F3} localMusic={localMusic!=null} effectsPitch={effects.LastPitch:F2} effectVoices={effects.ActiveCount}/{effects.Capacity} effectDuck={effectsDuck:F2} muted={muted}";
        AudioSource Source(GameObject owner)
        { var s=owner.AddComponent<AudioSource>();s.playOnAwake=false;s.spatialBlend=0;s.dopplerLevel=0;return s; }
        public GameAudio(GameObject owner)
        {
            calm=Source(owner);battle=Source(owner);voice=Source(owner);
            effects=new CombatAudioVoices(owner,10);debris=new CombatAudioVoices(owner,4);
            for(int i=0;i<fistContacts.Length;i++)fistContacts[i]=RuntimeResources.Own(owner.transform,CombatImpactSounds.Create(0,i));
            heavyContact=RuntimeResources.Own(owner.transform,CombatImpactSounds.Create(1));
            beamContact=RuntimeResources.Own(owner.transform,CombatImpactSounds.Create(2));
            charge=Source(owner);charge.loop=true;charge.volume=0;
            charge.clip=RuntimeResources.Own(owner.transform,CreateBeamGather());
            monsterRay=Source(owner);monsterRay.loop=true;monsterRay.volume=0;
            monsterRay.clip=RuntimeResources.Own(owner.transform,CreateMonsterRay());
            battleStinger=RuntimeResources.Own(owner.transform,CreateBattleStinger());
            landingThud=RuntimeResources.Own(owner.transform,CreateLandingThud());
            defeatSurge=RuntimeResources.Own(owner.transform,CreateDefeatSurge());
            groundCrunch=RuntimeResources.Own(owner.transform,CreateGroundCrunch());
            dissolveShimmer=RuntimeResources.Own(owner.transform,CreateDissolveShimmer());
            arrivalRoar=RuntimeResources.Own(owner.transform,CreateArrivalRoar());
            // Load once at startup so a first punch/voice line does not perform resource I/O mid-fight.
            foreach(var clip in Resources.LoadAll<AudioClip>("Audio"))clips["Audio/"+clip.name]=clip;
            foreach(var clip in Resources.LoadAll<AudioClip>("Voice"))clips["Voice/"+clip.name]=clip;
            calm.clip=Clip("Audio/music_ready");battle.clip=Clip("Audio/music_battle");
            var projectMusic=Clip("Audio/miracle_reappearance");
            if(projectMusic)
            {
                calm.clip=projectMusic;battle.clip=projectMusic;ProjectMusicLoaded=true;
                MusicSource="项目内导入《奇迹再现》";
            }
            else MusicSource="内置原创战斗循环（未包含《奇迹再现》）";
            calm.loop=battle.loop=true;calm.volume=battle.volume=0;calm.Play();battle.Play();
            Volume=PlayerPrefs.GetFloat("sound.master",.75f);MusicVolume=PlayerPrefs.GetFloat("sound.music",.45f);
            MusicEnabled=PlayerPrefs.GetInt("sound.musicEnabled",1)==1;
            ApplyEffectsMix();
            if(Debug.isDebugBuild) Debug.Log($"[Audio] musicReady={calm.clip!=null} musicBattle={battle.clip!=null} source={MusicSource} voice={Clip("Voice/welcome")!=null}");
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
            effects.Play(battleStinger,.68f);
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
            effects.Play(landingThud,.72f);
            debris.Play(defeatSurge,.44f);
            if(Debug.isDebugBuild)Debug.Log("[VictoryStage] landing-thud playing=True");
            if(Debug.isDebugBuild)Debug.Log("[DefeatImpact] sound=True");
        }
        public static AudioClip CreateDefeatSurge()
        {
            const int rate=22050;var samples=new float[(int)(rate*1.6f)];
            var random=new System.Random(260930);float air=0,rumble=0;
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)rate,n=(float)random.NextDouble()*2-1;
                air=Mathf.Lerp(air,n,.20f);rumble=Mathf.Lerp(rumble,n,.013f);
                float envelope=Mathf.SmoothStep(0,1,t/.025f)*Mathf.Exp(-t*2.8f)*(1-Mathf.SmoothStep(0,1,(t-1.25f)/.35f));
                float body=Mathf.Sin(2*Mathf.PI*(48*t-7*t*t))*Mathf.Exp(-t*5)*.15f;
                samples[i]=(air*.38f+rumble*.9f+body)*envelope;
            }
            var clip=AudioClip.Create("DefeatDustSurge",samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
        public static AudioClip CreateDissolveShimmer()
        {
            const int rate=22050;var samples=new float[(int)(rate*1.5f)];
            var random=new System.Random(260929);float air=0;
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)rate;
                air=Mathf.Lerp(air,(float)random.NextDouble()*2-1,.08f);
                float envelope=Mathf.SmoothStep(0,1,t/.14f)*(1-Mathf.SmoothStep(0,1,(t-.35f)/1.15f));
                float tone=Mathf.Sin(2*Mathf.PI*(620*t+120*t*t))*.11f+Mathf.Sin(2*Mathf.PI*(930*t+180*t*t))*.045f;
                samples[i]=(air*.20f+tone)*envelope;
            }
            var clip=AudioClip.Create("MonsterDepartureShimmer",samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
        public void MonsterDeparture()
        {
            if(muted||!dissolveShimmer)return;
            effects.Play(dissolveShimmer,.48f);
            if(Debug.isDebugBuild)Debug.Log("[MonsterDissolve] shimmer playing=True");
        }
        public static AudioClip CreateGroundCrunch()
        {
            const int rate=22050;var samples=new float[(int)(rate*.9f)];var random=new System.Random(260928);
            var starts=new float[18];var pitches=new float[18];
            for(int i=0;i<starts.Length;i++){starts[i]=.32f+(float)random.NextDouble()*.48f;pitches[i]=250+(float)random.NextDouble()*800;}
            float grit=0;
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)rate;grit=Mathf.Lerp(grit,(float)random.NextDouble()*2-1,.58f);
                float value=grit*Mathf.Exp(-16*t)*.54f;
                for(int chip=0;chip<starts.Length;chip++)
                {
                    float age=t-starts[chip];if(age<0||age>.08f)continue;
                    value+=(grit*.30f+Mathf.Sin(age*pitches[chip]*Mathf.PI*2)*.08f)*Mathf.Exp(-85*age)*Mathf.Min(1,age/.002f);
                }
                samples[i]=Mathf.Clamp(value*Mathf.Min(1,t/.004f),-.85f,.85f);
            }
            var clip=AudioClip.Create("GroundFragments",samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
        public void GroundContact(bool rush)
        {
            if(muted||!groundCrunch)return;
            debris.Play(groundCrunch,.48f);
            if(rush)debris.Play(landingThud,.34f);
            if(Debug.isDebugBuild)Debug.Log($"[GroundImpact] sound=True rush={rush}");
        }
        public static AudioClip CreateArrivalRoar()
        {
            const int rate=22050;var samples=new float[(int)(rate*.88f)];var random=new System.Random(260930);float breath=0,phase=0;
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)rate,u=t/.88f;
                phase+=2*Mathf.PI*Mathf.Lerp(73,48,u)/rate;
                breath=Mathf.Lerp(breath,(float)random.NextDouble()*2-1,.10f);
                float voice=Mathf.Sin(phase)*.23f+Mathf.Sin(phase*2.01f)*.12f+Mathf.Sin(phase*3.02f)*.065f;
                float envelope=Mathf.SmoothStep(0,1,t/.10f)*(1-Mathf.SmoothStep(0,1,(t-.46f)/.42f));
                samples[i]=(voice*(.82f+.18f*Mathf.Sin(t*2*Mathf.PI*17))+breath*.4f)*envelope;
            }
            var clip=AudioClip.Create("MonsterArrivalRoar",samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
        public void MonsterArrival()
        {
            if(muted||!arrivalRoar)return;
            effects.Play(arrivalRoar,.70f,1,1);
            if(Debug.isDebugBuild)Debug.Log("[MonsterEntrance] roar sound=True");
        }
        public void MonsterRecoveryStep()
        {
            if(muted||!landingThud)return;
            effects.Play(landingThud,.25f,1,0);
            if(Debug.isDebugBuild)Debug.Log("[MonsterStagger] footstep playing=True");
        }
        public static AudioClip CreateBeamGather()
        {
            // Every frequency completes an integer number of cycles in this
            // half-second loop. The live charge power supplies the rise, so
            // pauses and interrupted cinematics cannot leave a timed tail.
            const int rate=22050;var samples=new float[rate/2];
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)rate;
                float hum=Mathf.Sin(2*Mathf.PI*84*t)*.18f+Mathf.Sin(2*Mathf.PI*168*t)*.065f;
                float air=0;for(int n=1;n<=12;n++)air+=Mathf.Sin(2*Mathf.PI*(270+36*n)*t+n*1.37f)*(.095f/Mathf.Sqrt(n));
                samples[i]=(hum+air)*(.88f+.12f*Mathf.Sin(2*Mathf.PI*10*t));
            }
            var clip=AudioClip.Create("BeamGatherHum",samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
        public void SetChargePower(float power)
        {
            power=Mathf.Clamp01(power);
            if(muted||phase!=GamePhase.Battle||Volume<=0||power<.001f)
            {
                if(charge.isPlaying){charge.Stop();if(Debug.isDebugBuild)Debug.Log("[BeamChargeAudio] stopped");}
                charge.volume=0;return;
            }
            // Keep the chosen original battle cry in front of the sound bed.
            charge.volume=Mathf.Clamp01(Volume)*(voice.isPlaying?.10f:.18f)*power;
            charge.pitch=.82f+power*.68f;
            if(!charge.isPlaying){charge.Play();if(Debug.isDebugBuild)Debug.Log("[BeamChargeAudio] started; follows presentation power");}
        }
        public static AudioClip CreateMonsterRay()
        {
            const int rate=22050;var samples=new float[rate/2];
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)rate,phase=2*Mathf.PI*t;
                float tone=Mathf.Sin(phase*116)*.18f+Mathf.Sin(phase*232)*.08f;
                for(int n=1;n<=9;n++)tone+=Mathf.Sin(phase*(410+n*62)+n*.83f)*(.10f/Mathf.Sqrt(n));
                samples[i]=tone*(.65f+.35f*Mathf.Pow(Mathf.Sin(phase*16),2));
            }
            var clip=AudioClip.Create("GolzaUltrasonicRay",samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
        public void SetMonsterRayPower(float power)
        {
            power=Mathf.Clamp01(power);
            if(muted||phase!=GamePhase.Battle||Volume<=0||power<.001f)
            {
                if(monsterRay.isPlaying){monsterRay.Stop();if(Debug.isDebugBuild)Debug.Log("[MonsterRayAudio] stopped");}
                monsterRay.volume=0;return;
            }
            monsterRay.volume=Mathf.Clamp01(Volume)*(voice.isPlaying?.12f:.22f)*power;
            monsterRay.pitch=.88f+.16f*power;
            if(!monsterRay.isPlaying){monsterRay.Play();if(Debug.isDebugBuild)Debug.Log("[MonsterRayAudio] started");}
        }
        public void UseLocalMusic(AudioClip clip,string sourceName=null)
        {
            calm.Stop();battle.Stop();var previous=localMusic;localMusic=clip;
            if(clip)
            {
                calm.clip=clip;battle.clip=clip;ProjectMusicLoaded=false;
            }
            else
            {
                calm.clip=Clip("Audio/music_ready");battle.clip=Clip("Audio/music_battle");ProjectMusicLoaded=false;
            }
            calm.loop=battle.loop=true;calm.Play();battle.Play();
            if(previous)Object.Destroy(previous);
            MusicSource=clip?(sourceName??clip.name):"内置原创战斗循环（未包含《奇迹再现》）";
            if(Debug.isDebugBuild)Debug.Log($"[Audio] music source={MusicSource} local={(clip!=null)}");
            MusicEnabled=true;
        }
        public void Effect(string key,float gain=1)
        {
            if(muted)return;
            var clip=key=="impact"?fistContacts[contactSequence++%fistContacts.Length]:Clip("Audio/"+key);if(!clip)return;
            // Small deterministic pitch changes keep repeated punches and hits
            // from sounding machine-perfect while preserving the authored cue.
            int n=effectSequence++;
            float pitch=key=="swing"?(n%3==0?1.04f:n%3==1?.96f:1f):
                key=="impact"?(n%2==0?1.03f:.97f):
                key=="enemy_rush"?(n%2==0?.94f:1.02f):1f;
            effects.Play(clip,gain,pitch,key=="beam"?3:1);
        }
        public void Hit(bool heavy,bool beam)
        {
            if(muted)return;
            var clip=beam?beamContact:heavy?heavyContact:fistContacts[contactSequence++%fistContacts.Length];
            effects.Play(clip,heavy?.90f:.80f,1,beam?3:heavy?2:1);
            if(heavy&&!beam)effects.Play(Clip("Audio/combo"),.36f,1,2);
            if(Debug.isDebugBuild)Debug.Log($"[CombatAudio] contact={(beam?"beam":heavy?"heavy":"fist")}");
        }
        void ApplyEffectsMix()
        {
            float master=muted?0:Mathf.Clamp01(Volume);
            effects.SetVolume(master*.75f*effectsDuck);
            debris.SetVolume(master*.60f*Mathf.Lerp(.32f,1,Mathf.InverseLerp(.42f,1,effectsDuck)));
        }
        public void Speak(string key,int importance,GamePhase expected)
        {
            if(muted) {NotifyInstruction(key,0);return;}
            var clip=Clip("Voice/"+key);if(!clip) {NotifyInstruction(key,0);return;}
            // Let the finisher cry complete even when its hit immediately wins the round.
            if(!voice.isPlaying || (!beamVoice && (importance>priority || importance>=5)))
            {
                voice.Stop();voice.clip=clip;priority=importance;beamVoice=key.StartsWith("beam",System.StringComparison.Ordinal);voice.Play();
                // Apply before this frame's impact starts, not one render frame later.
                voice.volume=Mathf.Clamp01(Volume);effectsDuck=.42f;ApplyEffectsMix();
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
        public string ResolveBeamVoiceKeyForHero(string heroId,out bool exact)
        {
            string requested="beam";
            for(int i=0;i<HeroRoster.Count;i++)
                if(HeroRoster.At(i).Id==heroId){requested=HeroRoster.At(i).BeamVoiceKey;break;}
            exact=HasVoice(requested);
            return exact?requested:(HasVoice("beam")?"beam":null);
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
                    bool exact;
                    string beamKey=ResolveBeamVoiceKeyForHero(HeroId,out exact);
                    RequestedBeamVoiceKey="beam";
                    for(int i=0;i<HeroRoster.Count;i++)
                        if(HeroRoster.At(i).Id==HeroId){RequestedBeamVoiceKey=HeroRoster.At(i).BeamVoiceKey;break;}
                    ResolvedBeamVoiceKey=beamKey??"missing";BeamVoiceExact=exact;
                    // Only use a clip when it has actually been imported for
                    // that hero. Missing licensed/original recordings fall
                    // back to the neutral beam cue instead of pretending that
                    // a generic voice is the hero's source audio.
                    if(Debug.isDebugBuild)Debug.Log($"[BeamVoice] hero={HeroId} requested={RequestedBeamVoiceKey} resolved={ResolvedBeamVoiceKey} exact={BeamVoiceExact} source={BeamVoiceSource}");
                    if(string.IsNullOrEmpty(beamKey))break;
                    Speak(beamKey,5,state);break;
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
            voice.volume=master;
            effectsDuck=voice.isPlaying?.42f:Mathf.Lerp(effectsDuck,1,1-Mathf.Exp(-dt/.18f));
            ApplyEffectsMix();
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
        public void Reset() { voice.Stop();effects.Stop();debris.Stop();charge.Stop();monsterRay.Stop();charge.volume=monsterRay.volume=0;effectsDuck=1;ApplyEffectsMix();pending.Clear();priority=0;beamVoice=false; }
        public void Save()
        {
            PlayerPrefs.SetFloat("sound.master",Volume);PlayerPrefs.SetFloat("sound.music",MusicVolume);
            PlayerPrefs.SetInt("sound.musicEnabled",MusicEnabled?1:0);PlayerPrefs.Save();
        }
    }
}
