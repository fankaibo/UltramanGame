Shader "Training/VolcanicSpark" {
 SubShader {
  Tags {"Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True"}
  Blend One One ZWrite Off Cull Off
  Pass { CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
   float hash21(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   half4 frag(v2f i):SV_Target {
    // The ejecta mesh is a short camera-facing ribbon. Treat its UV.y as the
    // flight direction and shape it like a hot ember with a tapered tail,
    // rather than drawing a uniform red line through the whole ribbon.
    float across=abs(i.uv.x*2-1),along=saturate(i.uv.y);
    float breakup=.90+.10*sin(along*21+i.color.g*13+hash21(float2(i.color.g,along))*2);
    // Each flight reads as a rounded incandescent head followed by a short,
    // fading tail. This keeps the eruption organic at 1080p instead of making
    // a chain of square red lines when several ribbons overlap.
    float head=exp(-across*across*30-(along-.82)*(along-.82)*52)*breakup;
    float tail=exp(-across*across*42)*pow(along,.55)*(1-along)*.26;
    float halo=exp(-across*across*5.5-(along-.76)*(along-.76)*9)*.17;
    float energy=saturate(head*1.25+tail+halo);
    // Vertex RGB carries the fragment's current temperature. A fixed orange
    // palette kept cooling rocks white-hot and made every halo look alike.
    // Keep the hot center, but let the rock cool through red to dark crust.
    float3 radiance=i.color.rgb*lerp(.65,1.25,saturate(head*1.4));
    return half4(radiance*energy*i.color.a,0);
   }
  ENDCG }
 }
}
