Shader "Training/ProjectileWake" {
 Properties { _Color("Energy",Color)=(.18,.7,1,.85) _Age("Shot clock",Float)=0 }
 SubShader { Tags {"Queue"="Transparent+20" "RenderType"="Transparent"}
  Blend SrcAlpha One ZWrite Off ZTest LEqual Cull Off
  Pass { CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct Input {float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   struct Output {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   float4 _Color;float _Age;
   Output vert(Input v){Output o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
   half4 frag(Output i):SV_Target {
    float x=saturate(i.uv.x),y=i.uv.y*2-1;
    // The leading end merges with the moving volume. A soft, turbulent
    // envelope widens behind it; there are no opaque discs or outlined rings.
    // Taper in UV space on a constant-width strip. A two-triangle geometric
    // taper bends the interpolated UV center across the diagonal seam.
    float center=y/(.12+.88*x);
    float hot=exp(-center*center*65);
    float envelope=exp(-center*center*5.5);
    float ends=smoothstep(0,.28,x)*(1-smoothstep(.92,1,x));
    float threads=.82+.18*sin(y*18+x*31-_Age*56);
    float alpha=(hot*.86+envelope*.30*threads)*ends*_Color.a*i.color.a;
    return half4(lerp(_Color.rgb*1.35,float3(1.8,1.92,2),hot*.82),alpha);
   }
  ENDCG }
 }
}
