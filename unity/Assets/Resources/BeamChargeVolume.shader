Shader "Training/BeamChargeVolume" {
 Properties { _RoseBlend("Rose energy",Float)=0  _WarmBlend("Warm energy",Float)=0  _Noise("Density lattice",3D)="white"{} _Age("Charge age",Float)=0 _Power("Accumulated power",Float)=0 _Tint("Energy tint",Color)=(1,1,1,1) }
 SubShader {
  Tags {"Queue"="Transparent-9" "RenderType"="Transparent" "IgnoreProjector"="True"}
  Blend One OneMinusSrcAlpha ZWrite Off ZTest Always Cull Front
  Pass { CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #include "UnityCG.cginc"
   UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
   sampler3D _Noise;float _Age,_Power,_WarmBlend,_RoseBlend;float4 _Tint;
   struct v2f {float4 pos:SV_POSITION;float3 world:TEXCOORD0;float4 screen:TEXCOORD1;};
   v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.screen=ComputeScreenPos(o.pos);return o;}
   float noise(float3 p){float3 f=frac(p);f=f*f*(3-2*f);return tex3Dlod(_Noise,float4((floor(p)+f+.5)/32,0)).r;}
   half4 frag(v2f i):SV_Target {
    float3 ray=normalize(i.world-_WorldSpaceCameraPos);
    float3 origin=mul(unity_WorldToObject,float4(_WorldSpaceCameraPos,1)).xyz;
    float3 direction=mul((float3x3)unity_WorldToObject,ray);
    float3 safeDir=(step(0,direction)*2-1)*max(abs(direction),.00001);
    float3 a=(-.5-origin)/safeDir,b=(.5-origin)/safeDir;
    float3 lo=min(a,b),hi=max(a,b);
    float start=max(0,max(lo.x,max(lo.y,lo.z))),end=min(hi.x,min(hi.y,hi.z));
    float depth=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(i.screen)));
    end=min(end,depth/max(.001,dot(ray,-UNITY_MATRIX_V[2].xyz)));
    if(end<=start||_Power<.001)return 0;
    float stride=(end-start)/24;float4 result=0;
    [loop] for(int j=0;j<24;j++) {
     float3 p=origin+direction*(start+(j+.5)*stride);
     float r=length(p),edge=1-smoothstep(.31,.49,r);
     // Increasing spatial frequency pulls the density inward. Two moving
     // octaves break the outline without adding a hard sphere or flat ring.
     float3 flow=p*(8+_Age*3)+float3(2.4,-_Age*.65,1.6);
     float n=noise(flow)*.68+noise(flow*2.07+4.3)*.32;
     float wisp=saturate((n-.38)*3.4)*edge;
     float core=exp(-dot(p,p)*94)*(.3+_Power*.7);
     float d=(wisp*.50+core*3.2)*_Power;
     float alpha=1-exp(-d*stride*2.8);
     float3 radiance=lerp(float3(.035,.26,.72),float3(.78,.18,.025),_WarmBlend)+lerp(float3(1.1,1.4,1.75),float3(1.75,1.35,.75),_WarmBlend)*core*1.8;
     radiance=lerp(radiance,float3(.75,.045,.27)+float3(1.7,1.1,1.45)*core*1.8,_RoseBlend);
     result.rgb+=(1-result.a)*radiance*_Tint.rgb*alpha;result.a+=(1-result.a)*alpha*.58;
    }
    return result;
   }
  ENDCG }
 }
}
