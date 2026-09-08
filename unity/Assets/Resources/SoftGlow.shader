Shader "Training/SoftGlow" {
    Properties { _Color ("Color", Color) = (0.2,0.8,1,0.5) }
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
            struct appdata { float4 vertex:POSITION; fixed4 color:COLOR; };
            struct v2f { float4 pos:SV_POSITION; fixed4 color:COLOR; };
            v2f vert(appdata v) { v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.color=v.color;return o; }
            fixed4 frag(v2f i):SV_Target { return _Color*i.color; }
            ENDCG
        }
    }
}
