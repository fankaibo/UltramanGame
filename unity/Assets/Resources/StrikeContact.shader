Shader "Training/StrikeContact" {
 Properties { _Age("Normalized age",Range(0,1))=0 _Seed("Variation",Float)=0 _Power("Power",Float)=1 }
 SubShader { Tags { "Queue"="Transparent+16" "RenderType"="Transparent" }
 Blend SrcAlpha One ZWrite Off Cull Off
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 float _Age,_Seed,_Power;
 struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
 v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord.xy*2-1;return o;}
 float hash(float n){return frac(sin(n*127.1+_Seed*311.7)*43758.5453);}
 half4 frag(v2f i):SV_Target {
  // Irregular tapered tongues replace the perfectly circular contact ring.
  // The narrow white core dies first; warm fragments separate and fade.
  float2 p=i.uv; p.y*=1.2;
  float r=length(p),angle=atan2(p.y,p.x);
  float sector=(angle+UNITY_PI)/(2*UNITY_PI)*12;
  float n=floor(sector),across=abs(frac(sector)-.5)*2;
  float tip=.43+.48*hash(n+3);
  float width=pow(saturate(1-across),1.4+hash(n+8)*2.3);
  float tongue=width*(1-smoothstep(tip-.12,tip,r))*smoothstep(.05,.19,r);
  // The cabinet contact reads as a hot core followed by a thin expanding
  // shock ring.  Keep the irregular tongues, then add the two temporal
  // layers so a hit is still readable when the fist overlaps a dark mesh.
  float split=smoothstep(_Age*.34-.08,_Age*.34+.06,r);
  float fine=pow(saturate(1-across),18)*(1-smoothstep(tip-.08,tip+.08,r));
  float core=exp(-r*r*120)*pow(saturate(1-_Age*1.65),1.25);
  float bloom=exp(-r*r*15)*pow(saturate(1-_Age*1.18),1.10);
  float ringRadius=lerp(.10,.72,smoothstep(.02,.60,_Age));
  float ring=exp(-pow((r-ringRadius)*19,2))*pow(saturate(1-_Age),1.18);
  float ringGlow=exp(-pow((r-ringRadius)*7,2))*.23*pow(saturate(1-_Age),1.35);
  float haze=exp(-r*r*5.8)*.17;
  float fade=pow(saturate(1-_Age),1.34);
  float alpha=saturate((tongue*split+fine*.5+core*2.8+bloom*.58+ring*.72+ringGlow+haze)*fade)*_Power;
  float hot=saturate(core*2.6+bloom*.85+ring*.65+pow(saturate(1-r*1.9),3)*.6);
  float3 color=lerp(float3(1.62,.27,.035),float3(2.25,1.95,1.48),hot);
  return half4(color,alpha*(1-smoothstep(.87,1,r)));
 }
 ENDCG }
 }
}
