Shader "Training/ContactSpark" {
 SubShader {
  Tags {"Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True"}
  Blend One One ZWrite Off ZTest LEqual Cull Off
  Pass { CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
   half4 frag(v2f i):SV_Target {
    float across=i.uv.y*2-1,along=saturate(i.uv.x);
    float head=exp(-across*across*14-pow((along-.90)*9,2));
    float tail=exp(-across*across*25)*smoothstep(0,.65,along)*(1-smoothstep(.78,1,along));
    float halo=exp(-across*across*4)*head*.20;
    float3 heat=lerp(float3(1,.24,.035),float3(2.6,1.8,.8),head);
    return half4(heat*(head+tail*.50+halo)*i.color.a,0);
   }
  ENDCG }
 }
}
