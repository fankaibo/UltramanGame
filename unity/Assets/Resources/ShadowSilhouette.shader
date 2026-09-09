Shader "Training/ShadowSilhouette"
{
    Properties { _Color ("Opacity",Color)=(1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            float4 vert(float4 v:POSITION):SV_POSITION{return UnityObjectToClipPos(v);}
            fixed4 frag():SV_Target{return fixed4(_Color.aaa,1);}
            ENDCG
        }
    }
}
