Shader "Training/GroundDust" {
 Properties { _Color("Ash",Color)=(.46,.43,.39,.66) _Age("Age",Range(0,1))=0 _Seed("Seed",Float)=0 }
 SubShader {
  Tags {"Queue"="Transparent+14" "RenderType"="Transparent"}
  Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
  Pass { CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fog
   #include "UnityCG.cginc"
   UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
   fixed4 _Color;float _Age,_Seed;
   struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float4 screen:TEXCOORD1;float eye:TEXCOORD2;UNITY_FOG_COORDS(3)};
   v2f vert(appdata_img v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord;o.screen=ComputeScreenPos(o.pos);o.eye=-UnityObjectToViewPos(v.vertex).z;UNITY_TRANSFER_FOG(o,o.pos);return o;}
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y);}
   fixed4 frag(v2f i):SV_Target {
    float2 p=i.uv*2-1;
    float n=noise(p*3.3+_Seed+float2(_Age*.7,-_Age*.35))*.66+noise(p*8.2-_Seed)*.34;
    float density=saturate((1-length(p))*2.2+(n-.52)*1.15);
    float life=smoothstep(0,.07,_Age)*(1-smoothstep(.26,1,_Age));
    float scene=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(i.screen)));
    float soft=saturate((scene-i.eye)/.20);
    fixed4 c=fixed4(_Color.rgb*lerp(.36,1.12,n)*lerp(.75,1.18,i.uv.y),density*density*_Color.a*life*soft);
    UNITY_APPLY_FOG(i.fogCoord,c);return c;
   }
  ENDCG }
 }
}
