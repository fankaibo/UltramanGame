Shader "Training/PhotoPersonShadow"
{
    Properties {
        _MainTex("Person alpha",2D)="black"{}
        _Frame("Frame UV",Vector)=(0,0,1,1)
        _GroundY("Visible sole height",Float)=0
        _Strength("Shadow strength",Range(0,1))=1
    }
    SubShader {
        Tags {"Queue"="Geometry-500" "RenderType"="Transparent"}
        Pass {
            Cull Off ZWrite Off ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;float4 _MainTex_TexelSize,_Frame;float _GroundY,_Strength;
            struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float height:TEXCOORD1;};
            v2f vert(appdata_base v)
            {
                v2f o;float3 world=mul(unity_ObjectToWorld,v.vertex).xyz;
                float h=world.y-_GroundY;
                // Same upper-left moon direction and compressed ground depth
                // as the fixed hero's mesh projection, anchored at the soles.
                world.x+=max(0,h)*.34;
                world.y=_GroundY+h*.12;
                o.pos=mul(UNITY_MATRIX_VP,float4(world,1));
                o.uv=_Frame.xy+v.texcoord.xy*_Frame.zw;o.height=h;return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float h=max(0,i.height),alpha=0;
                float blur=lerp(.65,4.5,saturate(h/4));
                for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)
                    alpha+=tex2D(_MainTex,i.uv+float2(x,y)*_MainTex_TexelSize.xy*blur).a/9;
                float strength=.36*exp(-h*.55)+.36*(1-smoothstep(0,.35,h));
                return fixed4(0,0,0,alpha*strength*_Strength);
            }
            ENDCG
        }
    }
}
