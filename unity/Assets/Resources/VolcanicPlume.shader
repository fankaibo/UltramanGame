Shader "Training/VolcanicPlume" {
 Properties { _Noise("Cached density lattice",3D)="white"{} _Seed("Seed",Float)=0 _Clock("Clock",Float)=0 _Surge("Vent surge",Range(0,1))=.5 }
 SubShader {
  Tags {"Queue"="Transparent-20" "RenderType"="Transparent" "IgnoreProjector"="True"}
  // Back faces give one ray per pixel, including when a cut-in camera enters
  // the volume. Scene depth clips each ray before foreground opaque geometry.
  Blend One OneMinusSrcAlpha ZWrite Off ZTest Always Cull Front
  Pass { CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #include "UnityCG.cginc"
   UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
   sampler3D _Noise;
   float _Seed,_Clock,_Surge;
   struct v2f {float4 pos:SV_POSITION;float3 world:TEXCOORD0;float4 screen:TEXCOORD1;};
   v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.screen=ComputeScreenPos(o.pos);return o;}
   float noise(float3 p){
    float3 f=frac(p);f=f*f*(3-2*f);
    return tex3Dlod(_Noise,float4((floor(p)+f+.5)/32,0)).r;
   }
   float density(float3 p){
    float h=p.y+.5;
    float3 flow=p*float3(6.4,9,6.4)+float3(_Seed,-_Clock*.65,_Seed*.37);
    float n=noise(flow)*.55+noise(flow*2.13+8)*.30+noise(flow*4.19-3)*.15;
    float2 center=float2(-.12+h*.23+.025*sin(h*14-_Clock*.5+_Seed),.015*sin(h*11+_Seed));
    float radius=.055+pow(saturate(h),.62)*.29;
    float body=1-length(p.xz-center)/radius;
    return saturate(body*2.5+(n-.48)*3.2)*smoothstep(0,.04,h)*(1-smoothstep(.67,.98,h));
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
    float stride=(end-start)/36;
    float4 result=0;
    [loop] for(int stepIndex=0;stepIndex<36;stepIndex++){
     float3 p=origin+direction*(start+(stepIndex+.5)*stride);
     float d=density(p);
     if(d>.005){
      float h=p.y+.5;
      float lightDensity=density(p+float3(-.055,.045,-.055));
      float lighting=saturate(.48+(d-lightDensity)*1.8);
      float3 color=lerp(float3(.043,.047,.056),float3(.25,.28,.32),lighting);
      // Ash swatches are display colors; heat below is emitted radiance.
      #ifndef UNITY_COLORSPACE_GAMMA
       color=GammaToLinearSpace(color);
      #endif
      // Incandescent gas is confined to the throat. The cool upper billows
      // stay opaque and directional instead of becoming orange fire sprites.
      float heat=pow(saturate(1-h*4.3),2)*(.65+_Surge*.5);
      color+=float3(2.5,.38,.035)*heat*(.35+.65*d);
      float alpha=1-exp(-d*stride*2.1);
      result.rgb+=(1-result.a)*color*alpha;result.a+=(1-result.a)*alpha;
      if(result.a>.985)break;
     }
    }
    return result;
   }
  ENDCG }
 }
}
