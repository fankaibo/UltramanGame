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
    float drift=sin(x*15-_Age*42)*.045*(1-x);
    float center=(y-drift)/(.20+.64*x);
    float hot=exp(-center*center*95)*smoothstep(.48,.94,x);
    // Shear the cooler wake into moving strands. The leading hot core joins
    // the volume, while the fading rear no longer reads as a rigid laser rod.
    float strandA=exp(-pow((center-.10*sin(x*23-_Age*57))*13,2));
    float strandB=exp(-pow((center+.17*sin(x*17-_Age*43+1.7))*11,2));
    float envelope=exp(-center*center*6.5);
    float ends=smoothstep(0,.26,x)*(1-smoothstep(.94,1,x));
    float breaks=.48+.52*smoothstep(-.5,.8,sin(x*38-_Age*67));
    float alpha=(hot*.65+(strandA+strandB)*.21*breaks+envelope*.19)*ends*_Color.a*i.color.a;
    return half4(lerp(_Color.rgb*1.4,float3(1.65,1.85,2),hot*.82),alpha);
   }
  ENDCG }
 }
}
