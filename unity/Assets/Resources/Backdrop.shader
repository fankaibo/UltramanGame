Shader "Training/Backdrop" {
    Properties { _MainTex("Backdrop",2D)="white"{} _Clock("Atmosphere clock",Float)=0 }
    SubShader {
        Tags { "Queue"="Background" "RenderType"="Opaque" }
        ZWrite Off Cull Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "BackdropAtmosphere.cginc"
            sampler2D _MainTex;
            float _Clock;
            struct v2f { float4 pos:SV_POSITION;float2 uv:TEXCOORD0; };
            v2f vert(appdata_base v) { v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord;return o; }
            fixed4 frag(v2f i):SV_Target {
                // A tiny horizon-only refraction sells warm volcanic air while
                // keeping the moon and mountain silhouette stable.
                fixed4 color=tex2D(_MainTex,AtmosphereUV(i.uv,_Clock));
                color.rgb*=AtmosphereShade(i.uv);
                float meteors=AtmosphereMeteor(i.uv,_Clock,.07)+AtmosphereMeteor(i.uv,_Clock,.43)*.82+AtmosphereMeteor(i.uv,_Clock,.76)*.66;
                color.rgb+=float3(.42,.66,1.0)*meteors;
                return color;
            }
            ENDCG
        }
    }
}
