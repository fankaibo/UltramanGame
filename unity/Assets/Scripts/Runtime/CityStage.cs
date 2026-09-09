using UnityEngine;
using UnityEngine.Rendering;

namespace UltramanGame.Runtime
{
    // A physical foreground beneath the existing city skyline. No circular podium.
    public sealed class CityStage : MonoBehaviour
    {
        Material ground,metal,edgeLight,sky;
        Cubemap reflections;
        Material previousSky;
        Texture previousReflection;
        DefaultReflectionMode previousMode;
        public static CityStage Create(Transform parent)
        {
            var stage=new GameObject("Waterfront stage").AddComponent<CityStage>();
            stage.transform.SetParent(parent,false);stage.Build();return stage;
        }
        void Build()
        {
            ground=new Material(Resources.Load<Shader>("CityGround"));
            metal=new Material(Resources.Load<Material>("PrototypeSurface"));
            metal.color=new Color(.035f,.065f,.11f);metal.SetFloat("_Metallic",.7f);metal.SetFloat("_Glossiness",.65f);
            edgeLight=new Material(Resources.Load<Material>("PrototypeSurface"));
            edgeLight.color=new Color(.08f,.32f,.48f);edgeLight.EnableKeyword("_EMISSION");edgeLight.SetColor("_EmissionColor",new Color(.10f,.7f,1)*1.5f);
            // The rear boundary sits below the distant waterfront so the skyline remains visible.
            Box("Stone plaza",new Vector3(0,-.085f,-8),new Vector3(70,.13f,25),ground);
            Box("Waterfront coping",new Vector3(0,.015f,4.4f),new Vector3(70,.13f,.35f),metal);
            Box("Recessed waterfront light",new Vector3(0,.03f,4.20f),new Vector3(70,.025f,.025f),edgeLight);
            for(int side=-1;side<=1;side+=2)
                for(int row=0;row<4;row++)
                {
                    float x=side*(6+row*2.8f);
                    Box("Low waterfront marker",new Vector3(x,.21f,4.4f),new Vector3(.15f,.4f,.2f),metal);
                    Box("Marker lamp",new Vector3(x,.39f,4.4f),new Vector3(.16f,.035f,.21f),edgeLight);
                }
            previousSky=RenderSettings.skybox;previousReflection=RenderSettings.customReflectionTexture;previousMode=RenderSettings.defaultReflectionMode;
            sky=new Material(Resources.Load<Shader>("CitySky"));RenderSettings.skybox=sky;
            reflections=new Cubemap(128,TextureFormat.RGBAHalf,true){name="Waterfront lighting reflection"};
            var capture=new GameObject("Environment capture").AddComponent<Camera>();capture.enabled=false;
            capture.clearFlags=CameraClearFlags.Skybox;capture.cullingMask=0;capture.allowHDR=true;
            capture.RenderToCubemap(reflections);Release(capture.gameObject);
            RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.customReflectionTexture=reflections;
            RenderSettings.reflectionIntensity=.85f;
        }
        void Box(string name,Vector3 position,Vector3 scale,Material material)
        {GameWorld.Primitive(name,PrimitiveType.Cube,transform,position,scale,material);}
        void OnDestroy()
        {
            if(RenderSettings.customReflectionTexture==reflections)
            {RenderSettings.customReflectionTexture=previousReflection;RenderSettings.defaultReflectionMode=previousMode;RenderSettings.skybox=previousSky;}
            Release(reflections);Release(ground);Release(metal);Release(edgeLight);Release(sky);
        }
        static void Release(Object value)
        {if(!value)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
