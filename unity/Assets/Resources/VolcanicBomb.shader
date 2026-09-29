Shader "Training/VolcanicBomb" {
 Properties { [HideInInspector] _MainTex("Rock coordinates",2D)="white"{} }
 SubShader {
  Tags {"RenderType"="Opaque"}
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows
  #pragma target 3.0
  sampler2D _MainTex;
  struct Input {float2 uv_MainTex;float4 color:COLOR;};
  float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
  float noise(float2 p){float2 n=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(n),hash(n+float2(1,0)),f.x),lerp(hash(n+float2(0,1)),hash(n+1),f.x),f.y);}
  void surf(Input i,inout SurfaceOutputStandard o){
   float n=noise(i.uv_MainTex*8+i.color.g*29),detail=noise(i.uv_MainTex*27+7);
   float heat=saturate(i.color.r),cracks=1-smoothstep(.04,.15,abs(n-.5));
   // Integrate subpixel crust detail rather than showing a blinking orange
   // wire pattern when a distant fragment covers only a handful of pixels.
   float footprint=max(length(ddx(i.uv_MainTex*8)),length(ddy(i.uv_MainTex*8)));
   float detailWeight=1-smoothstep(.45,1.6,footprint);
   cracks=lerp(.38,cracks,detailWeight);detail=lerp(.5,detail,detailWeight);
   float3 fire=lerp(float3(.6,.025,.001),float3(3.2,.78,.07),heat*heat);
   o.Albedo=lerp(float3(.021,.018,.017),float3(.095,.065,.048),detail);
   o.Emission=fire*pow(heat,1.3)*(.12+.88*cracks);
   o.Metallic=0;o.Smoothness=.10;o.Occlusion=.75+.25*detail;
  }
  ENDCG
 }
 FallBack "Standard"
}
