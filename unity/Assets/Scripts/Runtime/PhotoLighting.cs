using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UltramanGame.Runtime
{
    // The studio is far from the arena but directional lights still reach it.
    // Isolate only this synchronous camera render, then restore the live world.
    public sealed class PhotoLighting
    {
        readonly Light[] lamps;
        readonly List<Light> sceneLights=new List<Light>();
        readonly List<int> masks=new List<int>();
        public PhotoLighting(Transform parent)
        {
            lamps=new[]{
                Lamp(parent,"Photo moon key",new Color(.80f,.86f,1),1.05f,new Vector3(28,-32,0)),
                Lamp(parent,"Photo soft fill",new Color(.56f,.65f,.82f),.42f,new Vector3(15,38,0)),
                Lamp(parent,"Photo lava rim",new Color(1,.28f,.10f),.45f,new Vector3(18,155,0))};
            lamps[0].shadows=LightShadows.Soft;lamps[0].shadowStrength=.72f;
            lamps[0].shadowBias=.025f;lamps[0].shadowNormalBias=.06f;
        }
        static Light Lamp(Transform parent,string name,Color color,float intensity,Vector3 rotation)
        {
            var light=new GameObject(name).AddComponent<Light>();light.transform.SetParent(parent,false);
            light.type=LightType.Directional;light.color=color;light.intensity=intensity;light.transform.localEulerAngles=rotation;
            light.cullingMask=1<<31;light.shadows=LightShadows.None;light.enabled=false;return light;
        }
        public void Render(Camera camera)
        {
            var mode=RenderSettings.ambientMode;var sky=RenderSettings.ambientSkyColor;
            var equator=RenderSettings.ambientEquatorColor;var ground=RenderSettings.ambientGroundColor;
            bool fog=RenderSettings.fog;
            sceneLights.Clear();masks.Clear();
            try
            {
                foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                {
                    if(!light.enabled||(light.cullingMask&(1<<31))==0)continue;
                    sceneLights.Add(light);masks.Add(light.cullingMask);light.cullingMask&=~(1<<31);
                }
                foreach(var lamp in lamps)lamp.enabled=true;
                RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Trilight;
                RenderSettings.ambientSkyColor=new Color(.27f,.30f,.36f);
                RenderSettings.ambientEquatorColor=new Color(.21f,.23f,.28f);
                RenderSettings.ambientGroundColor=new Color(.10f,.10f,.12f);
                camera.Render();
            }
            finally
            {
                foreach(var lamp in lamps)if(lamp)lamp.enabled=false;
                for(int i=0;i<sceneLights.Count;i++)if(sceneLights[i])sceneLights[i].cullingMask=masks[i];
                RenderSettings.ambientSkyColor=sky;RenderSettings.ambientEquatorColor=equator;
                RenderSettings.ambientGroundColor=ground;RenderSettings.ambientMode=mode;RenderSettings.fog=fog;
                sceneLights.Clear();masks.Clear();
            }
        }
    }
}
