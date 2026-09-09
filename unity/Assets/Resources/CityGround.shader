Shader "Training/CityGround" {
 Properties { _Color("Stone",Color)=(.075,.13,.23,1) }
 SubShader {
  Tags { "Queue"="Geometry+10" "RenderType"="Opaque" }
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows
  #pragma target 3.0
  #include "UnityCG.cginc"
  fixed4 _Color;
  struct Input { float3 worldPos; };
  float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
  float noise(float2 p) {
   float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
   return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
  }
  void surf(Input i,inout SurfaceOutputStandard o) {
   float2 p=i.worldPos.xz;
   // Large paving slabs, with staggered joints and a fine stone surface.
   float2 tile=p/1.8; tile.x+=step(1,fmod(floor(tile.y)+100,2))*.5;
   float2 edge=min(frac(tile),1-frac(tile));
   float seam=1-smoothstep(.0015,.008,min(edge.x,edge.y));
   float grain=noise(p*65)*.55+noise(p*8)*.3+noise(p*.8)*.15;
   float wet=smoothstep(.40,.72,noise(p*.42));
   o.Albedo=_Color.rgb*lerp(.82,1.2,grain)*(1-seam*.68);
   float n=noise(p*12),nx=noise(p*12+float2(.06,0)),ny=noise(p*12+float2(0,.06));
   o.Normal=normalize(float3((n-nx)*.35,(n-ny)*.35,1));
   o.Metallic=.22; o.Smoothness=lerp(.38,.79,wet)*(1-seam*.8);
   o.Occlusion=1-seam*.4;
  }
  ENDCG
 }
 FallBack "Standard"
}
