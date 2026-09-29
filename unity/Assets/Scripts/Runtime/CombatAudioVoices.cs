using UnityEngine;

namespace UltramanGame.Runtime
{
    // One clip per source: changing the next punch's pitch cannot retune a beam
    // or an impact tail already playing. Sources are allocated only at startup.
    public sealed class CombatAudioVoices
    {
        readonly AudioSource[] sources;
        readonly float[] gains;
        readonly int[] priorities;
        readonly long[] order;
        long sequence;
        float volume;
        public float LastPitch {get;private set;}=1;
        public int Capacity=>sources.Length;
        public int ActiveCount {get {int n=0;foreach(var s in sources)if(s.isPlaying)n++;return n;}}
        public CombatAudioVoices(GameObject owner,int capacity)
        {
            sources=new AudioSource[capacity];gains=new float[capacity];priorities=new int[capacity];order=new long[capacity];
            for(int i=0;i<capacity;i++)
            {
                var s=owner.AddComponent<AudioSource>();s.playOnAwake=false;s.spatialBlend=0;s.dopplerLevel=0;s.volume=0;
                sources[i]=s;
            }
        }
        public void SetVolume(float value)
        {volume=Mathf.Clamp01(value);for(int i=0;i<sources.Length;i++)sources[i].volume=volume*gains[i];}
        public bool Play(AudioClip clip,float gain=1,float pitch=1,int priority=1)
        {
            if(!clip)return false;
            int slot=-1;
            for(int i=0;i<sources.Length;i++)if(!sources[i].isPlaying){slot=i;break;}
            if(slot<0)
            {
                for(int i=0;i<sources.Length;i++)
                    if(priorities[i]<=priority&&(slot<0||priorities[i]<priorities[slot]||priorities[i]==priorities[slot]&&order[i]<order[slot]))slot=i;
                if(slot<0)return false;
            }
            var s=sources[slot];s.Stop();s.clip=clip;gains[slot]=Mathf.Clamp01(gain);priorities[slot]=priority;order[slot]=++sequence;
            s.pitch=Mathf.Clamp(pitch,.5f,2);s.volume=volume*gains[slot];s.priority=priority>=3?64:128;s.Play();LastPitch=s.pitch;
            return true;
        }
        public void Stop()
        {foreach(var s in sources){s.Stop();s.clip=null;}LastPitch=1;}
    }
}
