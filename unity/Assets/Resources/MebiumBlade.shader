Shader "Training/MebiumBlade" {
 Properties { _Age("Action time",Float)=0 _Heart("Hot core",Float)=0 _Opacity("Opacity",Float)=1 }
 SubShader {
  Tags {"Queue"="Transparent+12" "RenderType"="Transparent"}
  Blend SrcAlpha One ZWrite Off Cull Back
  Pass { CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct Input {float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;};
   struct Output {float4 vertex:SV_POSITION;float3 normal:TEXCOORD0;float3 view:TEXCOORD1;float2 uv:TEXCOORD2;};
   float _Age,_Heart,_Opacity;
   Output vert(Input v){Output o;o.vertex=UnityObjectToClipPos(v.vertex);float3 p=mul(unity_ObjectToWorld,v.vertex).xyz;o.view=_WorldSpaceCameraPos-p;o.normal=UnityObjectToWorldNormal(v.normal);o.uv=v.uv;return o;}
   fixed4 frag(Output i):SV_Target {
    float facing=abs(dot(normalize(i.normal),normalize(i.view)));
    float ribs=.5+.5*sin(i.uv.x*47-_Age*65+sin(i.uv.y*19+i.uv.x*13)*1.4);
    float flow=.82+.18*ribs;
    float3 amber=lerp(float3(1,.18,.012),float3(1,.69,.13),pow(facing,.6));
    float3 color=lerp(amber,float3(1,.96,.63),_Heart*.92);
    float alpha=lerp(.40+.38*facing,.93,_Heart)*flow*_Opacity;
    alpha*=smoothstep(0,.035,i.uv.x)*(1-smoothstep(.95,1,i.uv.x));
    return fixed4(color,alpha);
   }
  ENDCG }
 }
}
