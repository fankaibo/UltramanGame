Shader "UltramanGame/TransformationVeil"
{
    Properties { _Strength("Strength",Float)=0 _Clock("Clock",Float)=0 }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent"}
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
            float _Strength,_Clock;
            v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                float edge=pow(saturate(sin(i.uv.y*3.14159)),.6);
                float ribbon=pow(saturate(.5+.5*sin(i.uv.y*35-i.uv.x*12-_Clock*14)),14);
                float4 c=float4(lerp(float3(.08,.36,1),float3(.6,.9,1),ribbon),edge*_Strength*(.025+ribbon*.16));
                return c;
            }
            ENDCG
        }
    }
}
