Shader "Training/SkillDischarge" {
 Properties { _Color("Energy",Color)=(.18,.7,1,1) _Age("Effect clock",Float)=0 _Impact("Contact",Float)=0 }
 SubShader { Tags {"Queue"="Transparent+8" "RenderType"="Transparent"}
  Blend One One ZWrite Off ZTest LEqual Cull Back
  Pass { CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #include "UnityCG.cginc"
   struct v2f {float4 pos:SV_POSITION;float3 local:TEXCOORD0;};
   float4 _Color;float _Age,_Impact;
   v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.local=v.vertex.xyz;return o;}
   float hash(float3 p){p=frac(p*.1031);p+=dot(p,p.yzx+33.33);return frac((p.x+p.y)*p.z);}
   float noise(float3 p){float3 q=floor(p),f=frac(p);f=f*f*(3-2*f);
    return lerp(lerp(lerp(hash(q),hash(q+float3(1,0,0)),f.x),lerp(hash(q+float3(0,1,0)),hash(q+float3(1,1,0)),f.x),f.y),
     lerp(lerp(hash(q+float3(0,0,1)),hash(q+float3(1,0,1)),f.x),lerp(hash(q+float3(0,1,1)),hash(q+1),f.x),f.y),f.z);}
   half4 frag(v2f i):SV_Target {
    float3 ro=mul(unity_WorldToObject,float4(_WorldSpaceCameraPos,1)).xyz;
    float3 rd=normalize(i.local-ro);float b=dot(ro,rd),d=b*b-dot(ro,ro)+.25;
    clip(d);float a=max(0,-b-sqrt(d)),end=-b+sqrt(d),stepSize=(end-a)/12;
    float flash=lerp(1,1-smoothstep(.025,.11,_Age),_Impact);float3 sum=0;
    [unroll] for(int n=0;n<12;n++) {
     float3 p=ro+rd*(a+(n+.5)*stepSize);
     float edge=saturate((.25-dot(p,p))*16),r2=dot(p.xy,p.xy);
     float flow=noise(p*11+float3(_Age*4,-_Age*8,_Age*19));
     float tongues=smoothstep(.18,.73,flow);
     // Contact expands back toward the incoming shot, leaving the monster
     // silhouette readable. No complete disc or evenly spaced radial rays.
     float outward=lerp(1,1-smoothstep(-.10,.10,p.z),_Impact);
     float cloud=exp(-r2*lerp(29,18,_Impact))*tongues*outward;
     float hot=exp(-r2*135-pow((p.z+.025)*8,2))*flash;
     sum+=(float3(1.55,1.7,1.85)*hot*6+_Color.rgb*cloud*lerp(2.8,4,_Impact))*edge*stepSize;
    }
    return half4(sum*_Color.a,0);
   }
  ENDCG }
 }
}
