using UnityEngine;

namespace UltramanGame.Runtime
{
    public static class CombatImpactSounds
    {
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
