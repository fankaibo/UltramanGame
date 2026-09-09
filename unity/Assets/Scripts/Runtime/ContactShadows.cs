using UnityEngine;
using UnityEngine.Rendering;

namespace UltramanGame.Runtime
{
    // Render only the two actors to a small mask. The transparent receiver keeps
    // the existing city plaza visible and follows the current skeletal silhouettes.
    [ExecuteAlways]
    public sealed class ContactShadows : MonoBehaviour
    {
        public const int ActorLayer=30;
        Camera shadowCamera;
        RenderTexture mask;
        Material receiver;
        GameObject ground;
        Shader silhouette;
        float lastRender=-10;
        bool rendering;
        public int RenderCount { get; private set; }

        void OnEnable()
        {
            silhouette=Resources.Load<Shader>("ShadowSilhouette");
            var shader=Resources.Load<Shader>("ContactShadow");
            if(!silhouette||!shader){enabled=false;return;}
            mask=new RenderTexture(512,512,16,RenderTextureFormat.ARGB32){name="Actor shadow mask",wrapMode=TextureWrapMode.Clamp};mask.Create();
            var go=new GameObject("Actor shadow camera");go.transform.SetParent(transform,false);
            shadowCamera=go.AddComponent<Camera>();shadowCamera.enabled=false;
            shadowCamera.transform.position=Vector3.up*10;shadowCamera.transform.LookAt(Vector3.zero,Vector3.forward);
            shadowCamera.orthographic=true;shadowCamera.orthographicSize=5;shadowCamera.aspect=1;
            shadowCamera.nearClipPlane=.1f;shadowCamera.farClipPlane=15;
            shadowCamera.clearFlags=CameraClearFlags.SolidColor;shadowCamera.backgroundColor=Color.black;
            shadowCamera.cullingMask=1<<ActorLayer;shadowCamera.targetTexture=mask;
            receiver=new Material(shader){name="City contact shadow",mainTexture=mask};
            ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.name="Transparent actor shadows";
            ground.transform.position=Vector3.up*.006f;ground.transform.localScale=Vector3.one;
            Release(ground.GetComponent<Collider>());
            var surface=ground.GetComponent<Renderer>();surface.sharedMaterial=receiver;surface.shadowCastingMode=ShadowCastingMode.Off;
        }
        void OnPreCull() => RenderNow();
        public void RenderNow()
        {
            if(rendering||!shadowCamera||!mask)return;
            if(Application.isPlaying&&Time.unscaledTime-lastRender<1/30f)return;
            // The shadow camera is a child for cleanup, but its projection is fixed to the plaza.
            shadowCamera.transform.position=Vector3.up*10;shadowCamera.transform.LookAt(Vector3.zero,Vector3.forward);
            rendering=true;
            try {shadowCamera.RenderWithShader(silhouette,"");lastRender=Time.unscaledTime;RenderCount++;}
            finally {rendering=false;}
        }
        void OnDisable()
        {
            if(shadowCamera){shadowCamera.targetTexture=null;Release(shadowCamera.gameObject);}
            if(mask){mask.Release();Release(mask);}
            Release(ground);Release(receiver);
            shadowCamera=null;mask=null;ground=null;receiver=null;
        }
        static void Release(Object value)
        {if(!value)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
