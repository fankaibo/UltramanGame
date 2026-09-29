Shader "Training/ChargeFilament" {
 SubShader { Tags {"Queue"="Transparent+2" "RenderType"="Transparent"}
  Blend SrcAlpha One ZWrite Off Cull Off
  Pass { CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct input {float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
   struct v2f {float4 pos:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
   v2f vert(input v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=v.uv;return o;}
   half4 frag(v2f i):SV_Target {
    float y=abs(i.uv.y*2-1);
    float soft=exp(-y*y*4)*(1-smoothstep(.55,1,y));
    float core=exp(-y*y*48);
    return half4(i.color.rgb*(.4+core*1.5),i.color.a*soft);
   }
  ENDCG }
 }
}
