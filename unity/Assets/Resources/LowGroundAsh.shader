Shader "Training/LowGroundAsh" {
 Properties { _Noise("Shared ash lattice",3D)="white"{} _Clock("Stage time",Float)=0 }
 SubShader {
  Tags {"Queue"="Transparent-20" "RenderType"="Transparent" "IgnoreProjector"="True"}
  Blend One OneMinusSrcAlpha ZWrite Off ZTest Always Cull Front
  Pass { CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #include "UnityCG.cginc"
   UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
   sampler3D _Noise;float _Clock;
   struct v2f {float4 pos:SV_POSITION;float3 world:TEXCOORD0;float4 screen:TEXCOORD1;};
   v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.screen=ComputeScreenPos(o.pos);return o;}
   float noise(float3 p){float3 f=frac(p);f=f*f*(3-2*f);return tex3Dlod(_Noise,float4((floor(p)+f+.5)/32,0)).r;}
   float4 frag(v2f i):SV_Target {
    float3 ray=normalize(i.world-_WorldSpaceCameraPos);
    float3 origin=mul(unity_WorldToObject,float4(_WorldSpaceCameraPos,1)).xyz;
    float3 direction=mul((float3x3)unity_WorldToObject,ray);
    float3 safeDir=(step(0,direction)*2-1)*max(abs(direction),.00001);
    float3 a=(-.5-origin)/safeDir,b=(.5-origin)/safeDir;
    float3 nearBounds=min(a,b),farBounds=max(a,b);
    float start=max(0,max(nearBounds.x,max(nearBounds.y,nearBounds.z)));
    float end=min(farBounds.x,min(farBounds.y,farBounds.z));
    float depth=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(i.screen)));
    end=min(end,depth/max(.001,dot(ray,-UNITY_MATRIX_V[2].xyz)));
    if(end<=start)return 0;
    float stride=(end-start)/12;float4 result=0;
    [loop] for(int j=0;j<12;j++){
     float distance=start+(j+.5)*stride;
     float3 p=origin+direction*distance;
     float3 world=_WorldSpaceCameraPos+ray*distance;
     float3 drift=world*float3(.55,1.7,.8)+float3(-_Clock*.24,0,_Clock*.055);
     float n=noise(drift)*.72+noise(drift*2.1+7.3)*.28;
     float edge=1-smoothstep(.30,.495,max(abs(p.x),abs(p.z)));
     float height=exp(-pow((p.y+.14)*4.7,2))*(1-smoothstep(.38,.495,abs(p.y)));
     float density=saturate((n-.36)*2.2)*edge*height;
     float3 color=lerp(float3(.16,.19,.22),float3(.38,.41,.44),n);
     #ifndef UNITY_COLORSPACE_GAMMA
      color=GammaToLinearSpace(color);
     #endif
     float alpha=1-exp(-density*stride*.58);
     result.rgb+=(1-result.a)*color*alpha;result.a+=(1-result.a)*alpha;
    }
    return result;
   }
  ENDCG }
 }
}
