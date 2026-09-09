Shader "Training/EnergyFlare" {
 Properties { _Color("Light",Color)=(.2,.7,1,1) _Ring("Wave",Float)=0 }
 SubShader { Tags { "Queue"="Transparent+10" "RenderType"="Transparent" } Blend SrcAlpha One ZWrite Off Cull Off
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 float4 _Color;float _Ring;
 struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
 v2f vert(appdata_base v) {v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord.xy*2-1;return o;}
 half4 frag(v2f i):SV_Target {
  float r=length(i.uv);
  float glow=pow(saturate(1-r),3)*.8+exp(-r*r*80)*1.7;
  float rays=exp(-abs(i.uv.x)*55)*pow(saturate(1-abs(i.uv.y)),2)*.5+exp(-abs(i.uv.y)*65)*pow(saturate(1-abs(i.uv.x)),2)*.55;
  float ring=exp(-pow((r-.72)*35,2))*.8;
  return half4(_Color.rgb,_Color.a*lerp(glow+rays,ring,_Ring));
 }
 ENDCG }
 }
}
