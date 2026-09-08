Shader "Training/PhotoLayer" {
    Properties {
        _MainTex("Image",2D)="white"{}
        _Frame("Frame UV",Vector)=(0,0,1,1)
        _KeyGreen("Green key only for Tiga",Float)=0
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
            sampler2D _MainTex;float4 _Frame;float _KeyGreen;
            struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
            v2f vert(appdata_base v) {v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=_Frame.xy+v.texcoord.xy*_Frame.zw;return o;}
            fixed4 frag(v2f i):SV_Target {
                fixed4 c=tex2D(_MainTex,i.uv);
                if(_KeyGreen>.5) {c.a*=1-smoothstep(.12,.48,c.g-max(c.r,c.b));c.g=min(c.g,max(c.r,c.b)+.06);}
                return c;
            }
            ENDCG
        }
    }
}
