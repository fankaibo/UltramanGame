Shader "Training/SlamDustVolume" {
 Properties { _Noise("Cached ash lattice",3D)="white"{} _Age("Impact seconds",Float)=0 _Span("Wave distance",Float)=3 _Extent("World bounds",Vector)=(4,1.6,6,0) }
 SubShader {
  Tags {"Queue"="Transparent-8" "RenderType"="Transparent" "IgnoreProjector"="True"}
  Blend One OneMinusSrcAlpha ZWrite Off ZTest Always Cull Front
  Pass { CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #include "UnityCG.cginc"
   UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
   sampler3D _Noise;float _Age,_Span;float3 _Extent;
   struct v2f {float4 pos:SV_POSITION;float3 world:TEXCOORD0;float4 screen:TEXCOORD1;};
   v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.screen=ComputeScreenPos(o.pos);return o;}
   float noise(float3 p){float3 f=frac(p);f=f*f*(3-2*f);return tex3Dlod(_Noise,float4((floor(p)+f+.5)/32,0)).r;}
   float density(float3 p){
    float3 q=p*_Extent;q.y+=1.2;
    float field=0;
    // A progressing line of lifting, rolling billows; timing matches the
    // existing ground fragments from the fists to the hero's contact plane.
    [unroll] for(int k=0;k<6;k++){
     float lane=k/5.0,age=_Age-lane*.12;
     float growth=1-exp(-max(0,age)*4.5);
     float3 center=float3(sin(k*2.4)*.16,.23+growth*.18,(lane-.5)*_Span+growth*.14);
     float3 radius=float3(.30+growth*.78,.23+growth*.55,.26+growth*.52);
     float3 r=(q-center)/radius;
     float life=smoothstep(0,.055,age)*(1-smoothstep(.38,1.48,age));
     if(life<=0||dot(r,r)>1.9)continue;
     // Rotating density coordinates retain round lobes from side views.
     float angle=age*1.9+k*.65,c=cos(angle),s=sin(angle);
     float3 curl=float3(r.x,r.y*c-r.z*s,r.y*s+r.z*c);
     float n=noise(curl*2.3+float3(k*5.7,3.2,1.9))*.7+noise(curl*5.3+k*2.1)*.3;
     float shape=saturate((1-length(r))*1.7+(n-.51)*1.25)*smoothstep(.18,.55,n);
     field=max(field,shape*life);
    }
    return field*smoothstep(0,.07,q.y)*(1-smoothstep(.42,.5,max(abs(p.x),max(abs(p.y),abs(p.z)))));
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
    float stride=(end-start)/64;float4 result=0;
    [loop] for(int j=0;j<64;j++){
     float3 p=origin+direction*(start+(j+.5)*stride);float d=density(p);
     if(d>.002){
      float above=density(p+float3(-.10,.14,-.08)/_Extent);
      float light=saturate(.34+(d-above)*2.8);
      float3 color=lerp(float3(.10,.12,.14),float3(.44,.46,.48),light);
      #ifndef UNITY_COLORSPACE_GAMMA
       color=GammaToLinearSpace(color);
      #endif
      float alpha=1-exp(-d*stride*4.8);
      result.rgb+=(1-result.a)*color*alpha;result.a+=(1-result.a)*alpha;
      if(result.a>.96)break;
     }
    }
    return result;
   }
  ENDCG }
 }
}
