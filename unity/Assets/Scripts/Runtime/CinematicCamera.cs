using UnityEngine;
using UltramanGame.Core;

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
        Vector4 motion;
        Color motionColor;
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
                composite.SetTexture("_Bloom",a);
                // Keep the Fuji night scene restrained at rest, then let the
                // arcade beats punch through the screen at the exact contact
                // frame. This makes a rush, guard impact and finisher read as
                // one continuous cabinet effect instead of three unrelated
                // world-space flashes.
                float beatBloom=.32f+Mathf.Clamp01(flash)*.22f+
                    Mathf.Clamp01(shockStrength)*.16f+Mathf.Clamp01(motion.x)*.10f;
                composite.SetFloat("_Strength",beatBloom);
                composite.SetColor("_FlashColor",new Color(flashColor.r,flashColor.g,flashColor.b,flash));
                // Project after GameWorld has positioned the camera. The flash
                // stays on the collision, including closeups and camera recoil.
                var center=localized?view.WorldToViewportPoint(pulseWorld):new Vector3(.5f,.49f,1);
                composite.SetVector("_PulseCenter",new Vector4(center.x,center.y,localized?1:0,center.z>0?1:0));
                composite.SetFloat("_Aspect",source.width/(float)source.height);
                composite.SetFloat("_ShockRadius",Mathf.Lerp(.035f,.52f,Mathf.Clamp01(shockAge/.22f)));
                composite.SetFloat("_ShockStrength",shockStrength);
                composite.SetColor("_ShockColor",new Color(flashColor.r,flashColor.g,flashColor.b,1));
                composite.SetVector("_Motion",motion);composite.SetColor("_MotionColor",motionColor);
                Graphics.Blit(source,destination,composite,2);RenderCount++;
            }
            finally {RenderTexture.ReleaseTemporary(a);RenderTexture.ReleaseTemporary(b);}
        }
        public void Pulse(Color color,float strength)
        { flash=Mathf.Max(flash,Mathf.Clamp01(strength));shockStrength=Mathf.Max(shockStrength,Mathf.Clamp01(strength)*.72f);shockAge=0;flashColor=color;pulsePending=true;localized=false; }
        public void PulseAt(Vector3 position,Color color,float strength)
        {Pulse(color,strength);pulseWorld=position;localized=true;}
        public void CombatMotion(Battle state,bool showcase=false)
        {
            motion=Vector4.zero;
            if(showcase||state.Phase!=GamePhase.Battle||state.Action==HeroAction.Beam)return;
            bool punching=state.Action==HeroAction.LeftPunch||state.Action==HeroAction.RightPunch;
            bool combo=punching&&ComboStrikeMotion.Active(state);
            // Every fifth contact already owns a close shot and a larger
            // world-space burst. Carry that same beat into the outer lens so
            // the transition between consecutive punches reads as one arcade
            // rhythm rather than a static pose plus a HUD counter.
            // Keep the established timing windows and amplitudes. Only the
            // interpolation shape changes here: the C2-continuous envelope
            // removes the visible snap at entry, contact and release without
            // changing camera speed, gameplay timing, or the central pose.
            float punch=punching?MotionBeat(state.ActionAge,.025f,.12f,.34f)*(combo?1.28f:1):0;
            float rush=state.Enemy==EnemyPhase.Attack?MotionBeat(state.EnemyAge,.06f,.34f,.78f)*.85f:0;
            float hurt=state.Action==HeroAction.Hurt?MotionBeat(state.ActionAge,0,.10f,.42f)*.90f:0;
            float strength=Mathf.Max(punch,rush,hurt);
            if(strength<=0)return;
            bool hurtLead=hurt>punch&&hurt>=rush,rushLead=!hurtLead&&rush>punch;
            float phase=rushLead?state.EnemyAge:state.ActionAge;
            motion=new Vector4(strength,phase,punching?(state.Action==HeroAction.LeftPunch?-1:1):0,0);
            motionColor=hurtLead?new Color(1,.36f,.20f):rushLead?new Color(1,.66f,.36f):
                combo?new Color(1,.72f,.24f):new Color(.52f,.78f,1);
        }
        static float MotionBeat(float age,float start,float peak,float end)
        {
            float rise=Quintic(Mathf.InverseLerp(start,peak,age));
            float fall=1-Quintic(Mathf.InverseLerp(peak,end,age));
            return rise*fall;
        }
        // C2-continuous easing keeps the edge smear from gaining a visible
        // snap at entry, contact, or release while remaining deterministic at
        // any render rate.
        static float Quintic(float t)
        {
            t=Mathf.Clamp01(t);
            return t*t*t*(t*(t*6-15)+10);
        }
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
        public void Clear() {flash=0;shockAge=10;shockStrength=0;flashColor=Color.white;pulsePending=false;localized=false;pulseWorld=Vector3.zero;motion=Vector4.zero;}
        void OnDisable()
        {Clear();if(composite){if(Application.isPlaying)Destroy(composite);else DestroyImmediate(composite);composite=null;}}
    }
}
