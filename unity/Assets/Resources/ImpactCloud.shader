Shader "Training/ImpactCloud" {
 Properties { _Color("Color",Color)=(1,.65,.28,1) _Age("Age",Range(0,1))=0 _Seed("Seed",Float)=0 _Hot("Hot",Float)=0 }
 SubShader {
  Tags {"Queue"="Transparent+15" "RenderType"="Transparent"}
  Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
  Pass { CGPROGRAM
   #pragma vertex vert_img
   #pragma fragment frag
   #include "UnityCG.cginc"
   fixed4 _Color;float _Age,_Seed,_Hot;
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y);}
   fixed4 frag(v2f_img i):SV_Target {
    float2 p=i.uv*2-1;float n=noise(p*3+_Seed+_Age*.6)*.6+noise(p*7-_Seed)*.4;
    float d=length(p)*(1+.16*sin(atan2(p.y,p.x)*7+_Seed));
    float density=saturate((1-d)*2.5+(n-.65)*1.5);
    float alpha=density*density*_Color.a*(1-_Age)*smoothstep(0,.07,_Age+.02);
    float ember=pow(saturate(1-_Age*2),2)*_Hot;
    float3 smoke=lerp(_Color.rgb*.3,_Color.rgb*.85,n)*lerp(.7,1.25,i.uv.y);
    return fixed4(lerp(smoke,float3(1.8,.8,.24),ember*saturate(density*1.4)),alpha);
   }
  ENDCG }
 }
}
