using UnityEngine;

namespace UltramanGame.Runtime
{
    public static class CombatImpactSounds
    {
        // Original non-vocal plasma discharge: a prompt pressure snap, a
        // short resonant core and air shedding behind it. Contact owns the
        // heavier low end; the launch must remain legible during rapid casts.
        public static AudioClip CreateProjectileLaunch(int variation=0)
        {
            const int rate=44100;const float duration=.28f;
            var dry=new float[Mathf.RoundToInt(rate*duration)];var data=new float[dry.Length];
            var random=new System.Random(261019186+variation*71);
            float fast=0,slow=0,phase=0,previous=0,dc=0,peak=0;
            for(int i=0;i<dry.Length;i++)
            {
                float t=i/(float)rate,noise=(float)random.NextDouble()*2-1;
                fast=Mathf.Lerp(fast,noise,.52f);slow=Mathf.Lerp(slow,noise,.065f);
                float air=fast-slow;
                float pressure=Mathf.Sin(2*Mathf.PI*((165+variation*3)*t+95*.012f*(1-Mathf.Exp(-t/.012f))))*.30f*Mathf.Exp(-t/.032f);
                phase+=2*Mathf.PI*(620+variation*13+1200*Mathf.Exp(-t/.032f))/rate;
                float core=(Mathf.Sin(phase)+.18f*Mathf.Sin(phase*1.47f))*.16f*Mathf.Exp(-t/.045f);
                float snap=air*.78f*Mathf.Exp(-t/.008f);
                float trail=air*.38f*Mathf.Exp(-t/.063f)*(1-Mathf.Exp(-t/.004f));
                float value=(pressure+core+snap+trail)*Mathf.Clamp01(t/.0015f);
                dc=.99886f*(dc+value-previous);previous=value;dry[i]=dc;
            }
            for(int i=0;i<data.Length;i++)
            {
                int echo=i-(int)(rate*.028f);
                float value=(dry[i]+(echo>=0?dry[echo]*.10f:0))*Mathf.Clamp01((data.Length-1-i)/(rate*.035f));
                data[i]=value;peak=Mathf.Max(peak,Mathf.Abs(value));
            }
            for(int i=0;i<data.Length;i++)data[i]*=.56f/Mathf.Max(.001f,peak);
            var clip=AudioClip.Create("TigaDischarge"+variation,data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }

        // Original non-vocal claw foley. The event starts with the attack;
        // its air crest follows the accelerating hand, just before contact.
        public static AudioClip CreateClawSweep()
        {
            const int rate=44100;const float duration=.64f;
            var samples=new float[Mathf.RoundToInt(rate*duration)];var random=new System.Random(261019177);
            float fast=0,slow=0,phase=0,peak=0;
            for(int i=0;i<samples.Length;i++)
            {
                float age=i/(float)rate,t=Mathf.Max(0,age-.08f),noise=(float)random.NextDouble()*2-1;
                float rush=Mathf.SmoothStep(0,1,t/.23f)*(1-Mathf.SmoothStep(0,1,(t-.23f)/.28f));
                float cutoff=Mathf.Lerp(.045f,.42f,Mathf.Sin(Mathf.Clamp01(t/.51f)*Mathf.PI));
                fast=Mathf.Lerp(fast,noise,cutoff);slow=Mathf.Lerp(slow,noise,.018f);
                phase+=2*Mathf.PI*Mathf.Lerp(125,63,Mathf.Clamp01(t/.51f))/rate;
                float cloth=(fast-slow)*.76f+slow*.24f;
                float body=Mathf.Sin(phase)*.12f*(.78f+.22f*Mathf.Sin(t*73));
                float value=(cloth+body)*rush*Mathf.Clamp01((duration-age)/.025f);
                samples[i]=value;peak=Mathf.Max(peak,Mathf.Abs(value));
            }
            for(int i=0;i<samples.Length;i++)samples[i]*=.62f/Mathf.Max(.001f,peak);
            var clip=AudioClip.Create("Golza claw sweep",samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
        // A brief plasma crack for individual bullets. Keep the finisher's
        // long, low impact separate so fast casts have a readable rhythm.
        public static AudioClip CreateProjectile(int variation=0)
        {
            const int rate=44100;const float duration=.36f;
            var data=new float[Mathf.RoundToInt(rate*duration)];
            var random=new System.Random(261011+variation*71);
            float fast=0,slow=0,phase=0,previous=0,dc=0,peak=0;
            for(int i=0;i<data.Length;i++)
            {
                float t=i/(float)rate,noise=(float)random.NextDouble()*2-1;
                fast=Mathf.Lerp(fast,noise,.62f);slow=Mathf.Lerp(slow,noise,.075f);
                float bodyPhase=2*Mathf.PI*(88*t+110*.018f*(1-Mathf.Exp(-t/.018f)));
                float body=Mathf.Sin(bodyPhase)*Mathf.Exp(-t/.065f)*.48f;
                phase+=2*Mathf.PI*(520+(1000+variation*45)*Mathf.Exp(-t/.024f))/rate;
                float plasma=Mathf.Sin(phase)*Mathf.Exp(-t/.035f)*.14f;
                float crack=(fast-slow)*Mathf.Exp(-t/.014f)*.78f;
                float sparks=(fast-slow)*Mathf.Exp(-t/.060f)*.16f;
                float value=(body+plasma+crack+sparks)*Mathf.Min(1,t/.0015f)*Mathf.Clamp01((duration-t)/.025f);
                dc=.99886f*(dc+value-previous);previous=value;
                data[i]=dc*Mathf.Clamp01((data.Length-1-i)/(rate*.02f));peak=Mathf.Max(peak,Mathf.Abs(data[i]));
            }
            for(int i=0;i<data.Length;i++)data[i]*=.76f/Mathf.Max(.001f,peak);
            var clip=AudioClip.Create("ProjectileContact"+variation,data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
        // Original layered foley synthesis: contact crack, low body weight,
        // rough surface rattle and short reflections. No voice or song samples.
        public static AudioClip Create(int tier,int variation=0)
        {
            const int rate=44100;
            float duration=tier==0?.40f:tier==1?.82f:1.20f;
            var dry=new float[Mathf.RoundToInt(rate*duration)];var output=new float[dry.Length];
            var random=new System.Random(260930+tier*71+variation*17);
            float fast=0,slow=0,previous=0,dc=0;
            float bottom=tier==0?58:tier==1?38:46,drop=tier==0?65:tier==1?48:110;
            float decay=tier==0?.09f:tier==1?.19f:.27f;
            for(int i=0;i<dry.Length;i++)
            {
                float t=i/(float)rate,noise=(float)random.NextDouble()*2-1;
                fast=Mathf.Lerp(fast,noise,.55f);slow=Mathf.Lerp(slow,noise,.06f);
                float phase=2*Mathf.PI*((bottom+variation*2)*t+drop*.028f*(1-Mathf.Exp(-t/.028f)));
                float body=Mathf.Sin(phase)*Mathf.Exp(-t/decay)*.48f;
                float crack=(fast-slow)*Mathf.Exp(-t/(tier==0?.022f:.034f))*.80f;
                float rattle=slow*Mathf.Exp(-t/(decay*1.5f))*.42f;
                float shell=(Mathf.Sin(2*Mathf.PI*(367+variation*19)*t)+Mathf.Sin(2*Mathf.PI*613*t))*.045f*Mathf.Exp(-t/.045f);
                float value=body+crack+rattle+shell;
                if(tier>0)
                {
                    for(int tap=0;tap<2;tap++)
                    {float age=t-(.055f+tap*.07f);if(age>0)value+=(fast*.23f+Mathf.Sin(2*Mathf.PI*79*age)*.12f)*Mathf.Exp(-age/.045f)*Mathf.Min(1,age/.003f);}
                }
                if(tier==2)value+=fast*.20f*Mathf.Exp(-t/.38f)*Mathf.SmoothStep(0,1,t/.08f);
                value*=Mathf.Min(1,t/.002f)*Mathf.Clamp01((duration-t)/.035f);
                // Remove subsonic/DC drift without flattening the attack.
                dc=.99886f*(dc+value-previous);previous=value;dry[i]=dc;
            }
            float peak=0;
            for(int i=0;i<dry.Length;i++)
            {
                float value=dry[i];
                int a=i-(int)(rate*.043f),b=i-(int)(rate*.091f);
                if(a>=0)value+=dry[a]*.10f;if(b>=0)value+=dry[b]*.055f;
                output[i]=value*Mathf.Clamp01((dry.Length-1-i)/(rate*.02f));peak=Mathf.Max(peak,Mathf.Abs(output[i]));
            }
            float target=tier==0?.68f:tier==1?.80f:.76f;
            for(int i=0;i<output.Length;i++)output[i]*=target/Mathf.Max(.001f,peak);
            var clip=AudioClip.Create((tier==0?"FistContact":tier==1?"HeavyContact":"BeamContact")+variation,output.Length,1,rate,false);
            clip.SetData(output,0);return clip;
        }
    }
}
