using UnityEngine;

namespace UltramanGame.Runtime
{
    // These source models pack their luminous lenses into the body atlas,
    // instead of exposing separate eye/timer materials. Regions refer to the
    // original 256px layout and remain valid if that layout is later upscaled.
    public static class HeroAtlasLights
    {
        public static bool Configure(Material material,string hero)
        {
            if(!material.HasProperty("_AtlasLights"))return false;
            if(hero=="Grigio"&&material.name=="Grigio_Tex_geliqao")
            {
                material.SetVector("_EyeRegion",Region(116,112,153,150));
                material.SetVector("_CoreRegion",Region(62,219,93,241));
                material.SetFloat("_WarmEyes",1);
            }
            else if(hero=="Geed"&&material.name=="Geed_Tex_dandittruth")
            {
                material.SetVector("_EyeRegion",Region(138,193,176,226));
                material.SetVector("_CoreRegion",Region(91,78,111,139));
                material.SetFloat("_WarmEyes",0);
            }
            else return false;
            material.SetFloat("_AtlasLights",1);
            Debug.Log("[HeroAtlasLights] hero="+hero+" originalAtlas=eyes+core");
            return true;
        }
        static Vector4 Region(float left,float top,float right,float bottom)
            =>new Vector4(left/256,(256-bottom)/256,right/256,(256-top)/256);
        public static void SetCharge(Material material,float charge)
        {
            material.SetFloat("_EyeRadiance",1.1f+Mathf.Clamp01(charge)*.35f);
            material.SetFloat("_CoreRadiance",.65f+Mathf.Max(0,charge)*1.6f);
        }
    }
}
