Shader "Training/MebiumBladeSweep" {
 SubShader {
  Tags {"Queue"="Transparent+11" "RenderType"="Transparent"}
  Blend SrcAlpha One ZWrite Off Cull Off
  Pass { CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct Input {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
   struct Output {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
   Output vert(Input v){Output o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
   fixed4 frag(Output i):SV_Target {
    float tip=smoothstep(0,.16,i.uv.y)*(1-smoothstep(.82,1,i.uv.y));
    float band=.72+.28*sin(i.uv.y*38+i.uv.x*6);
    return fixed4(1,.48,.055,i.color.a*tip*band*smoothstep(0,.28,i.uv.x));
   }
  ENDCG }
 }
}
