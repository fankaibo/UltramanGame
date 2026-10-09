Shader "Training/AtmosWave" {
 Properties { _Tint("Tint",Color)=(.35,.75,1,1) _Accent("Accent",Color)=(.9,.98,1,1) _Power("Power",Float)=1 _Phase("Phase",Float)=0 }
 SubShader { Tags { "Queue"="Transparent+2" "RenderType"="Transparent" }
 Blend SrcAlpha One ZWrite Off Cull Off
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 float4 _Tint,_Accent;float _Power,_Phase;
 struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
 v2f vert(appdata_base v){v2f o;float a=v.texcoord.x*6.283185;
  v.vertex.xy*=1+.012*sin(a*9-_Phase*2)+.007*sin(a*17+_Phase*3);
  o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord.xy;return o;}
 half4 frag(v2f i):SV_Target {
  float around=i.uv.x*6.283185;
  float core=pow(abs(sin(i.uv.y*6.283185)),3);
  float ripple=.62+.38*sin(around*7-_Phase*3)*sin(around*13+_Phase);
  float crest=pow(saturate(sin(around*4-_Phase)),12);
  return half4(lerp(_Tint.rgb,_Accent.rgb,core*.38)*(.50+core*.80+crest*.45),_Power*ripple*(.18+core*.65));
 }
 ENDCG }
 }
}
