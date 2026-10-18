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
  // The x axis is the projected incoming strike. Fragments scatter back
  // from the contact, while the white core dies before the warm tails.
  float2 p=i.uv;
  p.x+=_Age*.12;
  p.y*=1.16;
  float r=length(p),angle=atan2(p.y,p.x);
  float sector=(angle+UNITY_PI)/(2*UNITY_PI)*11;
  float n=floor(sector);
  float center=.26+hash(n+4)*.48;
  float across=abs(frac(sector)-center)*2;
  float tip=.36+.52*hash(n+3);
  float width=pow(saturate(1-across),2.0+hash(n+8)*5.0);
  float backscatter=lerp(.52,1,saturate(.5-p.x));
  float root=lerp(.045,.52,_Age)*(.6+hash(n+12)*.6);
  float tongue=width*(1-smoothstep(tip-.16,tip,r))*smoothstep(root-.04,root+.10,r)*backscatter;
  float fine=pow(saturate(1-across),24)*(1-smoothstep(tip-.06,tip+.03,r))*smoothstep(root-.02,root+.02,r);
  float core=exp(-r*r*100)*pow(saturate(1-_Age*2.8),1.4);
  float bloom=exp(-r*r*18)*pow(saturate(1-_Age*2),1.3);
  float fade=pow(saturate(1-_Age),1.15);
  float alpha=saturate(core*3.2+bloom*.9+(tongue*1.6+fine*.65)*fade)*_Power;
  float hot=saturate(core*2.8+bloom+pow(saturate(1-r*2.3),3));
  float3 color=lerp(float3(1.6,.32,.045),float3(2.4,2.2,1.85),hot);
  return half4(color,alpha*(1-smoothstep(.87,1,r)));
 }
 ENDCG }
 }
}
