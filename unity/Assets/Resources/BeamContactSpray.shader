Shader "Training/BeamContactSpray" {
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
    float head=exp(-across*across*17-pow((along-.86)*7,2));
    float tail=exp(-across*across*24)*smoothstep(0,.72,along)*(1-smoothstep(.82,1,along));
    float halo=exp(-across*across*3-pow((along-.8)*4,2));
    return half4((float3(2.2,3.2,3.8)*head+float3(.18,.85,1.8)*(tail*.8+halo*.18))*i.color.a,0);
   }
  ENDCG }
 }
}
