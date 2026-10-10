Shader "Training/MonsterRay" {
 Properties { _Age("Attack age",Float)=0 _Length("Length",Float)=1 _Power("Power",Float)=0 }
 SubShader { Tags { "Queue"="Transparent+1" "RenderType"="Transparent" }
 Blend One OneMinusSrcAlpha ZWrite Off Cull Off
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 float _Age,_Length,_Power;
 struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
 v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord.xy;return o;}
 half4 frag(v2f i):SV_Target {
  float x=i.uv.x*max(.05,_Length),y=i.uv.y*2-1;
  float wave=sin(x*22-_Age*44),phase=sin(x*33-_Age*58);
  float core=exp(-pow(abs(y-wave*.025)*4.2,3));
  float body=exp(-y*y*5)*( .70+.30*phase*phase );
  float bands=pow(saturate(wave),8)*exp(-y*y*2.7);
  float edge=(1-smoothstep(.72,1,abs(y)))*_Power;
  float3 radiance=(float3(1.3,1.05,1.35)*core+float3(.40,.025,.65)*body+float3(.6,.15,.82)*bands)*edge;
  return half4(radiance,core*.90*edge);
 }
 ENDCG }
 }
}
