using UnityEngine;

namespace UltramanGame.Runtime
{
    [ExecuteAlways,RequireComponent(typeof(Camera))]
    public sealed class CinematicCamera : MonoBehaviour
    {
        Material composite;
        public int RenderCount { get; private set; }
        float flash;
        Color flashColor=Color.white;
        void OnEnable()
        {var shader=Resources.Load<Shader>("CinematicComposite");if(shader&&shader.isSupported)composite=new Material(shader);}
        void OnRenderImage(RenderTexture source,RenderTexture destination)
        {
            if(!composite){Graphics.Blit(source,destination);return;}
            var a=RenderTexture.GetTemporary(Mathf.Max(1,source.width/4),Mathf.Max(1,source.height/4),0,source.format);
            var b=RenderTexture.GetTemporary(a.width,a.height,0,source.format);
            try
            {
                Graphics.Blit(source,a,composite,0);
                composite.SetVector("_Direction",Vector2.right*1.4f);Graphics.Blit(a,b,composite,1);
                composite.SetVector("_Direction",Vector2.up*1.4f);Graphics.Blit(b,a,composite,1);
                composite.SetTexture("_Bloom",a);composite.SetFloat("_Strength",.32f);
                composite.SetColor("_FlashColor",new Color(flashColor.r,flashColor.g,flashColor.b,flash));
                Graphics.Blit(source,destination,composite,2);RenderCount++;
            }
            finally {RenderTexture.ReleaseTemporary(a);RenderTexture.ReleaseTemporary(b);}
        }
        public void Pulse(Color color,float strength)
        { flash=Mathf.Max(flash,Mathf.Clamp01(strength));flashColor=color; }
        void Update()
        { if(flash>0)flash=Mathf.MoveTowards(flash,0,Time.unscaledDeltaTime*8f); }
        void OnDisable()
        {if(composite){if(Application.isPlaying)Destroy(composite);else DestroyImmediate(composite);composite=null;}}
    }
}
