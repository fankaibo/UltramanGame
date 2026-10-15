Shader "Training/MeteorCore" {
    Properties { _Color ("Color", Color) = (1,0.94,0.78,1) }
    SubShader {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.uv; return o; }
            fixed4 frag(v2f i):SV_Target {
                float radius=length((i.uv-.5)*2);
                float edge=saturate(1-radius);
                float alpha=edge*edge*_Color.a;
                return fixed4(_Color.rgb,alpha);
            }
            ENDCG
        }
    }
}
