Shader "Training/GroundStone" {
 Properties {
  _MainTex("Scanned colour",2D)="gray"{}
  [Normal] _Normal("Scanned normal",2D)="bump"{}
  _ARM("Occlusion roughness metal",2D)="white"{}
  _Color("Ash-covered basalt",Color)=(.64,.67,.70,1)
 }
 SubShader {
  Tags {"RenderType"="Opaque"}
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows addshadow
  #pragma target 3.0
  sampler2D _MainTex,_Normal,_ARM;fixed4 _Color;
  struct Input {float2 uv_MainTex;};
  void surf(Input i,inout SurfaceOutputStandard o) {
   float3 colour=tex2D(_MainTex,i.uv_MainTex).rgb;
   float gray=dot(colour,float3(.2126,.7152,.0722));
   o.Albedo=lerp(colour,gray.xxx,.78)*_Color.rgb;
   o.Normal=UnpackNormal(tex2D(_Normal,i.uv_MainTex));
   float3 arm=tex2D(_ARM,i.uv_MainTex).rgb;
   o.Metallic=0;o.Smoothness=(1-arm.g)*.32;o.Occlusion=lerp(.45,1,arm.r);
  }
  ENDCG
 }
 FallBack "Diffuse"
}
