Shader "Training/ClawSweep" {
 SubShader {
  Tags {"Queue"="Transparent+15" "RenderType"="Transparent"}
  Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
  Pass { CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct Input {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
   struct Output {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
   Output vert(Input v) {Output o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
   fixed4 frag(Output i):SV_Target {
    float y=i.uv.y*2-1;
    // One soft swept area and a thin leading rim, instead of three hot
    // luminous centre lines. Uneven wisps disperse the trailing air sheet.
    float feather=sin(i.uv.x*47+y*13)*.05+sin(i.uv.x*103-y*19)*.035;
    float edge=pow(saturate(1-abs(y)-feather),1.3);
    float rim=exp(-pow((y+.50+sin(i.uv.x*8)*.08)*10,2));
    float wisps=.72+.28*sin(i.uv.x*39-y*16);
    float ends=smoothstep(0,.18,i.uv.x)*(1-smoothstep(.80,1,i.uv.x));
    float alpha=(edge*.35*wisps+rim*.58)*ends*i.color.a;
    return fixed4(lerp(i.color.rgb,float3(1.65,1.62,1.52),rim*.8),alpha);
   }
  ENDCG }
 }
}
