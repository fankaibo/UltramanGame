Shader "Training/EnergyShield" {
 Properties {
  _Color("Energy",Color)=(.12,.65,1,.7) _Clock("Clock",Float)=0
  _HitAge("Contact age",Float)=10 _HitPoint("Local contact",Vector)=(0,0,0,0)
 }
 SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" } Blend SrcAlpha One ZWrite Off Cull Back
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 float4 _Color,_HitPoint;float _Clock,_HitAge;
 struct v2f {float4 pos:SV_POSITION;float3 normal:TEXCOORD0;float3 world:TEXCOORD1;float2 local:TEXCOORD2;};
 v2f vert(appdata_base v) {v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.normal=UnityObjectToWorldNormal(v.normal);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.local=v.vertex.xy;return o;}
 float hexEdge(float2 p) {
  const float2 cell=float2(1,1.7320508);
  float2 a=p-cell*floor(p/cell+.5);
  float2 b=p-cell*.5-cell*floor((p-cell*.5)/cell+.5);
  float2 h=abs(dot(a,a)<dot(b,b)?a:b);
  float border=max(h.x,dot(h,float2(.5,.8660254)));
  return smoothstep(.5-max(fwidth(border)*1.2,.018),.5,border);
 }
 half4 frag(v2f i):SV_Target {
  // A flattened sphere is seen obliquely during combat. A broad Fresnel
  // term filled most of it with blue at that angle and concealed the fight.
  float rim=pow(1-abs(dot(normalize(i.normal),normalize(_WorldSpaceCameraPos-i.world))),4.5);
  float radial=length(i.local*2);
  float angle=atan2(i.local.y,i.local.x);
  float flow=.62+.38*sin(angle*5-_Clock*1.6+sin(angle*11+_Clock)*.4);
  float edge=smoothstep(.945,.975,radial)*(1-smoothstep(.988,1.001,radial));
  float filament=edge*(.45+.55*flow);
  // The lattice only conducts an actual impact. At rest the center is a
  // nearly clear film, with the animated perimeter defining its boundary.
  float2 q=(i.local-_HitPoint.xy)*float2(1,1.12);
  float d=length(q),theta=atan2(q.y,q.x);
  float age=max(0,_HitAge),fade=saturate(1-age/.58);
  // A short pressure front breaks into conducting patches instead of two
  // complete concentric target rings. All motion uses the contact clock.
  float warp=(sin(theta*7+.8)*.014+sin(theta*13-1.2)*.009)*smoothstep(.035,.18,d);
  float path=d+warp;
  float sector=.30+.70*smoothstep(-.5,.65,sin(theta*9+sin(theta*4)*1.1));
  float front=exp(-pow((path-age*1.72)/.021,2))*fade*sector;
  float wake=exp(-pow((path-age*1.27)/.040,2))*fade*.26;
  float2 lattice=i.local*float2(11,12.4);
  float cells=hexEdge(lattice);
  // Cells brighten after the front reaches them, then die locally. The
  // barrier remains transparent between channels, including at impact.
  float arrived=age-path/1.72;
  float conduct=smoothstep(-.025,.015,arrived)*exp(-max(0,arrived)*13)*fade;
  float variation=.55+.45*sin(lattice.x*2.7+lattice.y*4.1+.8);
  float channels=cells*conduct*variation;
  float core=exp(-d*d/.008)*pow(saturate(1-age/.19),2);
  float localGlow=front*.85+wake+channels*.92+core*1.6;
  float alpha=_Color.a*(rim*.20+filament*.65+localGlow*.82+.002);
  float3 color=lerp(_Color.rgb,float3(.70,.94,1),saturate(localGlow+filament*.40));
  color+=float3(.32,.52,.62)*(channels*.60+core*.80);
  return half4(color,saturate(alpha));
 }
 ENDCG }
 }
}
