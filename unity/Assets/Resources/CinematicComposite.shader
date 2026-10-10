Shader "Training/CinematicComposite" {
 Properties { _MainTex("Scene",2D)="white"{} }
 SubShader { Cull Off ZWrite Off ZTest Always
 CGINCLUDE
 #include "UnityCG.cginc"
 sampler2D _MainTex,_Bloom;float4 _MainTex_TexelSize;float2 _Direction;float _Strength;float4 _FlashColor;float4 _ShockColor;float _ShockRadius;float _ShockStrength;
 float4 _PulseCenter;float _Aspect;
 float4 _Motion,_MotionColor;
 half4 extract(v2f_img i):SV_Target {half3 c=tex2D(_MainTex,i.uv).rgb;float b=max(c.r,max(c.g,c.b));return half4(c*saturate((b-.85)/max(b,.001)),1);}
 half4 blur(v2f_img i):SV_Target {
  float2 d=_MainTex_TexelSize.xy*_Direction;
  return tex2D(_MainTex,i.uv)*.227027+(tex2D(_MainTex,i.uv+d*1.384615)+tex2D(_MainTex,i.uv-d*1.384615))*.316216+(tex2D(_MainTex,i.uv+d*3.230769)+tex2D(_MainTex,i.uv-d*3.230769))*.070270;
 }
 half4 compose(v2f_img i):SV_Target {
  half3 c=tex2D(_MainTex,i.uv).rgb;
  // Viewport-bound motion belongs to the final lens, not world geometry.
  // Keep the central 64% of the image clean so the child can read the pose,
  // while the outer thirds carry the fast cabinet-style travel smear seen in
  // the reference footage.
  // Keep the soft halo outside the measured central 78% so the pose remains
  // untouched even after the streak body is widened for a TV-sized display.
  float rim=smoothstep(.80,.99,abs(i.uv.x*2-1));
  // Leave the top and bottom HUD rails clean; the old .03/.97 shoulders
  // placed faint speed lines behind the title and footer on short windows.
  float vertical=smoothstep(.09,.18,i.uv.y)*(1-smoothstep(.82,.91,i.uv.y));
  float motionAmount=rim*vertical*_Motion.x;
  if(motionAmount>0) {
   float aspect=max(_Aspect,.1);
   float2 d=(i.uv-float2(.5,.49))*float2(aspect,1);
   float2 drag=normalize(d)/float2(aspect,1)*(.0085*_Motion.x);
   half3 trail=(tex2D(_MainTex,saturate(i.uv-drag)).rgb+tex2D(_MainTex,saturate(i.uv-drag*2)).rgb)*.5;
   // The reference cabinet uses a readable blue/orange speed shell at the
   // instant of contact. Keep the centre clean, but make the outer rails
   // survive a living-room TV's motion blur and camera exposure.
   c=lerp(c,trail,motionAmount*.42);
   float side=i.uv.x<.5?-1:1;
   float streak=0;
   [unroll] for(int n=0;n<3;n++) {
    float slope=(n-1)*.38+side*.035+sin(_Motion.y*6+n*2.1)*.025;
    float separation=abs(d.y-slope*abs(d.x));
    // The previous 1–3 px filaments read as hard debug lines on a TV.  Keep
    // the same three directional bands, but give each a wide energy body and
    // a softer falloff so the lens feels like a photographed light streak.
    float width=.0038+rim*.0052;
    float filament=exp(-pow(separation/width,2))*.34;
    float halo=exp(-pow(separation/(width*5.2),2))*.30;
    float shimmer=.82+.18*sin(_Motion.y*9+n*2.1+side);
    streak+=(filament+halo)*shimmer;
   }
   float lead=_Motion.z==0?1:lerp(.60,1,step(0,side*_Motion.z));
   c+=_MotionColor.rgb*streak*motionAmount*.30*lead;
  }
  c+=tex2D(_Bloom,i.uv).rgb*_Strength;
  // Keep the Fuji plate realistic, but give the whole combat lens the
  // saturated cabinet response visible in the reference video.  Shadows pick
  // up a restrained blue night key and brighter lava/suit highlights gain a
  // warm orange lift.  This is applied after bloom so the characters and VFX
  // share one display response; the separate photo camera never uses this
  // composite and therefore keeps its neutral export.
  float sceneLuma=dot(c,float3(.2126,.7152,.0722));
  float shadowGrade=1-smoothstep(.10,.48,sceneLuma);
  float highlightGrade=smoothstep(.36,.84,sceneLuma);
  // Keep the idle Fuji plate and unlit inspection samples byte-stable. The
  // display grade is useful during a hit, but a permanent contrast/colour
  // wash made the neutral scene look like a debug filter and broke the HDR
  // shoulder review. Activate it only when the lens is actually in motion.
  float2 delta=(i.uv-_PulseCenter.xy)*float2(_Aspect,1);
  float deltaSq=dot(delta,delta);
  // Match the compositor's measured center-safe band (u=.11..89) and keep
  // the top/bottom HUD rails free of motion grading.
  float edgeGrade=smoothstep(.82,.99,abs(i.uv.x*2-1));
  float gradeVertical=smoothstep(.09,.18,i.uv.y)*(1-smoothstep(.82,.91,i.uv.y));
  edgeGrade*=gradeVertical;
  // Localized pulses use the world-projected contact as their grading mask;
  // a distant pixel should keep the unlit scene while a global transition
  // pulse continues to grade the whole frame.
  float localizedMask=_PulseCenter.z>.5?exp(-deltaSq/.13):1;
  float visiblePulse=(_PulseCenter.z<.5||_PulseCenter.w>.5)?1:0;
  float lensActivity=saturate(_Motion.x*.85*edgeGrade+(_ShockStrength*.80+_FlashColor.a*saturate(_Strength))*localizedMask*visiblePulse);
  c=lerp(c,lerp(sceneLuma.xxx,c,1.08),lensActivity);
  c=lerp(c,(c-.5)*1.045+.5,lensActivity);
  c+=float3(-.006,.002,.020)*shadowGrade*lensActivity;
  c+=float3(.016,.004,-.004)*highlightGrade*lensActivity;
  // Contact frames in the reference cabinet briefly lift saturation and
  // contrast, while the neutral Fuji night remains untouched between beats.
  // The motion shell is a peripheral lens effect: applying its grade to the
  // whole frame used to tint the fighter's torso and made the center-safe-band
  // review fail. Keep localized impact light global, but confine motion grade
  // to the same outer rail that carries the streaks.
  // `_PulseCenter.w` is zero when a localized world point projects behind the
  // camera. A hidden impact must not leave a global tint or starburst behind.
  float localizedGrade=saturate(_ShockStrength*.16+_FlashColor.a*.10*saturate(_Strength))*visiblePulse*localizedMask;
  float grade=saturate(localizedGrade+_Motion.x*.22*edgeGrade);
  float luma=dot(c,float3(.2126,.7152,.0722));
  c=lerp(c,luma.xxx+(c-luma.xxx)*(1+grade*1.35),grade);
  c*=1+grade*.08;
  float distanceFromCenter=length(delta);
  // Keep the dark stage and costume colors through a hit. A local exposure
  // bloom and a faint circular wave carry the impact instead of a flat tint.
  float localFlash=exp(-deltaSq/.020)*.46+exp(-deltaSq/.13)*.075;
  float exposure=lerp(.16,localFlash,_PulseCenter.z)*_PulseCenter.w;
  c+=_FlashColor.rgb*_FlashColor.a*exposure;
  float ring=exp(-pow((distanceFromCenter-_ShockRadius)/.022,2))*_ShockStrength*.24*_PulseCenter.w;
  c+=_ShockColor.rgb*ring;
  // The reference cabinet punctuates contact with a short star-shaped burst,
  // not only a point flash. Keep it localized to the collision and fade it
  // toward the edge so the fighters remain readable on a television.
  if(_PulseCenter.z>0.5&&_PulseCenter.w>0.5)
  {
   float angle=atan2(delta.y,delta.x);
   float spokes=pow(saturate(.5+.5*cos(angle*8+_ShockRadius*18)),18);
   float reach=exp(-distanceFromCenter*1.65)*saturate(_ShockStrength*1.15);
   float streak=spokes*reach*(.11+.20*exp(-deltaSq/.055));
   c+=_ShockColor.rgb*streak;
   float core=exp(-deltaSq/.006)*_ShockStrength*.16;
   c+=_FlashColor.rgb*core;
  }
  float2 p=i.uv*2-1;c*=1-dot(p,p)*.035;
  // A soft shoulder leaves the night scene unchanged. Bright color channels
  // approach white gradually instead of clipping to a flat white patch.
  float3 excess=max(0,c-.78);
  c=min(c,.78)+.22*excess/(.22+excess);
  return half4(c,1);
 }
 ENDCG
 Pass { CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment extract
 ENDCG }
 Pass { CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment blur
 ENDCG }
 Pass { CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment compose
 ENDCG }
 }
}
