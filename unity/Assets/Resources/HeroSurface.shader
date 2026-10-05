Shader "Training/HeroSurface" {
 Properties {
  _MainTex("Original suit",2D)="white"{} _Color("Tint",Color)=(1,1,1,1)
  _Metallic("Metal",Range(0,1))=.2 _Glossiness("Smoothness",Range(0,1))=.42
  _RimColor("Arcade rim color",Color)=(.12,.48,1,1)
  _RimPower("Arcade rim falloff",Range(0.5,8))=3.2 _RimStrength("Arcade rim strength",Range(0,2))=.18
  _TextureArmor("Silver material separation",Range(0,1))=0
  _MicroDetail("Suit micro detail",Range(0,2))=.35
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
  sampler2D _MainTex,_CostumeOcclusion;fixed4 _Color,_EmissionColor,_RimColor;float4 _GuardPoint;
  half _CostumeFinish;
  half4 _GuardColor;half _Metallic,_Glossiness,_TextureArmor,_MicroDetail,_EmissionAudit,_RimPower,_RimStrength;
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
   // Small source atlases (especially the 256px Geed/Grigio textures) need a
   // stable material cue at the 45-degree battle distance. This restrained,
   // UV-attached grain adds cloth/paint breakup without inventing geometry.
   float2 grain=i.uv_MainTex*180;
   float grainWave=(sin(grain.x*6.283185)+sin(grain.y*6.283185*1.17))*.5;
   float grainMask=saturate(1-silver)*_MicroDetail;
   c.rgb*=1+grainWave*.028*grainMask;
   // The non-Tiga source suits have smooth low-resolution albedo maps. Add a
   // second, filtered scale of breakup so they read as fabric/paint at the
   // 45-degree arcade camera distance. The amplitude stays deliberately
   // small: this improves material separation without pretending to add mesh
   // detail or changing the measured Tiga finish below.
   float2 detailUv=i.uv_MainTex*72;
   float detailA=sin(detailUv.x*6.283185+sin(detailUv.y*2.1));
   float detailB=sin(detailUv.y*6.283185*1.13+sin(detailUv.x*1.7));
   float detailFilter=saturate(1-smoothstep(.18,.62,max(fwidth(detailUv.x),fwidth(detailUv.y))));
   float micro=(detailA*.58+detailB*.42)*detailFilter;
   float microMask=(1-silver)*_MicroDetail;
   c.rgb*=1+micro*.022*microMask;
   o.Albedo=c.rgb;o.Metallic=lerp(_Metallic,.42,silver);
   o.Smoothness=lerp(_Glossiness,.43,silver)-abs(micro)*.035*microMask;
   // The downloaded hero atlases do not ship a normal map. Recover a very
   // small amount of panel/paint relief from the filtered albedo derivative
   // so silver seams and colored inserts catch the same key light as the Tiga
   // costume. GPU derivatives reuse the current texture sample (four extra
   // neighbor reads made the first 1080P frame needlessly expensive).
   float luma=dot(c.rgb,float3(.30,.59,.11));
   float2 reliefGradient=float2(ddx(luma),ddy(luma));
   float reliefStrength=(.055+.065*_MicroDetail)*saturate(1-silver*.55);
   float2 relief=reliefGradient*reliefStrength;
   o.Normal=normalize(float3(micro*.028*microMask+relief.x,micro*.021*microMask+relief.y,1));
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
    o.Normal=normalize(float3(weave*.032*fabric+grainWave*.012*grainMask,1));
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
   // A restrained Fresnel rim separates the suit from the dark Fuji plate.
   // It is view-dependent, so the edge moves naturally with the close camera
   // rather than painting a fixed outline into the texture.
   float3 worldNormal=normalize(WorldNormalVector(i,float3(0,0,1)));
   float3 toCamera=normalize(_WorldSpaceCameraPos-i.worldPos);
   float rim=pow(1-saturate(dot(worldNormal,toCamera)),max(.5,_RimPower));
   o.Emission+=_RimColor.rgb*rim*_RimStrength;
   o.Alpha=c.a;
  }
  ENDCG
 }
 FallBack "Standard"
}
