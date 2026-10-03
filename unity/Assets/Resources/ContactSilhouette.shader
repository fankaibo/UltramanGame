Shader "Training/ContactSilhouette"
{
    Properties { _Color("Opacity",Color)=(1,1,1,1) _DissolveAmount("Departure progress",Float)=0 _DissolveBounds("Departure height",Vector)=(0,3.6,0,0) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            // Keep the nearest-to-ground fragment even when the head or
            // torso lies above it in the overhead camera. Green retains the
            // broad body silhouette; red is only near-floor contact.
            Cull Off ZWrite Off ZTest Always
            Blend One One
            BlendOp Max
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "KaijuDissolve.cginc"
            fixed4 _Color;float _DissolveAmount;float4 _DissolveBounds;
            struct v2f {float4 pos:SV_POSITION;float3 world:TEXCOORD0;};
            v2f vert(float4 p:POSITION){v2f o;o.pos=UnityObjectToClipPos(p);o.world=mul(unity_ObjectToWorld,p).xyz;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                KaijuDissolveEdge(i.world,_DissolveBounds.xy,_DissolveAmount);
                float nearFloor=1-smoothstep(.035,.32,max(0,i.world.y));
                return fixed4(nearFloor*_Color.a,_Color.a,0,1);
            }
            ENDCG
        }
    }
}
