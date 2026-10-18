using UnityEngine;
using UnityEngine.Rendering;

namespace UltramanGame.Runtime
{
    // A stationary, soft environment for the PBR materials. This is captured
    // once, without actors or effects; the live battle is never re-rendered.
    [ExecuteAlways]
    public sealed class VolcanoEnvironment : MonoBehaviour
    {
        RenderTexture reflection;
        Texture previousReflection;
        DefaultReflectionMode previousMode;
        float previousIntensity;
        public RenderTexture Reflection=>reflection;
        public static VolcanoEnvironment Create(Transform parent)
        {
            var owner=new GameObject("Volcanic environment reflection").AddComponent<VolcanoEnvironment>();
            owner.transform.SetParent(parent,false);owner.Build();return owner;
        }
        void Build()
        {
            var shader=Resources.Load<Shader>("VolcanoReflection");
            if(!shader||!shader.isSupported){Debug.LogWarning("[VolcanoEnvironment] reflection shader unavailable");return;}
            var material=new Material(shader);
            var camera=new GameObject("Volcanic reflection capture").AddComponent<Camera>();camera.enabled=false;
            camera.clearFlags=CameraClearFlags.Skybox;camera.cullingMask=0;camera.allowHDR=true;
            camera.gameObject.AddComponent<Skybox>().material=material;
            reflection=new RenderTexture(128,128,16,RenderTextureFormat.ARGBHalf)
            {name="Volcanic sky and lava reflection",dimension=TextureDimension.Cube,useMipMap=true,autoGenerateMips=false,filterMode=FilterMode.Trilinear};
            reflection.Create();
            try
            {
                if(!camera.RenderToCubemap(reflection))
                {Debug.LogWarning("[VolcanoEnvironment] reflection capture failed");reflection.Release();Release(reflection);reflection=null;return;}
                reflection.GenerateMips();
                previousReflection=RenderSettings.customReflectionTexture;previousMode=RenderSettings.defaultReflectionMode;previousIntensity=RenderSettings.reflectionIntensity;
                RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.customReflectionTexture=reflection;RenderSettings.reflectionIntensity=1;
                Debug.Log("[VolcanoEnvironment] captured=True faces=6 size=128 mipmaps=True");
            }
            finally {Release(camera.gameObject);Release(material);}
        }
        void OnDestroy()
        {
            if(!reflection)return;
            if(RenderSettings.customReflectionTexture==reflection)
            {
                RenderSettings.customReflectionTexture=previousReflection;
                RenderSettings.defaultReflectionMode=previousReflection||previousMode!=DefaultReflectionMode.Custom?previousMode:DefaultReflectionMode.Skybox;
                RenderSettings.reflectionIntensity=previousIntensity;
            }
            reflection.Release();Release(reflection);reflection=null;
        }
        static void Release(Object value){if(!value)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
