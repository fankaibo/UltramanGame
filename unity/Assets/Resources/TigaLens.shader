Shader "Training/TigaLens" {
 Properties {
  _Color("Original warm lens",Color)=(1,.98225,.521,1)
  _EmissionColor("Lens illumination",Color)=(1,1,.55,1)
  _Metallic("Metal",Range(0,1))=0 _Glossiness("Polished cover",Range(0,1))=.65
  [HideInInspector] _LensAudit("Emission inspection",Float)=0
  _SrcBlend("Source",Float)=1 _DstBlend("Destination",Float)=0 _ZWrite("Depth",Float)=1
 }
 SubShader {
  Tags {"RenderType"="Opaque"} Blend [_SrcBlend] [_DstBlend] ZWrite [_ZWrite]
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows addshadow keepalpha finalcolor:Finish
  #pragma target 3.0
  fixed4 _Color,_EmissionColor;half _Metallic,_Glossiness,_LensAudit;
  struct Input {float3 worldPos;float3 worldNormal;};
  void surf(Input i,inout SurfaceOutputStandard o) {
   float3 n=normalize(i.worldNormal);
   float facing=saturate(dot(n,normalize(_WorldSpaceCameraPos-i.worldPos)));
   // The source EYESEMI graph combines Layer Weight/Fresnel and warm
   // emission ramps. Preserve that angle-dependent cover in real time;
   // uniform Standard emission erased it during the original FBX conversion.
   float center=smoothstep(.06,.94,facing);
   float transmission=.10+.76*center;
   o.Albedo=_Color.rgb*.085;
   o.Metallic=_Metallic;o.Smoothness=_Glossiness;
   o.Emission=_EmissionColor.rgb*transmission;
   o.Alpha=_Color.a;
  }
  void Finish(Input i,SurfaceOutputStandard o,inout fixed4 color) {
   if(_LensAudit>.5) {
    #ifdef UNITY_PASS_FORWARDADD
     color.rgb=0;
    #else
     color.rgb=o.Emission;
    #endif
    return;
   }
   #ifdef UNITY_PASS_FORWARDADD
    color.rgb*=o.Alpha;
   #endif
  }
  ENDCG
 }
 FallBack "Standard"
}
