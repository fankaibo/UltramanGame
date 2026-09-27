Shader "Training/BeamStream" {
 Properties { _Clock("Clock",Float)=0 _Length("Length",Float)=1 _Power("Power",Float)=1 }
 SubShader { Tags { "Queue"="Transparent+1" "RenderType"="Transparent" }
 Blend One OneMinusSrcAlpha ZWrite Off Cull Off
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 float _Clock,_Length,_Power;
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
  float envelope=edge*_Power;
  float3 radiance=(float3(1.12,1.26,1.32)*core*flow+float3(.035,.25,.62)*mantle+float3(.20,.62,1)*filament)*envelope;
  return half4(radiance,core*.92*envelope);
 }
 ENDCG }
 }
}
