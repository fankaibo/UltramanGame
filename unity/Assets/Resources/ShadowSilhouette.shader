Shader "Training/ShadowSilhouette"
{
    Properties { _Color ("Opacity",Color)=(1,1,1,1) _DissolveAmount("Departure progress",Float)=0 _DissolveBounds("Departure height",Vector)=(0,3.6,0,0) }
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
            #include "KaijuDissolve.cginc"
            fixed4 _Color;
            float _DissolveAmount;float4 _DissolveBounds;
            struct v2f {float4 pos:SV_POSITION;float3 world:TEXCOORD0;};
            v2f vert(float4 v:POSITION){v2f o;o.pos=UnityObjectToClipPos(v);o.world=mul(unity_ObjectToWorld,v).xyz;return o;}
            fixed4 frag(v2f i):SV_Target{KaijuDissolveEdge(i.world,_DissolveBounds.xy,_DissolveAmount);return fixed4(_Color.aaa,1);}
            ENDCG
        }
    }
}
