using UnityEngine;

namespace UltramanGame.Runtime
{
    // Unity main-thread material updates. Reuse scratch storage; SetVectorArray
    // copies it to each material. Trigonometry runs twenty times per stage tick,
    // instead of inside every pixel's density and lighting ray samples.
    public static class VolcanicPlumeMotion
    {
        public const float Width=6.0f;
        const float CrossScale=3.8f/Width;
        const int Count=10;
        const float Spacing=.16f;
        static readonly Vector4[] Centers=new Vector4[Count];
        static readonly Vector4[] Radii=new Vector4[Count];
        static readonly int CentersId=Shader.PropertyToID("_ParcelCenters");
        static readonly int RadiiId=Shader.PropertyToID("_ParcelRadii");
        static readonly int FirstId=Shader.PropertyToID("_FirstHeight");
        static readonly int ClockId=Shader.PropertyToID("_Clock");
        static readonly int SeedId=Shader.PropertyToID("_Seed");
        static readonly int CrossId=Shader.PropertyToID("_CrossScale");

        public static void Update(Material material,float time)
        {
            float travel=time*.085f,seed=material.GetFloat(SeedId);
            int first=Mathf.FloorToInt(-travel/Spacing)-1;
            for(int i=0;i<Count;i++)
            {
                int id=first+i;
                float altitude=id*Spacing+travel,age=Mathf.Clamp01(altitude);
                float phase=id*2.39996f+seed*1.7f;
                float spread=.035f+.225f*Mathf.Pow(age,.68f),sway=.020f+.090f*age;
                // The hot throat rises vertically; older parcels drift with the
                // same prevailing wind at both vents. Widen only the container,
                // preserving the original world-space throat and billow width.
                float wind=.26f*age*age;
                Centers[i]=new Vector4(CrossScale*(-.12f+age*.19f+wind+Mathf.Sin(phase+age*4.1f)*sway),
                    altitude,Mathf.Cos(phase+age*3.2f)*sway,
                    Ease(-.14f,.04f,altitude)*(1-Ease(.77f,1.14f,altitude)));
                Radii[i]=new Vector4(1/(CrossScale*spread*(1+.27f*Mathf.Sin(phase))),
                    1/(.090f+.080f*age),1/(spread*.91f),0);
            }
            material.SetFloat(CrossId,CrossScale);material.SetFloat(ClockId,time);material.SetFloat(FirstId,first*Spacing+travel);
            material.SetVectorArray(CentersId,Centers);material.SetVectorArray(RadiiId,Radii);
        }
        static float Ease(float a,float b,float value)
        {float t=Mathf.InverseLerp(a,b,value);return t*t*(3-2*t);}
    }
}
