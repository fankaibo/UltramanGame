Shader "Training/RangedImpactSmoke" {
 Properties { _Noise("Density lattice",3D)="white"{} _Age("Normalized contact age",Float)=0 _Opacity("Opacity",Float)=1 _Seed("Seed",Float)=0 _Tint("Cooling energy",Color)=(.18,.7,1,1) }
 SubShader {
  Tags {"Queue"="Transparent-9" "RenderType"="Transparent" "IgnoreProjector"="True"}
  Blend One OneMinusSrcAlpha ZWrite Off ZTest Always Cull Front
  Pass { CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #include "UnityCG.cginc"
   UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
   sampler3D _Noise;float _Age,_Opacity,_Seed;float4 _Tint;
   struct v2f {float4 pos:SV_POSITION;float3 world:TEXCOORD0;float4 screen:TEXCOORD1;};
   v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.screen=ComputeScreenPos(o.pos);return o;}
   float noise(float3 p){float3 f=frac(p);f=f*f*(3-2*f);return tex3Dlod(_Noise,float4((floor(p)+f+.5)/32,0)).r;}
   float density(float3 p){
    float3 flow=p*8+float3(_Seed+3.7,-_Age*1.1,2.9-_Age*.5);
    float n=noise(flow)*.68+noise(flow*2.07+6.3)*.32;
    // Unequal rolling lobes break the spherical silhouette while the softened
    // box boundary and scene-depth integration keep it off foreground bodies.
    float lobe=min(length(p-float3(-.12,.035,0))*.93,length(p-float3(.15,-.04,-.05))*1.12);
    float body=(.32-lobe)*5+(n-.49)*1.65;
    float bounds=1-smoothstep(.42,.495,max(abs(p.x),max(abs(p.y),abs(p.z))));
    return saturate(body)*bounds;
   }
   float4 frag(v2f i):SV_Target {
    float3 ray=normalize(i.world-_WorldSpaceCameraPos);
    float3 origin=mul(unity_WorldToObject,float4(_WorldSpaceCameraPos,1)).xyz;
    float3 direction=mul((float3x3)unity_WorldToObject,ray);
    float3 safeDir=(step(0,direction)*2-1)*max(abs(direction),.00001);
    float3 lo=(-.5-origin)/safeDir,hi=(.5-origin)/safeDir;
    float3 nearBounds=min(lo,hi),farBounds=max(lo,hi);
    float start=max(0,max(nearBounds.x,max(nearBounds.y,nearBounds.z)));
    float end=min(farBounds.x,min(farBounds.y,farBounds.z));
    float depth=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(i.screen)));
    end=min(end,depth/max(.001,dot(ray,-UNITY_MATRIX_V[2].xyz)));
    if(end<=start)return 0;
    float stride=(end-start)/16;float4 result=0;
    [loop] for(int j=0;j<16;j++){
     float3 p=origin+direction*(start+(j+.5)*stride);float d=density(p);
     if(d>.003){
      float lighting=saturate(.42+(d-density(p+float3(-.09,.11,-.06)))*1.6);
      float3 color=lerp(float3(.085,.11,.14),float3(.49,.55,.61),lighting);
      #ifndef UNITY_COLORSPACE_GAMMA
       color=GammaToLinearSpace(color);
      #endif
      float hot=pow(saturate(1-length(p)*3),2)*(1-smoothstep(.03,.30,_Age));
      color+=_Tint.rgb*hot*.65;
      float alpha=1-exp(-d*stride*3.1*_Opacity);
      result.rgb+=(1-result.a)*color*alpha;result.a+=(1-result.a)*alpha;
     }
    }
    return result;
   }
  ENDCG }
 }
}
