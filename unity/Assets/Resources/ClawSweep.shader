Shader "Training/ClawSweep" {
 SubShader {
  Tags {"Queue"="Transparent+15" "RenderType"="Transparent"}
  Blend SrcAlpha One ZWrite Off Cull Off
  Pass { CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct Input {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
   struct Output {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
   Output vert(Input v) {Output o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
   fixed4 frag(Output i):SV_Target {
    float y=i.uv.y*2-1;
    float feather=sin(i.uv.x*63+y*14)*.06+sin(i.uv.x*117-y*21)*.035;
    float edge=pow(saturate(1-abs(y)-feather),2);
    float core=exp(-pow((y+.12)*6,2));
    float ends=smoothstep(0,.18,i.uv.x)*(1-smoothstep(.80,1,i.uv.x));
    float alpha=(edge*.28+core*.75)*ends*i.color.a;
    return fixed4(lerp(i.color.rgb,float3(1,.98,.88),core*.75),alpha);
   }
  ENDCG }
 }
}
