Shader "Training/BeamImpactVolume" {
 Properties { _Noise("Cached density lattice",3D)="white"{} _Age("Contact age",Float)=0 _Ground("Rolling ash",Float)=0 }
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
    float3 flow=p*7+float3(4.7,-_Age*.7,1.9);
    float n=noise(flow)*.64+noise(flow*2.09+5.3)*.36;
    float body;
    if(_Ground>.5){
     // Irregular rolling annulus, with an empty centre and a soft floor edge.
     float radius=length(p.xz);
     body=(1-abs(radius-(.32+(n-.5)*.13))/.095)*saturate((n-.25)*2.3);
     body*=smoothstep(-.5,-.32,p.y)*(1-smoothstep(-.08,.45,p.y));
    }else{
     float r=length(p*float3(1,1.07,1));
     body=(.39-r)*6+(n-.48)*1.85;
    }
    float bounds=1-smoothstep(.43,.5,max(abs(p.x),max(abs(p.y),abs(p.z))));
    float fade=1-smoothstep(_Ground>.5?.65f:.75f,1.85,_Age);
    return saturate(body)*bounds*fade;
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
      float lightDensity=density(p+float3(-.075,.09,-.07));
      float lighting=saturate(.40+(d-lightDensity)*1.6);
      float3 color=lerp(float3(.07,.095,.125),float3(.46,.54,.62),lighting);
      if(_Ground>.5)color=lerp(float3(.11,.105,.095),float3(.40,.38,.34),lighting);
      #ifndef UNITY_COLORSPACE_GAMMA
       color=GammaToLinearSpace(color);
      #endif
      if(_Ground<.5){
       float core=pow(saturate(1-length(p)*2.65),2)*(1-smoothstep(.08,.62,_Age));
       color+=float3(1.2,2.8,4.5)*core;
      }
      float alpha=1-exp(-d*stride*(_Ground>.5?1.7:3.2));
      result.rgb+=(1-result.a)*color*alpha;result.a+=(1-result.a)*alpha;
      if(result.a>.97)break;
     }
    }
    return result;
   }
  ENDCG }
 }
}
