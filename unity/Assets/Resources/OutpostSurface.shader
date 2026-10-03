Shader "Training/OutpostSurface" {
 Properties { _Color("Weathered surface",Color)=(.4,.4,.4,1) _Grain("Aggregate",2D)="gray"{} [Normal] _Normal("Scanned normal",2D)="bump"{} _ARM("AO roughness metal",2D)="white"{} _Scale("Surface size",Float)=2 _Metal("Metal",Float)=0 _Rough("Roughness",Float)=.85 _Rust("Oxidation",Float)=0 }
 SubShader {
  Tags {"RenderType"="Opaque"}
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows addshadow
  #pragma target 3.0
  #include "UnityCG.cginc"
  sampler2D _Grain,_Normal,_ARM;fixed4 _Color;float _Metal,_Rough,_Rust,_Scale;
  struct Input {float3 worldPos;float3 worldNormal;INTERNAL_DATA};
  float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
  float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
  void surf(Input i,inout SurfaceOutputStandard o){
   float3 geometric=normalize(WorldNormalVector(i,float3(0,0,1)));
   float3 w=pow(abs(geometric),4);w/=max(dot(w,1),.001);
   float3 p=i.worldPos*_Scale;
   float grain=dot(tex2D(_Grain,p.zy).rgb*w.x+tex2D(_Grain,p.xz).rgb*w.y+tex2D(_Grain,p.xy).rgb*w.z,float3(.2126,.7152,.0722));
   float deposits=noise(i.worldPos.xz*2.4+i.worldPos.y*.37);
   float streak=noise(float2(i.worldPos.x+i.worldPos.z,i.worldPos.y*.06)*26);
   float soot=smoothstep(.35,.84,deposits)*.28;
   float rust=smoothstep(.46,.72,deposits+streak*.17)*_Rust;
   float3 baseColor=_Color.rgb*lerp(.35,1.5,saturate(grain*2))*(1-soot);
   float3 oxide=float3(.27,.115,.05);
   #ifndef UNITY_COLORSPACE_GAMMA
    oxide=GammaToLinearSpace(oxide);
   #endif
   o.Albedo=lerp(baseColor,oxide,rust*.64);
   float3 nx=UnpackNormal(tex2D(_Normal,p.zy)),ny=UnpackNormal(tex2D(_Normal,p.xz)),nz=UnpackNormal(tex2D(_Normal,p.xy));
   float3 perturb=float3(0,nx.y,nx.x)*w.x+float3(ny.x,0,ny.y)*w.y+float3(nz.x,nz.y,0)*w.z;
   perturb-=geometric*dot(perturb,geometric);
   float3 normal=normalize(geometric+perturb*.65);
   o.Normal=float3(dot(normal,WorldNormalVector(i,float3(1,0,0))),dot(normal,WorldNormalVector(i,float3(0,1,0))),dot(normal,geometric));
   float3 arm=tex2D(_ARM,p.zy).rgb*w.x+tex2D(_ARM,p.xz).rgb*w.y+tex2D(_ARM,p.xy).rgb*w.z;
   o.Metallic=_Metal*(1-rust);o.Smoothness=(1-_Rough)*(1-soot*.7)*(1-arm.g*.5);o.Occlusion=lerp(.30,1,arm.r);
  }
  ENDCG
 }
 FallBack "Standard"
}
