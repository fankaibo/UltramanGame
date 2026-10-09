Shader "Training/PhotoGroundMask"
{
    SubShader
    {
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            Blend One One
            BlendOp Max
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct data {float4 vertex:POSITION;float4 color:COLOR;};
            struct v2f {float4 pos:SV_POSITION;float4 color:COLOR;};
            v2f vert(data v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.color=v.color;return o;}
            fixed4 frag(v2f i):SV_Target{return i.color;}
            ENDCG
        }
    }
}
