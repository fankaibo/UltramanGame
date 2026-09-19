Shader "Training/EnergyShield" {
 Properties {
  _Color("Energy",Color)=(.12,.65,1,.7) _Clock("Clock",Float)=0
  _HitAge("Contact age",Float)=10 _HitPoint("Local contact",Vector)=(0,0,0,0)
 }
 SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" } Blend SrcAlpha One ZWrite Off Cull Back
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 float4 _Color,_HitPoint;float _Clock,_HitAge;
 struct v2f {float4 pos:SV_POSITION;float3 normal:TEXCOORD0;float3 world:TEXCOORD1;float2 local:TEXCOORD2;};
 v2f vert(appdata_base v) {v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.normal=UnityObjectToWorldNormal(v.normal);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.local=v.vertex.xy;return o;}
 float hexEdge(float2 p) {
  const float2 cell=float2(1,1.7320508);
  float2 a=p-cell*floor(p/cell+.5);
  float2 b=p-cell*.5-cell*floor((p-cell*.5)/cell+.5);
  float2 h=abs(dot(a,a)<dot(b,b)?a:b);
  float border=max(h.x,dot(h,float2(.5,.8660254)));
  return smoothstep(.5-max(fwidth(border)*1.2,.018),.5,border);
 }
 half4 frag(v2f i):SV_Target {
  float rim=pow(1-abs(dot(normalize(i.normal),normalize(_WorldSpaceCameraPos-i.world))),1.8);
  // Object-plane cells avoid the doubled, stretched latitude grid that made
  // the old sphere look like a wireframe. Keep the center transparent at rest.
  float cells=hexEdge(i.local*float2(11,12.4));
  float d=length((i.local-_HitPoint.xy)*float2(1,1.12));
  float age=max(0,_HitAge),fade=saturate(1-age/.58);
  float wave=exp(-pow((d-age*1.9)/.026,2))*fade;
  float echo=exp(-pow((d-age*1.3)/.018,2))*fade*.45;
  float core=exp(-d*d/ .013)*pow(saturate(1-age/.19),2);
  float localGlow=(wave+echo)*.85+core;
  float idlePulse=.86+.14*sin(_Clock*2.1);
  float alpha=_Color.a*(rim*.88+cells*(.032+localGlow*.5)+localGlow*.66+.009)*idlePulse;
  return half4(lerp(_Color.rgb*(1+rim*.6),float3(.76,.96,1),saturate(localGlow)),saturate(alpha));
 }
 ENDCG }
 }
}
