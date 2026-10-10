Shader "Training/SkillEnergy" {
 Properties { _Color("Energy",Color)=(.2,.65,1,1) _Age("Clock",Float)=0 }
 SubShader { Tags {"Queue"="Transparent+8" "RenderType"="Transparent"}
  Blend SrcAlpha One ZWrite Off Cull Back
  Pass { CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct v2f {float4 pos:SV_POSITION;float3 normal:TEXCOORD0;float3 view:TEXCOORD1;float3 local:TEXCOORD2;};
   float4 _Color;float _Age;
   v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.normal=UnityObjectToWorldNormal(v.normal);o.view=_WorldSpaceCameraPos-mul(unity_ObjectToWorld,v.vertex).xyz;o.local=v.vertex.xyz;return o;}
   half4 frag(v2f i):SV_Target {
    float facing=saturate(dot(normalize(i.normal),normalize(i.view)));
    float bands=.72+.28*sin(i.local.z*24+atan2(i.local.x,i.local.y)*3-_Age*48);
    float core=pow(facing,5);
    float shell=pow(facing,.65)*bands;
    return half4(lerp(_Color.rgb,float3(1,.98,.90),core*.85),_Color.a*(shell*.65+core*.75));
   }
  ENDCG }
 }
}
