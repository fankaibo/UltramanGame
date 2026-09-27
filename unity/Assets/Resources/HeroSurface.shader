Shader "Training/HeroSurface" {
 Properties {
  _MainTex("Original suit",2D)="white"{} _Color("Tint",Color)=(1,1,1,1)
  _Metallic("Metal",Range(0,1))=.2 _Glossiness("Smoothness",Range(0,1))=.42
  _TextureArmor("Silver material separation",Range(0,1))=0
  _EmissionColor("Base emission",Color)=(0,0,0,0)
  _GuardPoint("Shield contact and radius",Vector)=(0,0,0,1)
  _GuardColor("Reflected shield light",Color)=(0,0,0,0)
  [HideInInspector] _EmissionAudit("Emission inspection",Float)=0
  _SrcBlend("Source",Float)=1 _DstBlend("Destination",Float)=0 _ZWrite("Depth",Float)=1
 }
 SubShader {
  Tags {"RenderType"="Opaque"} Blend [_SrcBlend] [_DstBlend] ZWrite [_ZWrite]
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows addshadow keepalpha finalcolor:FadeAdditive
  #pragma target 3.0
  sampler2D _MainTex;fixed4 _Color,_EmissionColor;float4 _GuardPoint;
  half4 _GuardColor;half _Metallic,_Glossiness,_TextureArmor,_EmissionAudit;
  struct Input {float2 uv_MainTex;float3 worldPos;float3 worldNormal;};
  void FadeAdditive(Input i,SurfaceOutputStandard o,inout fixed4 color) {
   if(_EmissionAudit>.5) {
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
  void surf(Input i,inout SurfaceOutputStandard o) {
   fixed4 c=tex2D(_MainTex,i.uv_MainTex)*_Color;
   float brightest=max(c.r,max(c.g,c.b)),darkest=min(c.r,min(c.g,c.b));
   float saturation=(brightest-darkest)/max(.03,brightest);
   // Separate bright neutral armor from colored suit panels using the
   // original texture. Color borders are not invented as surface normals.
   float silver=(1-smoothstep(.12,.35,saturation))*smoothstep(.15,.65,brightest)*_TextureArmor;
   o.Albedo=c.rgb;o.Metallic=lerp(_Metallic,.68,silver);o.Smoothness=lerp(_Glossiness,.57,silver);
   float3 toward=_GuardPoint.xyz-i.worldPos;
   float distanceToLight=length(toward)/max(.01,_GuardPoint.w);
   float facing=smoothstep(-.15,.65,dot(normalize(i.worldNormal),normalize(toward+float3(0,.00001,0))));
   float patch=(1-smoothstep(.12,1,distanceToLight))*facing;
   // A small local reflection retains the original panels; distant legs and
   // the back do not turn into the same luminous blue as the shield.
   o.Emission=_EmissionColor.rgb+_GuardColor.rgb*(_GuardColor.a*patch)*(.18+c.rgb*.55);
   o.Alpha=c.a;
  }
  ENDCG
 }
 FallBack "Standard"
}
