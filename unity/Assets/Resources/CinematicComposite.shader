Shader "Training/CinematicComposite" {
 Properties { _MainTex("Scene",2D)="white"{} }
 SubShader { Cull Off ZWrite Off ZTest Always
 CGINCLUDE
 #include "UnityCG.cginc"
 sampler2D _MainTex,_Bloom;float4 _MainTex_TexelSize;float2 _Direction;float _Strength;
 half4 extract(v2f_img i):SV_Target {half3 c=tex2D(_MainTex,i.uv).rgb;float b=max(c.r,max(c.g,c.b));return half4(c*saturate((b-.85)/max(b,.001)),1);}
 half4 blur(v2f_img i):SV_Target {
  float2 d=_MainTex_TexelSize.xy*_Direction;
  return tex2D(_MainTex,i.uv)*.227027+(tex2D(_MainTex,i.uv+d*1.384615)+tex2D(_MainTex,i.uv-d*1.384615))*.316216+(tex2D(_MainTex,i.uv+d*3.230769)+tex2D(_MainTex,i.uv-d*3.230769))*.070270;
 }
 half4 compose(v2f_img i):SV_Target {
  half3 c=tex2D(_MainTex,i.uv).rgb+tex2D(_Bloom,i.uv).rgb*_Strength;
  float2 p=i.uv*2-1;c*=1-dot(p,p)*.035;
  return half4(c,1);
 }
 ENDCG
 Pass { CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment extract
 ENDCG }
 Pass { CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment blur
 ENDCG }
 Pass { CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment compose
 ENDCG }
 }
}
