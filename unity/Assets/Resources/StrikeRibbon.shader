Shader "Training/StrikeRibbon" {
 Properties { _Color("Color",Color)=(.45,.8,1,.6) }
 SubShader {
  Tags {"Queue"="Transparent+20" "RenderType"="Transparent"}
  Blend SrcAlpha One ZWrite Off Cull Off
  Pass { CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct Input {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
   struct Output {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
   fixed4 _Color;
   Output vert(Input v){
    Output o;
    // Place the air wake just outside the skin surface. Depth testing remains
    // enabled, so an arm on the far side cannot shine through the whole torso.
    float3 view=UnityObjectToViewPos(v.vertex);view.z+=.18;
    o.vertex=mul(UNITY_MATRIX_P,float4(view,1));o.uv=v.uv;o.color=v.color;return o;
   }
   fixed4 frag(Output i):SV_Target {
    float edge=saturate(1-abs(i.uv.y*2-1));
    float core=pow(edge,10);
    float wisps=.7+.3*sin(i.uv.y*37+i.uv.x*13);
    float alpha=(edge*edge*.48*wisps+core*.55)*i.color.a*_Color.a;
    return fixed4(lerp(_Color.rgb,float3(1,.98,.86),core*.55),alpha);
   }
  ENDCG }
 }
}
