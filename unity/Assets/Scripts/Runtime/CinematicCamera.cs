using UnityEngine;

namespace UltramanGame.Runtime
{
    [ExecuteAlways,RequireComponent(typeof(Camera))]
    public sealed class CinematicCamera : MonoBehaviour
    {
        Material composite;
        public int RenderCount { get; private set; }
        float flash;
        bool pulsePending;
        Color flashColor=Color.white;
        float shockAge=10,shockStrength;
        void OnEnable()
        {var shader=Resources.Load<Shader>("CinematicComposite");if(shader&&shader.isSupported)composite=new Material(shader);}
        void OnRenderImage(RenderTexture source,RenderTexture destination)
        {
            if(!composite){Graphics.Blit(source,destination);return;}
            // Keep the original scene full resolution and use a half-resolution
            // bloom buffer. The previous quarter-size buffer softened the beam
            // edge and character rim on a 1080P/2K television.
            var a=RenderTexture.GetTemporary(Mathf.Max(1,source.width/2),Mathf.Max(1,source.height/2),0,source.format);
            var b=RenderTexture.GetTemporary(a.width,a.height,0,source.format);
            try
            {
                Graphics.Blit(source,a,composite,0);
                composite.SetVector("_Direction",Vector2.right*1.4f);Graphics.Blit(a,b,composite,1);
                composite.SetVector("_Direction",Vector2.up*1.4f);Graphics.Blit(b,a,composite,1);
                composite.SetTexture("_Bloom",a);composite.SetFloat("_Strength",.32f);
                composite.SetColor("_FlashColor",new Color(flashColor.r,flashColor.g,flashColor.b,flash));
                // A very short cabinet-style ripple makes the contact frame read
                // as a hit instead of only a colour change. It is deliberately
                // screen-space and deterministic, so it does not cover the
                // actors or alter the gameplay clock.
                composite.SetFloat("_ShockRadius",Mathf.Lerp(.04f,.92f,Mathf.Clamp01(shockAge/.22f)));
                composite.SetFloat("_ShockStrength",shockStrength);
                composite.SetColor("_ShockColor",new Color(flashColor.r,flashColor.g,flashColor.b,1));
                Graphics.Blit(source,destination,composite,2);RenderCount++;
            }
            finally {RenderTexture.ReleaseTemporary(a);RenderTexture.ReleaseTemporary(b);}
        }
        public void Pulse(Color color,float strength)
        { flash=Mathf.Max(flash,Mathf.Clamp01(strength));shockStrength=Mathf.Max(shockStrength,Mathf.Clamp01(strength)*.72f);shockAge=0;flashColor=color;pulsePending=true; }
        // GameWorld owns presentation time. Editor captures invoke it directly,
        // so relying on MonoBehaviour.Update leaves every later frame tinted.
        public void Tick(float dt)
        {
            if(dt<=0)return;
            // Hits are queued before GameWorld.Tick; display the contact frame
            // once even when a 15/30 fps step exceeds an ordinary pulse's life.
            if(pulsePending){pulsePending=false;return;}
            if(flash>0)flash=Mathf.MoveTowards(flash,0,dt*8f);
            shockAge+=dt;shockStrength=Mathf.MoveTowards(shockStrength,0,dt*6.2f);
        }
        public void Clear() {flash=0;shockAge=10;shockStrength=0;flashColor=Color.white;pulsePending=false;}
        void OnDisable()
        {Clear();if(composite){if(Application.isPlaying)Destroy(composite);else DestroyImmediate(composite);composite=null;}}
    }
}
