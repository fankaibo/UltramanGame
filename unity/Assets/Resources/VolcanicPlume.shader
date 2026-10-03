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
    // Three noise bands keep the silhouette soft while opening irregular gaps
    // between puffs.  The low band moves the lobes as a group; the high band
    // breaks up their edges so the volume does not read as a single cylinder.
    float coarse=noise(flow*.44+float3(1.7,-.8,2.4));
    float n=noise(flow)*.52+noise(flow*2.13+8)*.30+noise(flow*4.19-3)*.18;
    float detail=noise(flow*1.63+float3(-3.2,4.6,1.1));
    float drift=.035*sin(h*12-_Clock*.32+_Seed)+(.5-coarse)*.065;
    float2 spine=float2(-.12+h*.23+drift,.015*sin(h*10+_Seed)+(.5-coarse)*.045);

    // Two overlapping, tapering lobes form a broken billow.  Their offsets
    // trade sides as the plume rises, producing a naturally pinched waist and
    // a wider, uneven cap instead of one smooth radial shell.
    float lobePhase=h*10.5-_Clock*.18+coarse*2.4;
    float2 offset=float2(.085*sin(lobePhase),.045*cos(lobePhase*.83));
    float lobeBias=.62+.24*smoothstep(.08,.72,h);
    float radius=.048+pow(saturate(h),.58)*.255;
    float radiusA=radius*(.84+.13*sin(h*8.2+coarse*4));
    float radiusB=radius*(.74+.16*cos(h*7.3+coarse*3));
    float bodyA=1-length((p.xz-(spine+offset))/radiusA);
    float bodyB=1-length((p.xz-(spine-offset*.72))/radiusB);
    float body=max(bodyA,bodyB*lobeBias);
    // Low-frequency noise hollows the center of a few puffs, while detail
    // noise only erodes the outer shell.  Both are bounded to keep fill rate
    // close to the original 36-step ray march.
    float hollow=(coarse-.43)*.45;
    float shell=body+hollow+(n-.5)*.72+(detail-.5)*.22;
    float base=smoothstep(0,.035,h);
    float brokenCap=1-smoothstep(.73,.99,h);
    float layers=.84+.16*saturate(.5+.5*sin(h*25+coarse*5.5+n*3.0));
    return saturate(shell*2.45)*base*brokenCap*layers;
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
