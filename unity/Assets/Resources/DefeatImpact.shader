Shader "Training/DefeatImpact" {
 Properties { _Noise("Cached density lattice",3D)="white"{} _Age("Landing age",Float)=0 _Ground("Ground roll",Float)=0 }
 SubShader {
  Tags {"Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True"}
  Blend One OneMinusSrcAlpha ZWrite Off ZTest Always Cull Front
  Pass { CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #include "UnityCG.cginc"
   UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
   sampler3D _Noise;float _Age,_Ground;
   struct v2f {float4 pos:SV_POSITION;float3 world:TEXCOORD0;float4 screen:TEXCOORD1;};
   v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.screen=ComputeScreenPos(o.pos);return o;}
   float noise(float3 p){float3 f=frac(p);f=f*f*(3-2*f);return tex3Dlod(_Noise,float4((floor(p)+f+.5)/32,0)).r;}
   float density(float3 p){
    float3 flow=p*8+float3(1.7,-_Age*.85,4.6);
    float n=noise(flow)*.64+noise(flow*2.07+3.4)*.36;
    float body;
    if(_Ground>.5){
     float radius=length(p.xz);
     body=(1-abs(radius-(.32+(n-.5)*.14))/.105)*saturate((n-.26)*2.6);
     body*=smoothstep(-.49,-.31,p.y)*(1-smoothstep(-.12,.42,p.y));
    }else{
     // Three asymmetric lobes give rising ash an uneven crown, rather than
     // a circular billboard. The world-space box remains depth occluded.
     float a=length((p-float3(-.15,-.08,.02))*float3(1.25,1.0,1.25));
     float b=length((p-float3(.13,.04,.04))*float3(1.45,1.05,1.35));
     float c=length((p-float3(.01,-.15,-.13))*float3(1.1,1.5,1.3));
     body=(.29-min(a,min(b,c)))*7+(n-.47)*1.9;
     body*=smoothstep(-.48,-.31,p.y);
    }
    float bounds=1-smoothstep(.43,.5,max(abs(p.x),max(abs(p.y),abs(p.z))));
    return saturate(body)*bounds*(1-smoothstep(_Ground>.5?.7:.9,2.4,_Age));
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
    float stride=(end-start)/24;float4 result=0;
    [loop] for(int j=0;j<24;j++){
     float3 p=origin+direction*(start+(j+.5)*stride);float d=density(p);
     if(d>.003){
      float lightDensity=density(p+float3(-.07,.09,-.07));
      float lighting=saturate(.42+(d-lightDensity)*1.7);
      float3 color=lerp(float3(.09,.105,.115),float3(.58,.56,.52),lighting);
      #ifndef UNITY_COLORSPACE_GAMMA
       color=GammaToLinearSpace(color);
      #endif
      float core=pow(saturate(1-length(p*float3(1,1.3,1))*2.3),2)*(1-smoothstep(.06,.55,_Age));
      color+=float3(4.5,2.35,.62)*core*(_Ground>.5?.35:1);
      float alpha=1-exp(-d*stride*(_Ground>.5?2.0:2.65));
      result.rgb+=(1-result.a)*color*alpha;result.a+=(1-result.a)*alpha;
      if(result.a>.97)break;
     }
    }
    return result;
   }
  ENDCG }
 }
}
