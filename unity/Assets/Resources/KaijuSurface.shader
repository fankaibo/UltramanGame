Shader "Training/KaijuSurface" {
 Properties {
  _MainTex("Original skin",2D)="white"{} _Color("Tint",Color)=(1,1,1,1)
  _EmissionColor("Arcade impact emission",Color)=(0,0,0,0)
  _ImpactPoint("Contact and radius",Vector)=(0,0,0,1)
  _ImpactDirection("Contact facing",Vector)=(0,0,1,0)
  _ImpactColor("Local impact light",Color)=(0,0,0,0)
  [HideInInspector] _EmissionAudit("Emission inspection",Float)=0
  _Metallic("Metal",Range(0,1))=.03 _Glossiness("Smoothness",Range(0,1))=.26
  _SrcBlend("Source",Float)=1 _DstBlend("Destination",Float)=0 _ZWrite("Depth",Float)=1
 }
 SubShader {
  Tags {"RenderType"="Opaque"} Blend [_SrcBlend] [_DstBlend] ZWrite [_ZWrite]
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows addshadow keepalpha finalcolor:FadeAdditive
  #pragma target 3.0
  sampler2D _MainTex;float4 _MainTex_TexelSize,_ImpactPoint,_ImpactDirection;fixed4 _Color,_EmissionColor;half4 _ImpactColor;half _Metallic,_Glossiness,_EmissionAudit;
  struct Input {float2 uv_MainTex;float3 worldPos;float3 worldNormal;INTERNAL_DATA};
  float hash(float2 p) {return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
  float cell(float2 p) {
   float2 ip=floor(p),f=frac(p);float d=1;
   for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++){
    float2 g=float2(x,y);float2 r=g+float2(hash(ip+g),hash(ip+g+37))-f;d=min(d,dot(r,r));}
   return sqrt(d);
  }
  float relief(float2 p) {return smoothstep(.03,.6,cell(p));}
  float skinHeight(float2 uv) {
   fixed3 c=tex2D(_MainTex,uv).rgb;
   #ifndef UNITY_COLORSPACE_GAMMA
    c=LinearToGammaSpace(c);
   #endif
   return dot(c,float3(.30,.59,.11));
  }
  void FadeAdditive(Input i,SurfaceOutputStandard o,inout fixed4 color) {
   // Editor inspection reads the actual surf emission without differences
   // from normal-map lighting. Gameplay always uses the default value 0.
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
   float2 p=i.uv_MainTex*float2(35,140);
   float n=relief(p),dx=relief(p+float2(.08,0))-n,dy=relief(p+float2(0,.08))-n;
   // The source texture contains the finger and chest scale detail. Reuse its
   // luminance as a shallow normal so the existing low-poly mesh catches a
   // moving key light without inventing a separate normal-map asset.
   float2 texel=max(_MainTex_TexelSize.xy*2.0,float2(.0005,.0005));
   float texDx=skinHeight(i.uv_MainTex+float2(texel.x,0))-skinHeight(i.uv_MainTex-float2(texel.x,0));
   float texDy=skinHeight(i.uv_MainTex+float2(0,texel.y))-skinHeight(i.uv_MainTex-float2(0,texel.y));
   o.Albedo=c.rgb*lerp(.88,1.05,n);o.Normal=normalize(float3(dx*.42+texDx*1.8,dy*.42+texDy*1.8,1));
   o.Metallic=_Metallic;o.Smoothness=_Glossiness*lerp(.65,1.15,n);o.Occlusion=lerp(.86,1,n);
   // The contact follows a chest bone through recoil. Light a bounded patch
   // on the facing skin, keeping the legs, tail, texture and normal relief.
   float distanceToHit=length(i.worldPos-_ImpactPoint.xyz)/max(.01,_ImpactPoint.w);
   float facing=smoothstep(0,.45,dot(normalize(WorldNormalVector(i,o.Normal)),normalize(_ImpactDirection.xyz)));
   float patch=(1-smoothstep(.15,1,distanceToHit))*facing;
   float detail=.30+.70*dot(c.rgb,float3(.30,.59,.11));
   o.Emission=_EmissionColor.rgb+_ImpactColor.rgb*(_ImpactColor.a*patch*detail);o.Alpha=c.a;
  }
  ENDCG
 }
 FallBack "Standard"
}
