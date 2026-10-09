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
    // Each parcel has a persistent index: the sampling window slides over
    // rising parcels instead of recycling a whole column at once. Its radius
    // grows with height; opposing offsets roll the rim away from the core.
    const float spacing=.16;
    float travel=_Clock*.085;
    float nearest=floor((h-travel)/spacing);
    float3 flow=p*float3(8,10,8)+float3(_Seed,-_Clock*.85,_Seed*.37);
    float broad=noise(flow*.81+float3(1.7,-.8,2.4));
    float detail=noise(flow*1.9+8)*.68+noise(flow*3.87-3)*.32;
    float body=0;
    [unroll] for(int parcel=-1;parcel<=2;parcel++){
     float id=nearest+parcel;
     float altitude=id*spacing+travel;
     float age=saturate(altitude);
     float phase=id*2.39996+_Seed*1.7;
     float spread=.035+.225*pow(age,.68);
     float sway=.018+.070*age;
     float2 center=float2(-.12+age*.23,0)+
       float2(sin(phase+age*4.1),cos(phase+age*3.2))*sway;
     float3 delta=float3(p.x-center.x,h-altitude,p.z-center.y);
     float3 radius=float3(spread*(1+.14*sin(phase)),.090+.092*age,spread*.91);
     // Soft union preserves necks between broad caps without a row of balls.
     float shell=1-length(delta/radius)+(broad-.5)*.62+(detail-.5)*.40;
     float puff=smoothstep(-.13,.38,shell);
     float life=smoothstep(-.14,.04,altitude)*(1-smoothstep(.77,1.14,altitude));
     body=1-(1-body)*(1-puff*life);
    }
    // A narrow turbulent throat joins the parcels to the vent. It has no
    // upper radial shell: billows, rather than a cone, define the silhouette.
    float2 throatCenter=float2(-.12+h*.23,0);
    float throat=1-length(p.xz-throatCenter)/(.040+.14*max(0,h));
    throat=saturate((throat+(broad-.5)*.50)*2)*(1-smoothstep(.10,.36,h));
    body=max(body,throat);
    float erosion=.55+.45*smoothstep(.25,.70,broad*.6+detail*.4);
    return body*erosion*smoothstep(0,.028,h)*(1-smoothstep(.86,.998,h));
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
      float lightDensity=density(p+float3(-.052,.040,-.060));
      float distantDensity=density(p+float3(-.12,.095,-.14));
      // A short directional shadow path darkens covered folds. This avoids
      // lighting every near-facing parcel like white steam. Three lattice
      // reads per density sample keep it below the previous ten per step.
      float transmission=exp(-lightDensity*1.6-distantDensity*2.2);
      float lighting=saturate(.18+transmission*.65+(d-lightDensity)*.8);
      float3 color=lerp(float3(.050,.055,.065),float3(.40,.425,.46),lighting);
      // Ash swatches are display colors; heat below is emitted radiance.
      #ifndef UNITY_COLORSPACE_GAMMA
       color=GammaToLinearSpace(color);
      #endif
      // Incandescent gas is confined to the throat. The cool upper billows
      // stay opaque and directional instead of becoming orange fire sprites.
      float heat=pow(saturate(1-h*4.3),2)*(.65+_Surge*.5);
      color+=float3(2.5,.38,.035)*heat*(.35+.65*d);
      float alpha=1-exp(-d*stride*3.0);
      result.rgb+=(1-result.a)*color*alpha;result.a+=(1-result.a)*alpha;
      if(result.a>.985)break;
     }
    }
    return result;
   }
  ENDCG }
 }
}
