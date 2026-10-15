Shader "Training/LightBullet" {
 Properties { _Color("Energy",Color)=(.18,.7,1,1) _Age("Shot clock",Float)=0 }
 SubShader { Tags {"Queue"="Transparent+8" "RenderType"="Transparent"}
  Blend One One ZWrite Off ZTest LEqual Cull Back
  Pass { CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #include "UnityCG.cginc"
   struct v2f {float4 pos:SV_POSITION;float3 local:TEXCOORD0;};
   float4 _Color;float _Age;
   v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.local=v.vertex.xyz;return o;}
   float hash(float3 p){p=frac(p*.1031);p+=dot(p,p.yzx+33.33);return frac((p.x+p.y)*p.z);}
   float noise(float3 p){float3 q=floor(p),f=frac(p);f=f*f*(3-2*f);
    return lerp(lerp(lerp(hash(q),hash(q+float3(1,0,0)),f.x),lerp(hash(q+float3(0,1,0)),hash(q+float3(1,1,0)),f.x),f.y),
     lerp(lerp(hash(q+float3(0,0,1)),hash(q+float3(1,0,1)),f.x),lerp(hash(q+float3(0,1,1)),hash(q+1),f.x),f.y),f.z);}
   half4 frag(v2f i):SV_Target {
    // Integrate through the existing unit sphere instead of lighting its
    // surface. This removes the solid ellipsoid rim at oblique camera angles.
    float3 ro=mul(unity_WorldToObject,float4(_WorldSpaceCameraPos,1)).xyz;
    float3 rd=normalize(i.local-ro);float b=dot(ro,rd),d=b*b-dot(ro,ro)+.25;
    clip(d);float a=max(0,-b-sqrt(d)),end=-b+sqrt(d),stepSize=(end-a)/12;
    float3 sum=0;
    [unroll] for(int n=0;n<12;n++) {
     float3 p=ro+rd*(a+(n+.5)*stepSize);
     float edge=saturate((.25-dot(p,p))*14);
     float2 drift=float2(sin(p.z*14-_Age*39),cos(p.z*19-_Age*31))*.022;
     float r2=dot(p.xy+drift,p.xy+drift);
     float axial=smoothstep(-.5,-.29,p.z)*(1-smoothstep(.20,.48,p.z));
     float flow=noise(p*13+float3(0,0,_Age*22));
     float hot=exp(-r2*190-pow((p.z-.11)*4.5,2));
     float cloud=exp(-r2*31)*smoothstep(.26,.72,flow)*axial;
     sum+=(float3(1.45,1.63,1.8)*hot*5+_Color.rgb*cloud*2.7)*edge*stepSize;
    }
    return half4(sum*_Color.a,0);
   }
  ENDCG }
 }
}
