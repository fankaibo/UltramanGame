Shader "Training/EnergyShield" {
 Properties { _Color("Energy",Color)=(.12,.65,1,.7) _Clock("Clock",Float)=0 }
 SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" } Blend SrcAlpha One ZWrite Off Cull Off
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 float4 _Color;float _Clock;
 struct v2f {float4 pos:SV_POSITION;float3 normal:TEXCOORD0;float3 world:TEXCOORD1;float2 uv:TEXCOORD2;};
 v2f vert(appdata_base v) {v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.normal=UnityObjectToWorldNormal(v.normal);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.uv=v.texcoord.xy;return o;}
 half4 frag(v2f i):SV_Target {
  float rim=pow(1-abs(dot(normalize(i.normal),normalize(_WorldSpaceCameraPos-i.world))),2.7);
  float scan=pow(saturate(sin(i.world.y*18-_Clock*4)),30)*.2;
  float2 p=i.uv*float2(40,24);p.x+=fmod(floor(p.y),2)*.5;
  float2 e=min(frac(p),1-frac(p));float grid=(1-smoothstep(.025,.07,min(e.x,e.y)))*.10;
  return half4(_Color.rgb*(1+rim),_Color.a*(rim*.8+scan+grid+.025));
 }
 ENDCG }
 }
}
