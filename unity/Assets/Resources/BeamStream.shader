Shader "Training/BeamStream" {
 Properties { _Clock("Clock",Float)=0 _Length("Length",Float)=1 _Power("Power",Float)=1 }
 SubShader { Tags { "Queue"="Transparent+1" "RenderType"="Transparent" }
 Blend SrcAlpha One ZWrite Off Cull Off
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 float _Clock,_Length,_Power;
 struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
 v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord.xy;return o;}
 half4 frag(v2f i):SV_Target {
  float x=i.uv.x*max(.05,_Length),y=i.uv.y*2-1;
  // Narrow core with a softer mantle. Moving filaments travel from the wrist
  // to the target instead of remaining a uniform white rectangle.
  float wobble=sin(x*16-_Clock*29)*.075+sin(x*29-_Clock*41)*.027;
  float core=exp(-pow((y-wobble*.25)*4.3,2));
  float mantle=exp(-y*y*3)*.45;
  float filament=exp(-pow((y-sin(x*19-_Clock*33)*.34)*19,2))*.34;
  filament+=exp(-pow((y-cos(x*14-_Clock*25)*.24)*24,2))*.22;
  float flow=.86+.14*pow(.5+.5*sin(x*25-_Clock*46),2);
  float edge=1-smoothstep(.74,1,abs(y));
  float3 color=lerp(float3(.08,.34,.9),float3(.83,.95,1),saturate(core+filament));
  return half4(color,(core*1.25+mantle+filament)*flow*edge*_Power);
 }
 ENDCG }
 }
}
