Shader "Training/ZeperionVolume" {
 Properties { _Noise("Density lattice",3D)="white"{} _Clock("Clock",Float)=0 _Length("Length",Float)=1 _Power("Power",Float)=1 _BeamTint("Beam tint",Color)=(.25,.72,1,1) _BeamAccent("Beam accent",Color)=(.9,.98,1,1) }
 SubShader {
  Tags {"Queue"="Transparent+1" "RenderType"="Transparent" "IgnoreProjector"="True"}
  Blend One OneMinusSrcAlpha ZWrite Off ZTest Always Cull Front
  Pass { CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #include "UnityCG.cginc"
   UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
   sampler3D _Noise;float _Clock,_Length,_Power;float4 _BeamTint,_BeamAccent;
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
    float stride=(end-start)/32;float4 result=0;
    [loop] for(int j=0;j<32;j++){
     float3 p=origin+direction*(start+(j+.5)*stride);
     float z=(p.z+.5)*_Length;
     // Fixed world-length flow advects toward the target; neither the camera
     // nor the expanding travel segment rotates or stretches its strands.
     float lane=noise(float3(p.xy*13,z*1.5-_Clock*8));
     // Long thin density strands share the beam axis. Slow longitudinal
     // distortion keeps them coherent across the shot rather than white noise.
     float fine=noise(float3(p.xy*16+5,z*.32-_Clock*1.8));
     float radius=length(p.xy);
     float envelope=exp(-pow(radius*8.5,3));
     float filament=pow(saturate(fine*1.8-.40),3);
     float pulse=.82+.18*sin(z*9-_Clock*28+lane*2);
     float core=envelope*(.18+.82*filament);
     float mantle=exp(-radius*radius*22)*(.25+.75*lane);
     float strands=pow(lane,5)*(.35+.65*fine)*exp(-radius*radius*9);
     float edge=1-smoothstep(.34+.055*lane,.49,radius);
     float cap=smoothstep(0,.035,z)*(1-smoothstep(max(0,_Length-.045),_Length,z));
     float d=(core*.95*pulse+mantle*.065+strands*.28)*edge*cap*_Power;
     float alpha=1-exp(-d*stride*12);
     float3 color=lerp(_BeamTint.rgb,_BeamAccent.rgb,saturate(filament*.85+envelope*.15));
     color*=.8+filament*.95+envelope*.12;
     result.rgb+=(1-result.a)*color*alpha;result.a+=(1-result.a)*alpha;
    }
    return result;
   }
  ENDCG }
 }
}
