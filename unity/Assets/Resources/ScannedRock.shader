Shader "Training/ScannedRock" {
 Properties {
  _MainTex("Scanned colour",2D)="gray"{}
  [Normal] _Normal("Scanned normal",2D)="bump"{}
  _ARM("Occlusion roughness metal",2D)="white"{}
  _Color("Volcanic grading",Color)=(.72,.76,.80,1)
 }
 SubShader {
  Tags {"RenderType"="Opaque"}
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows
  #pragma target 3.0
  sampler2D _MainTex,_Normal,_ARM;fixed4 _Color;
  struct Input {float2 uv_MainTex;};
  void surf(Input i,inout SurfaceOutputStandard o) {
   float3 c=tex2D(_MainTex,i.uv_MainTex).rgb;
   float gray=dot(c,float3(.2126,.7152,.0722));
   // Keep the scan's aligned colour, cavities and normal detail. Grade the
   // weathered stone into the ash field without inventing tiled cracks.
   o.Albedo=lerp(c,gray.xxx,.78)*_Color.rgb;
   float3 arm=tex2D(_ARM,i.uv_MainTex).rgb;
   o.Normal=UnpackNormal(tex2D(_Normal,i.uv_MainTex));
   o.Occlusion=lerp(.45,1,arm.r);o.Metallic=0;o.Smoothness=(1-arm.g)*.42;
  }
  ENDCG
 }
 FallBack "Standard"
}
