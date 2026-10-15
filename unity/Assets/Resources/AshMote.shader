Shader "Training/AshMote" {
 Properties { _Color("Color",Color)=(.35,.4,.45,.1) }
 SubShader {
  Tags {"Queue"="Transparent+12" "RenderType"="Transparent"}
  Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
  Pass { CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   fixed4 _Color;
   struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;float2 state:TEXCOORD1;};
   struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float2 state:TEXCOORD1;};
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.state=v.state;return o;}
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   fixed4 frag(v2f i):SV_Target {
    float2 p=i.uv*2-1;float n=hash(p*19+i.state.x);float d=length(p)*(1+.18*sin(atan2(p.y,p.x)*5+i.state.x));
    float edge=saturate((1-d)*5);float fade=smoothstep(0,.12,i.state.y)*(1-smoothstep(.78,1,i.state.y));
    return fixed4(_Color.rgb*(.72+.5*n),_Color.a*edge*fade);
   }
  ENDCG }
 }
}
