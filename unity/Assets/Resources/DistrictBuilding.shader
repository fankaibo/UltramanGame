Shader "Training/DistrictBuilding" {
 Properties { _Color("Facade",Color)=(.10,.18,.26,1) }
 SubShader {
  Tags {"RenderType"="Opaque"}
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows
  #pragma target 3.0
  fixed4 _Color;
  struct Input {float3 worldPos;float3 worldNormal;};
  float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
  void surf(Input i,inout SurfaceOutputStandard o){
   float side=step(abs(i.worldNormal.z),abs(i.worldNormal.x));
   float2 p=float2(lerp(i.worldPos.x,i.worldPos.z,side),i.worldPos.y);
   float2 cell=p/float2(.075,.11),f=frac(cell);
   float2 aa=max(fwidth(cell),.02);
   float pane=smoothstep(.12,.12+aa.x,f.x)*(1-smoothstep(.82-aa.x,.82,f.x))*smoothstep(.15,.15+aa.y,f.y)*(1-smoothstep(.78-aa.y,.78,f.y));
   pane*=1-step(.5,abs(i.worldNormal.y));
   float r=hash(floor(cell));float lit=step(.52,r)*pane;
   float3 light=lerp(float3(.16,.43,.58),float3(.95,.61,.26),step(.84,r));
   o.Albedo=lerp(_Color.rgb*.7,_Color.rgb*1.5,pane);
   o.Emission=light*lit*.56;o.Metallic=.42*pane;o.Smoothness=.4+.4*pane;
  }
  ENDCG
 }
 FallBack "Standard"
}
