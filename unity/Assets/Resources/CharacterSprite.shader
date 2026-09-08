Shader "Training/CharacterSprite" {
    Properties {
        _MainTex("Atlas",2D)="white"{}
        _Frame("Frame UV",Vector)=(0,0,0.25,0.5)
        _Tint("Tint",Color)=(1,1,1,1)
    }
    SubShader {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _Frame;
            fixed4 _Tint;
            struct v2f { float4 pos:SV_POSITION;float2 uv:TEXCOORD0; };
            v2f vert(appdata_base v) {
                v2f o;o.pos=UnityObjectToClipPos(v.vertex);
                o.uv=_Frame.xy+v.texcoord.xy*_Frame.zw;return o;
            }
            fixed4 frag(v2f i):SV_Target {
                fixed4 c=tex2D(_MainTex,i.uv);
                // Atlas production uses a solid green key; preserve neutral silver and yellow eyes.
                float green=c.g-max(c.r,c.b);
                float alpha=1-smoothstep(.12,.48,green);
                c.g=min(c.g,max(c.r,c.b)+.06);
                c.rgb*=_Tint.rgb;c.a*=alpha*_Tint.a;
                clip(c.a-.015);return c;
            }
            ENDCG
        }
    }
}
