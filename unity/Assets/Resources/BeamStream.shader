Shader "Training/BeamStream" {
 Properties { _Axial("Axial flow",Float)=0 _Clock("Clock",Float)=0 _Length("Length",Float)=1 _Power("Power",Float)=1 _BeamTint("Beam tint",Color)=(.25,.72,1,1) _BeamAccent("Beam accent",Color)=(.90,.98,1,1) }
 SubShader { Tags { "Queue"="Transparent+1" "RenderType"="Transparent" }
 Blend One OneMinusSrcAlpha ZWrite Off Cull Off
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 float _Clock,_Length,_Power,_Axial;float4 _BeamTint,_BeamAccent;
 float hash21(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 float noise21(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash21(a),hash21(a+float2(1,0)),f.x),lerp(hash21(a+float2(0,1)),hash21(a+1),f.x),f.y);}
 struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
 v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord.xy;return o;}
 half4 frag(v2f i):SV_Target {
  float x=i.uv.x*max(.05,_Length),y=i.uv.y*2-1;
  // A dense white discharge with a translucent blue mantle. Premultiplied
  // coverage preserves its core against bright skin, smoke and the night sky;
  // the wider energy field stays additive rather than becoming a solid tube.
  float wobble=sin(x*12-_Clock*24)*.025+sin(x*23-_Clock*31)*.012;
  float core=exp(-pow(abs(y-wobble)*2.65,3));
  float mantle=exp(-y*y*4.3);
  float filament=exp(-pow((y-sin(x*10-_Clock*20)*.42)*24,2))*.20;
  filament+=exp(-pow((y+cos(x*13-_Clock*24)*.52)*28,2))*.13;
  float flow=.94+.06*sin(x*19-_Clock*38);
  float edge=1-smoothstep(.74,1,abs(y));
  if(_Axial>.5)
  {
   // Stretched, aperiodic strands move along the discharge instead of
   // drawing a sinusoidal wire around it. The opaque white core and soft
   // mantle share the original depth-tested ribbon and gameplay clock.
   float drift=(noise21(float2(x*1.7-_Clock*5,9))-.5)*.045;
   float lanes=noise21(float2(x*.85-_Clock*7,y*11));
   float fine=noise21(float2(x*3.3-_Clock*16,y*23+4));
   core=exp(-pow(abs(y-drift)*3.1,3));
   mantle=exp(-y*y*5.6)*(.48+.52*lanes);
   filament=pow(lanes,3)*(.3+.7*fine)*exp(-y*y*2.5)*.65;
   flow=.87+.13*fine;
   edge=1-smoothstep(.60+.12*lanes,1,abs(y));
  }
  float envelope=edge*_Power;
  float3 tint=lerp(_BeamTint.rgb,_BeamAccent.rgb,core*.78);
  float3 radiance=(tint*1.35*core*flow+_BeamTint.rgb*.72*mantle+_BeamAccent.rgb*.95*filament)*envelope;
  return half4(radiance,core*.92*envelope);
 }
 ENDCG }
 }
}
