Shader "Training/ContactShadow"
{
    Properties { _MainTex ("Silhouette",2D)="black"{} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_TexelSize;
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            v2f vert(appdata_base v)
            { v2f o; o.pos=UnityObjectToClipPos(v.vertex);o.uv=mul(unity_ObjectToWorld,v.vertex).xz/10+.5;return o; }
            fixed4 frag(v2f i):SV_Target
            {
                float broad=0,contact=0;
                for(int x=-2;x<=2;x++)for(int y=-2;y<=2;y++)
                    broad+=tex2D(_MainTex,i.uv+float2(x,y)*_MainTex_TexelSize.xy*1.7).g/25;
                for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)
                    contact+=tex2D(_MainTex,i.uv+float2(x,y)*_MainTex_TexelSize.xy*.65).r/9;
                // Tight, dark contact under soles; broad and lighter occlusion
                // beneath elevated body parts. Lifting a foot fades the tight
                // component instead of keeping a dark sticker beneath it.
                float opacity=1-(1-broad*.24)*(1-contact*.64);
                return fixed4(.012,.020,.034,opacity);
            }
            ENDCG
        }
    }
}
