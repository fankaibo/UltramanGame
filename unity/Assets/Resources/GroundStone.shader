Shader "Training/GroundStone" {
 Properties { _Color("Basalt",Color)=(.19,.185,.175,1) }
 SubShader {
  Tags {"RenderType"="Opaque"}
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows addshadow
  #pragma target 3.0
  fixed4 _Color;
  struct Input {float3 worldPos;};
  float hash(float3 p){return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453);}
  void surf(Input i,inout SurfaceOutputStandard o){float3 local=mul(unity_WorldToObject,float4(i.worldPos,1)).xyz;float grain=hash(floor(local*18));o.Albedo=_Color.rgb*lerp(.68,1.35,grain);o.Metallic=0;o.Smoothness=.08;o.Occlusion=.95;}
  ENDCG
 }
 FallBack "Diffuse"
}
