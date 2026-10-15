Shader "Training/HudImage" {
 Properties {_MainTex("Image",2D)="white"{} _Tint("Linear tint",Vector)=(1,1,1,1)}
 SubShader {
  Tags {"Queue"="Overlay"} Cull Off ZWrite Off ZTest Always
  Blend SrcAlpha OneMinusSrcAlpha
  Pass { CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   sampler2D _MainTex;float4 _MainTex_TexelSize,_Tint;
   struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
   v2f vert(appdata_base v) {v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord.xy;return o;}
   fixed4 frag(v2f i):SV_Target {return tex2D(_MainTex,i.uv)*_Tint;}
  ENDCG }
 }
}
