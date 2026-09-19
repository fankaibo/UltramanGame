Shader "Training/VolcanicPlume" {
 Properties { _Seed("Seed",Float)=0 _Age("Age",Float)=0 _Clock("Clock",Float)=0 }
 SubShader {
  Tags {"Queue"="Transparent" "RenderType"="Transparent"}
  Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
  Pass { CGPROGRAM
   #pragma vertex vert_img
   #pragma fragment frag
   #include "UnityCG.cginc"
   float _Seed,_Age,_Clock;
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y);}
   fixed4 frag(v2f_img i):SV_Target {
    float2 p=i.uv*2-1;
    float n=noise(p*2.7+_Seed+float2(_Clock*.12,-_Clock*.23))*.6+noise(p*6.8-_Seed+_Clock*.11)*.28+noise(p*14+_Seed)*.12;
    // Layered noise keeps the cloud edge irregular instead of reading as a
    // translucent geometric disc.  Slightly stronger density makes the plume
    // visible against the dark Fuji sky without obscuring the fighters.
    float density=saturate((1-length(p))*3.1+(n-.57)*1.8);
    float opacity=density*density*smoothstep(0,.10,_Age+.035)*(1-smoothstep(.60,1,_Age))*.62;
    float hot=pow(1-saturate(_Age*3.2),2)*saturate(n*1.3);
    float3 smoke=lerp(float3(.075,.08,.09),float3(.29,.28,.27),n)*lerp(.7,1.1,i.uv.y);
    return fixed4(lerp(smoke,float3(2.6,.60,.065),hot),opacity);
   }
  ENDCG }
 }
}
