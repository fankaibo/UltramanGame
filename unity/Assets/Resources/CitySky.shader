Shader "Training/CitySky" {
 SubShader { Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" } Cull Off ZWrite Off
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct v2f { float4 pos:SV_POSITION;float3 ray:TEXCOORD0; };
 v2f vert(float4 v:POSITION) {v2f o;o.pos=UnityObjectToClipPos(v);o.ray=v.xyz;return o;}
 half4 frag(v2f i):SV_Target {
  float3 d=normalize(i.ray);
  float3 c=lerp(float3(.11,.2,.36),float3(.015,.06,.19),smoothstep(0,.8,d.y));
  c=lerp(float3(.035,.05,.075),c,smoothstep(-.35,.04,d.y));
  float key=pow(saturate(dot(d,normalize(float3(-.7,.6,-.6)))),32);
  float rim=pow(saturate(dot(d,normalize(float3(.8,.35,.6)))),24);
  c+=float3(1.0,.7,.4)*key*2.5+float3(.12,.65,1)*rim*3;
  return half4(c,1);
 }
 ENDCG }
 }
}
