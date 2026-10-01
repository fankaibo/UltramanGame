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
  // Its inner edge is exactly zero across the central 78% of the image.
  float rim=smoothstep(.78,.98,abs(i.uv.x*2-1));
  float vertical=smoothstep(.06,.18,i.uv.y)*(1-smoothstep(.83,.94,i.uv.y));
  float motionAmount=rim*vertical*_Motion.x;
  if(motionAmount>0) {
   float aspect=max(_Aspect,.1);
   float2 d=(i.uv-float2(.5,.49))*float2(aspect,1);
   float2 drag=normalize(d)/float2(aspect,1)*(.006*_Motion.x);
   half3 trail=(tex2D(_MainTex,saturate(i.uv-drag)).rgb+tex2D(_MainTex,saturate(i.uv-drag*2)).rgb)*.5;
   // The reference cabinet uses a readable blue/orange speed shell at the
   // instant of contact. Keep the centre clean, but make the outer rails
   // survive a living-room TV's motion blur and camera exposure.
   c=lerp(c,trail,motionAmount*.32);
   float side=i.uv.x<.5?-1:1;
   float streak=0;
   [unroll] for(int n=0;n<3;n++) {
    float slope=(n-1)*.38+side*.035+sin(_Motion.y*6+n*2.1)*.025;
    float separation=abs(d.y-slope*abs(d.x));
    float width=.0012+rim*.0025;
    float filament=exp(-pow(separation/width,2));
    float halo=exp(-pow(separation/(width*3.5),2))*.14;
    streak+=(filament+halo)*(.64+.36*sin(_Motion.y*9+n*2.1+side));
   }
   float lead=_Motion.z==0?1:lerp(.60,1,step(0,side*_Motion.z));
   c+=_MotionColor.rgb*streak*motionAmount*.36*lead;
  }
  c+=tex2D(_Bloom,i.uv).rgb*_Strength;
  // Contact frames in the reference cabinet briefly lift saturation and
  // contrast, while the neutral Fuji night remains untouched between beats.
  float grade=saturate(_Motion.x*.22+_ShockStrength*.16+_FlashColor.a*.10);
  float luma=dot(c,float3(.2126,.7152,.0722));
  c=lerp(c,luma.xxx+(c-luma.xxx)*(1+grade*1.35),grade);
  c*=1+grade*.08;
  float2 delta=(i.uv-_PulseCenter.xy)*float2(_Aspect,1);
  float distanceFromCenter=length(delta);
  // Keep the dark stage and costume colors through a hit. A local exposure
  // bloom and a faint circular wave carry the impact instead of a flat tint.
  float deltaSq=dot(delta,delta);
  float localFlash=exp(-deltaSq/.020)*.46+exp(-deltaSq/.13)*.075;
  float exposure=lerp(.16,localFlash,_PulseCenter.z)*_PulseCenter.w;
  c+=_FlashColor.rgb*_FlashColor.a*exposure;
  float ring=exp(-pow((distanceFromCenter-_ShockRadius)/.022,2))*_ShockStrength*.24*_PulseCenter.w;
  c+=_ShockColor.rgb*ring;
  // The reference cabinet punctuates contact with a short star-shaped burst,
  // not only a point flash. Keep it localized to the collision and fade it
  // toward the edge so the fighters remain readable on a television.
  if(_PulseCenter.z>0.5)
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
