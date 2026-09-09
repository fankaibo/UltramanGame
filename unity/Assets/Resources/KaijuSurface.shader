Shader "Training/KaijuSurface" {
 Properties {
  _MainTex("Original skin",2D)="white"{} _Color("Tint",Color)=(1,1,1,1)
  _Metallic("Metal",Range(0,1))=.03 _Glossiness("Smoothness",Range(0,1))=.26
  _SrcBlend("Source",Float)=1 _DstBlend("Destination",Float)=0 _ZWrite("Depth",Float)=1
 }
 SubShader {
  Tags {"RenderType"="Opaque"} Blend [_SrcBlend] [_DstBlend] ZWrite [_ZWrite]
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows addshadow keepalpha finalcolor:FadeAdditive
  #pragma target 3.0
  sampler2D _MainTex;float4 _MainTex_TexelSize;fixed4 _Color;half _Metallic,_Glossiness;
  struct Input {float2 uv_MainTex;};
  float hash(float2 p) {return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
  float cell(float2 p) {
   float2 ip=floor(p),f=frac(p);float d=1;
   for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++){
    float2 g=float2(x,y);float2 r=g+float2(hash(ip+g),hash(ip+g+37))-f;d=min(d,dot(r,r));}
   return sqrt(d);
  }
  float relief(float2 p) {return smoothstep(.03,.6,cell(p));}
  void FadeAdditive(Input i,SurfaceOutputStandard o,inout fixed4 color) {
   #ifdef UNITY_PASS_FORWARDADD
    color.rgb*=o.Alpha;
   #endif
  }
  void surf(Input i,inout SurfaceOutputStandard o) {
   fixed4 c=tex2D(_MainTex,i.uv_MainTex)*_Color;
   float2 p=i.uv_MainTex*float2(35,140);
   float n=relief(p),dx=relief(p+float2(.08,0))-n,dy=relief(p+float2(0,.08))-n;
   o.Albedo=c.rgb*lerp(.88,1.05,n);o.Normal=normalize(float3(dx*.65,dy*.65,1));
   o.Metallic=_Metallic;o.Smoothness=_Glossiness*lerp(.65,1.15,n);o.Occlusion=lerp(.86,1,n);o.Alpha=c.a;
  }
  ENDCG
 }
 FallBack "Standard"
}
