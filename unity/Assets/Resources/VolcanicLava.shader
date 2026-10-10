Shader "Training/VolcanicLava" {
 Properties { [HideInInspector] _MainTex("Coordinates",2D)="white"{} _Clock("Flow clock",Float)=0 _Pool("Crater pool",Float)=0 }
 SubShader {
  Tags { "RenderType"="Opaque" }
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows
  #pragma target 3.0
  float _Clock,_Pool;
  float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
  float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
  struct Input {float2 uv_MainTex;float3 worldPos;};
  sampler2D _MainTex;
  void surf(Input i,inout SurfaceOutputStandard o){
   float2 p=i.worldPos.xz;
   float2 drift=p+float2(.08,-.055)*_Clock;
   float n=noise(drift*5),detail=noise(drift*19);
   float across=lerp(abs(i.uv_MainTex.x*2-1),length(i.uv_MainTex*2-1),_Pool);
   float bank=smoothstep(.36,.85,across+(noise(p*13)-.5)*.22);
   float broken=1-smoothstep(.045,.13,abs(n-.49));
   float heat=(.20+broken*.8)*(1-bank)*lerp(.72,1,detail);
   o.Albedo=lerp(float3(.038,.033,.031),float3(.085,.055,.037),n)*(1-bank*.32);
   float ridge=noise(p*29);o.Normal=normalize(float3((ridge-.5)*.6,(detail-.5)*.6,1));
   o.Emission=float3(3.1,.48,.038)*heat*(.9+.1*sin(_Clock*.65+p.y));
   o.Metallic=.02;o.Smoothness=.07+heat*.22;o.Occlusion=.65+.35*ridge;
  }
  ENDCG
 }
 FallBack "Standard"
}
