Shader "Training/HeroSurface" {
 Properties {
  _MainTex("Original suit",2D)="white"{} _Color("Tint",Color)=(1,1,1,1)
  _Metallic("Metal",Range(0,1))=.2 _Glossiness("Smoothness",Range(0,1))=.42
  _TextureArmor("Silver material separation",Range(0,1))=0
  _CostumeFinish("Tiga costume finish",Range(0,1))=0
  _CostumeOcclusion("Local costume cavities",2D)="white"{}
  _EmissionColor("Base emission",Color)=(0,0,0,0)
  _GuardPoint("Shield contact and radius",Vector)=(0,0,0,1)
  _GuardColor("Reflected shield light",Color)=(0,0,0,0)
  _AtlasLights("Lenses in body atlas",Float)=0
  _EyeRegion("Eye UV bounds",Vector)=(0,0,0,0)
  _CoreRegion("Core UV bounds",Vector)=(0,0,0,0)
  _WarmEyes("Warm eye texture",Float)=0
  _EyeRadiance("Eye light",Float)=0 _CoreRadiance("Core light",Float)=0
  [HideInInspector] _EmissionAudit("Emission inspection",Float)=0
  _SrcBlend("Source",Float)=1 _DstBlend("Destination",Float)=0 _ZWrite("Depth",Float)=1
 }
 SubShader {
  Tags {"RenderType"="Opaque"} Blend [_SrcBlend] [_DstBlend] ZWrite [_ZWrite]
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows addshadow keepalpha finalcolor:FadeAdditive
  #pragma target 3.0
  sampler2D _MainTex,_CostumeOcclusion;fixed4 _Color,_EmissionColor;float4 _GuardPoint;
  half _CostumeFinish;
  half4 _GuardColor;half _Metallic,_Glossiness,_TextureArmor,_EmissionAudit;
  float4 _EyeRegion,_CoreRegion;half _AtlasLights,_WarmEyes,_EyeRadiance,_CoreRadiance;
  struct Input {float2 uv_MainTex;float3 worldPos;float3 worldNormal;INTERNAL_DATA};
  float AtlasRegion(float2 uv,float4 region) {
   float2 feather=max(fwidth(uv),float2(.001,.001));
   float2 inside=smoothstep(region.xy,region.xy+feather,uv)*(1-smoothstep(region.zw-feather,region.zw,uv));
   return inside.x*inside.y;
  }
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
   // Panel/lens masks were measured from the original display-space atlas.
   // Only classification uses that space; lighting retains linear albedo.
   float3 maskColor=c.rgb;
   #ifndef UNITY_COLORSPACE_GAMMA
    maskColor=LinearToGammaSpace(maskColor);
   #endif
   float brightest=max(maskColor.r,max(maskColor.g,maskColor.b)),darkest=min(maskColor.r,min(maskColor.g,maskColor.b));
   float saturation=(brightest-darkest)/max(.03,brightest);
   // Separate bright neutral armor from colored suit panels using the
   // original texture. Color borders are not invented as surface normals.
   float silver=(1-smoothstep(.12,.35,saturation))*smoothstep(.15,.65,brightest)*_TextureArmor;
   // The silver panels are painted costume/armor, not polished chrome.
   // Broader reflections leave the suit's curves legible between spot lights.
   o.Albedo=c.rgb;o.Metallic=lerp(_Metallic,.42,silver);o.Smoothness=lerp(_Glossiness,.43,silver);
   if(_CostumeFinish>0) {
    // The original UV colors define the panels exactly. Calibrate their
    // reflectance as dyed costume fabric, rather than pure RGB paint.
    float red=smoothstep(.15,.45,maskColor.r-max(maskColor.g,maskColor.b));
    float purple=smoothstep(.15,.40,maskColor.b-maskColor.g)*(1-red)*saturation;
    float3 ruby=float3(.64,.065,.115),violet=float3(.34,.14,.48);
    #ifndef UNITY_COLORSPACE_GAMMA
     ruby=GammaToLinearSpace(ruby);violet=GammaToLinearSpace(violet);
    #endif
    float3 dyed=lerp(lerp(c.rgb*.86,ruby,red),violet,purple);
    o.Albedo=lerp(c.rgb,dyed,_CostumeFinish);
    // UV-attached micro weave is filtered away below pixel scale. It must
    // neither swim through the moving skin nor sparkle in distant views.
    float2 yarn=i.uv_MainTex*320;
    float2 filtered=1-smoothstep(.25,.65,fwidth(yarn));
    float2 weave=sin(yarn*6.283185)*filtered;
    float fabric=(1-silver)*_CostumeFinish;
    o.Normal=normalize(float3(weave*.032*fabric,1));
    o.Smoothness=lerp(o.Smoothness,.25+.035*weave.x*weave.y,fabric);
    o.Occlusion=lerp(1,tex2D(_CostumeOcclusion,i.uv_MainTex).r,.72*_CostumeFinish);
   }
   float3 toward=_GuardPoint.xyz-i.worldPos;
   float distanceToLight=length(toward)/max(.01,_GuardPoint.w);
   float facing=smoothstep(-.15,.65,dot(normalize(WorldNormalVector(i,float3(0,0,1))),normalize(toward+float3(0,.00001,0))));
   float patch=(1-smoothstep(.12,1,distanceToLight))*facing;
   // A small local reflection retains the original panels; distant legs and
   // the back do not turn into the same luminous blue as the shield.
   // UV bounds isolate the original lens islands; color rejection keeps their
   // dark borders and neighboring silver/orange suit pixels non-emissive.
   float blue=smoothstep(.02,.18,maskColor.b-maskColor.r)*smoothstep(.22,.55,maskColor.b);
   float warm=smoothstep(.36,.58,min(maskColor.r,maskColor.g))*smoothstep(-.08,.03,maskColor.r-maskColor.b);
   // The cyan lenses contain near-white baked highlights. Chroma alone
   // excludes those highlights and produces dark islands inside a bright eye.
   float coolEye=max(blue,smoothstep(.55,.75,min(maskColor.r,min(maskColor.g,maskColor.b))));
   float eyes=AtlasRegion(i.uv_MainTex,_EyeRegion)*lerp(coolEye,warm,_WarmEyes);
   float core=AtlasRegion(i.uv_MainTex,_CoreRegion)*blue;
   o.Emission=_EmissionColor.rgb+_GuardColor.rgb*(_GuardColor.a*patch)*(.18+c.rgb*.55)
       +c.rgb*(_AtlasLights*(_EyeRadiance*eyes+_CoreRadiance*core));
   o.Alpha=c.a;
  }
  ENDCG
 }
 FallBack "Standard"
}
