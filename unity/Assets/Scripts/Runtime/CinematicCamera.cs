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
        Camera view;
        Vector3 pulseWorld;
        bool localized;
        void OnEnable()
        {view=GetComponent<Camera>();var shader=Resources.Load<Shader>("CinematicComposite");if(shader&&shader.isSupported)composite=new Material(shader);}
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
                // Project after GameWorld has positioned the camera. The flash
                // stays on the collision, including closeups and camera recoil.
                var center=localized?view.WorldToViewportPoint(pulseWorld):new Vector3(.5f,.49f,1);
                composite.SetVector("_PulseCenter",new Vector4(center.x,center.y,localized?1:0,center.z>0?1:0));
                composite.SetFloat("_Aspect",source.width/(float)source.height);
                composite.SetFloat("_ShockRadius",Mathf.Lerp(.035f,.52f,Mathf.Clamp01(shockAge/.22f)));
                composite.SetFloat("_ShockStrength",shockStrength);
                composite.SetColor("_ShockColor",new Color(flashColor.r,flashColor.g,flashColor.b,1));
                Graphics.Blit(source,destination,composite,2);RenderCount++;
            }
            finally {RenderTexture.ReleaseTemporary(a);RenderTexture.ReleaseTemporary(b);}
        }
        public void Pulse(Color color,float strength)
        { flash=Mathf.Max(flash,Mathf.Clamp01(strength));shockStrength=Mathf.Max(shockStrength,Mathf.Clamp01(strength)*.72f);shockAge=0;flashColor=color;pulsePending=true;localized=false; }
        public void PulseAt(Vector3 position,Color color,float strength)
        {Pulse(color,strength);pulseWorld=position;localized=true;}
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
        public void Clear() {flash=0;shockAge=10;shockStrength=0;flashColor=Color.white;pulsePending=false;localized=false;pulseWorld=Vector3.zero;}
        void OnDisable()
        {Clear();if(composite){if(Application.isPlaying)Destroy(composite);else DestroyImmediate(composite);composite=null;}}
    }
}
