Shader "Training/VolcanicSpark" {
 SubShader {
  Tags {"Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True"}
  Blend One One ZWrite Off Cull Off
  Pass { CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
   half4 frag(v2f i):SV_Target {
    float2 p=i.uv*2-1;
    float core=exp(-p.x*p.x*13-p.y*p.y*5);
    float halo=exp(-dot(p,p)*3.8)*.13;
    float edge=(1-smoothstep(.72,1,abs(p.x)))*(1-smoothstep(.75,1,abs(p.y)));
    return half4(i.color.rgb*(core+halo)*edge*i.color.a,0);
   }
  ENDCG }
 }
}
