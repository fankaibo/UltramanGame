Shader "Training/DissolveMotes" {
 Properties {_Color("Light",Color)=(2.3,1.1,.22,1) _Age("Victory clock",Float)=-10}
 SubShader {
  Tags {"Queue"="Transparent" "RenderType"="Transparent"} Blend SrcAlpha One ZWrite Off Cull Off
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   float4 _Color;float _Age;
   struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;float2 birth:TEXCOORD1;float2 size:TEXCOORD2;};
   struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float alpha:TEXCOORD1;};
   v2f vert(appdata v) {
    v2f o;float age=_Age-v.birth.x,life=v.birth.y,t=max(0,age);
    float3 p=mul(unity_ObjectToWorld,v.vertex).xyz;
    p+=v.normal*t+float3(sin(t*5+v.size.x)-sin(v.size.x),0,cos(t*4+v.size.x)-cos(v.size.x))*.055;
    p.y+=.28*t*t;
    float2 corner=(v.uv-.5)*2;
    float scale=v.size.y*(.8+.2*sin(t*9+v.size.x));
    float3 right=UNITY_MATRIX_V._m00_m01_m02,up=UNITY_MATRIX_V._m10_m11_m12;
    p+=right*corner.x*scale+up*corner.y*scale*1.45;
    o.pos=mul(UNITY_MATRIX_VP,float4(p,1));o.uv=corner;
    o.alpha=step(0,age)*step(age,life)*smoothstep(0,.035,t)*(1-smoothstep(life*.35,life,t));
    return o;
   }
   half4 frag(v2f i):SV_Target {
    float r=dot(i.uv,i.uv);float core=exp(-r*7),halo=pow(saturate(1-r),3)*.32;
    return half4(_Color.rgb*(.7+core*.6),(core+halo)*i.alpha*_Color.a);
   }
   ENDCG
  }
 }
}
