Shader "Training/VolcanoReflection" {
 SubShader { Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" } Cull Off ZWrite Off
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct v2f {float4 pos:SV_POSITION;float3 ray:TEXCOORD0;};
 v2f vert(float4 v:POSITION){v2f o;o.pos=UnityObjectToClipPos(v);o.ray=v.xyz;return o;}
 half4 frag(v2f i):SV_Target {
  float3 d=normalize(i.ray);
  // Cool overcast sky, dim basalt below, and warm light along the far vents.
  // Broad sources reveal curved armor instead of drawing hard chrome dots.
  float3 sky=lerp(float3(.17,.22,.31),float3(.09,.14,.23),smoothstep(.06,.95,d.y));
  float cloud=.94+.06*sin(d.x*7+d.z*5)*sin(d.y*11-d.z*4);
  float3 c=lerp(float3(.12,.09,.075),sky*cloud,smoothstep(-.20,.12,d.y));
  float moon=pow(saturate(dot(d,normalize(float3(-.60,.62,-.51)))),9);
  float fill=pow(saturate(dot(d,normalize(float3(.20,.32,-.93)))),5);
  c+=float3(.62,.69,.80)*moon*.72+float3(.30,.34,.42)*fill*.35;
  float horizon=exp(-pow((d.y+.02)/.23,2));
  float lava=pow(saturate(dot(normalize(d.xz+float2(.00001,0)),normalize(float2(-.42,1)))),7);
  c+=float3(.60,.18,.045)*horizon*lava*.65;
  // These radiance values are linear. The arena currently uses gamma
  // rendering, so its reflection target needs the same encoding as the
  // built-in sky. Otherwise silver armor loses most of its indirect light.
  #ifdef UNITY_COLORSPACE_GAMMA
   c=LinearToGammaSpace(c);
  #endif
  return half4(c,1);
 }
 ENDCG }
 }
}
