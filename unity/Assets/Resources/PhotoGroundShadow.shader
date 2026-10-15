Shader "Training/PhotoGroundShadow"
{
    Properties {_MainTex("Frozen hero silhouette",2D)="black"{} _Strength("Shadow strength",Range(0,1))=1}
    SubShader
    {
        Tags {"Queue"="Geometry-500" "RenderType"="Transparent"}
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;float4 _MainTex_TexelSize;float _Strength;
            struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
            v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                float contact=0,broad=0;
                for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)
                    contact+=tex2D(_MainTex,i.uv+float2(x,y)*_MainTex_TexelSize.xy*.7).r/9;
                for(int x=-2;x<=2;x++)for(int y=-2;y<=2;y++)
                    broad+=tex2D(_MainTex,i.uv+float2(x,y)*_MainTex_TexelSize.xy*2).g/25;
                return fixed4(0,0,0,(1-(1-contact*.64)*(1-broad*.30))*_Strength);
            }
            ENDCG
        }
    }
}
