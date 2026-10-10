Shader "Training/KaijuSurface" {
 Properties {
  _MainTex("Original skin",2D)="white"{} _Color("Tint",Color)=(1,1,1,1)
  _EmissionColor("Arcade impact emission",Color)=(0,0,0,0)
  _SkinDetail("Skin relief",Range(0,1))=1
  _DissolveAmount("Departure progress",Range(0,1))=0
  _DissolveBounds("Departure height",Vector)=(0,3.6,0,0)
  _ImpactPoint("Contact and radius",Vector)=(0,0,0,1)
  _ImpactDirection("Contact facing",Vector)=(0,0,1,0)
  _ImpactColor("Local impact light",Color)=(0,0,0,0)
  [HideInInspector] _EmissionAudit("Emission inspection",Float)=0
  _Metallic("Metal",Range(0,1))=.03 _Glossiness("Smoothness",Range(0,1))=.26
  _RimColor("Arcade rim color",Color)=(1,.16,.035,1)
  _RimPower("Arcade rim falloff",Range(0.5,8))=2.6 _RimStrength("Arcade rim strength",Range(0,2))=.24
  _ScaleContrast("Scale layer contrast",Range(0,1))=.82
  _RoughnessVariation("Scale roughness",Range(0,1))=.86
  _LavaFill("Volcanic warm fill",Range(0,1))=.58
  _SrcBlend("Source",Float)=1 _DstBlend("Destination",Float)=0 _ZWrite("Depth",Float)=1
 }
 SubShader {
  Tags {"RenderType"="Opaque"} Blend [_SrcBlend] [_DstBlend] ZWrite [_ZWrite]
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows addshadow keepalpha finalcolor:FadeAdditive
  #pragma target 3.0
  #include "KaijuDissolve.cginc"
  sampler2D _MainTex;float4 _MainTex_TexelSize,_ImpactPoint,_ImpactDirection;fixed4 _Color,_EmissionColor,_RimColor;half4 _ImpactColor;half _Metallic,_Glossiness,_EmissionAudit,_RimPower,_RimStrength,_ScaleContrast,_RoughnessVariation,_LavaFill;
  struct Input {float2 uv_MainTex;float3 worldPos;float3 worldNormal;INTERNAL_DATA};
  float _DissolveAmount,_SkinDetail;float4 _DissolveBounds;
  float hash(float2 p) {return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
  float valueNoise(float2 p) {
   float2 cell=floor(p),f=frac(p);f=f*f*(3-2*f);
   float a=hash(cell),b=hash(cell+float2(1,0)),c=hash(cell+float2(0,1)),d=hash(cell+float2(1,1));
   return lerp(lerp(a,b,f.x),lerp(c,d,f.x),f.y);
  }
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
   float edge=KaijuDissolveEdge(i.worldPos,_DissolveBounds.xy,_DissolveAmount);
   float2 p=i.uv_MainTex*float2(35,140);
   float n=relief(p),dx=relief(p+float2(.08,0))-n,dy=relief(p+float2(0,.08))-n;
   // Golza's source atlas is detailed enough for the silhouette but leaves
   // broad patches flat at the close battle camera. Combine a broad value
   // field with a fine grain so the scales have large planes, small pores and
   // a stable breakup without requiring another normal or mask texture.
   float broad=valueNoise(i.uv_MainTex*float2(8,32));
   float fine=valueNoise(i.uv_MainTex*float2(180,520));
   float scalePattern=saturate(n*.58+broad*.27+fine*.15);
   float cavity=saturate((1-n)*.58+(1-broad)*.30+(1-fine)*.12);
   float scaleLayer=lerp(.76,1.06,scalePattern);
   float scaleWeight=_SkinDetail*_ScaleContrast;
   // The source texture contains the finger and chest scale detail. Reuse its
   // luminance as a shallow normal so the existing low-poly mesh catches a
   // moving key light without inventing a separate normal-map asset.
   float2 texel=max(_MainTex_TexelSize.xy*2.0,float2(.0005,.0005));
   float texDx=skinHeight(i.uv_MainTex+float2(texel.x,0))-skinHeight(i.uv_MainTex-float2(texel.x,0));
   float texDy=skinHeight(i.uv_MainTex+float2(0,texel.y))-skinHeight(i.uv_MainTex-float2(0,texel.y));
   o.Albedo=c.rgb*lerp(1,scaleLayer,scaleWeight);o.Normal=normalize(float3((dx*.42+texDx*1.8)*_SkinDetail,(dy*.42+texDy*1.8)*_SkinDetail,1));
   // Keep the ridges readable while making the recessed skin absorb light.
   // The lower smoothness is deliberate: the imported skin otherwise reads
   // as polished plastic when the blue key light crosses the shoulder.
   float roughness=saturate(cavity*_RoughnessVariation);
   o.Metallic=_Metallic;o.Smoothness=_Glossiness*lerp(.72,.34,roughness);o.Occlusion=lerp(1,lerp(.93,.63,roughness),_SkinDetail);
   // The contact follows a chest bone through recoil. Light a bounded patch
   // on the facing skin, keeping the legs, tail, texture and normal relief.
   float distanceToHit=length(i.worldPos-_ImpactPoint.xyz)/max(.01,_ImpactPoint.w);
   float facing=smoothstep(0,.45,dot(normalize(WorldNormalVector(i,o.Normal)),normalize(_ImpactDirection.xyz)));
   float patch=(1-smoothstep(.15,1,distanceToHit))*facing;
   float detail=.30+.70*dot(c.rgb,float3(.30,.59,.11));
   float3 worldNormal=normalize(WorldNormalVector(i,o.Normal));
   float3 toCamera=normalize(_WorldSpaceCameraPos-i.worldPos);
   float rim=pow(1-saturate(dot(worldNormal,toCamera)),max(.5,_RimPower));
   // A restrained low-angle amber fill ties the dark skin to the volcano
   // stage. Height attenuation keeps the head from becoming a glowing lamp;
   // the regular scene lights still provide the main illumination.
   float3 lavaDirection=normalize(float3(-.24,.55,.80));
   float lavaFacing=saturate(dot(worldNormal,lavaDirection));
   float height01=saturate((i.worldPos.y-_DissolveBounds.x)/max(.01,_DissolveBounds.y));
   float lavaHeight=1-smoothstep(.18,.92,height01);
   float lavaBounce=pow(lavaFacing,1.35)*(.28+.72*lavaHeight);
   o.Emission=_EmissionColor.rgb+_ImpactColor.rgb*(_ImpactColor.a*patch*detail)+float3(2.6,1.1,.16)*edge
       +_RimColor.rgb*rim*_RimStrength+float3(1.0,.12,.018)*(.045*_LavaFill*lavaBounce);o.Alpha=c.a;
  }
  ENDCG
 }
 FallBack "Standard"
}
