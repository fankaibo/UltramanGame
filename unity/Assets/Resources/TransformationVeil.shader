Shader "UltramanGame/TransformationVeil"
{
    Properties { _Strength("Strength",Float)=0 _Clock("Clock",Float)=0 }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent"}
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;};
            struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;float3 normal:TEXCOORD2;float cap:TEXCOORD3;};
            float _Strength,_Clock;
            v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.normal=UnityObjectToWorldNormal(v.normal);o.cap=abs(v.normal.y);return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p)
            {
                float2 a=floor(p),b=frac(p);b=b*b*(3-2*b);
                return lerp(lerp(hash(a),hash(a+float2(1,0)),b.x),lerp(hash(a+float2(0,1)),hash(a+1),b.x),b.y);
            }
            fixed4 frag(v2f i):SV_Target
            {
                clip(.5-i.cap);
                float vertical=smoothstep(0,.12,i.uv.y)*(1-smoothstep(.72,1,i.uv.y));
                float flowing=noise(float2(i.uv.x*18,i.uv.y*4-_Clock*1.8));
                float shafts=pow(saturate(flowing),4);
                float rim=pow(1-abs(dot(normalize(i.normal),normalize(_WorldSpaceCameraPos-i.world))),2);
                float mantle=vertical*(.035+shafts*.48+rim*.12)*_Strength;
                return float4(lerp(float3(.08,.35,.92),float3(.66,.89,1),saturate(shafts*3+rim*.3)),mantle);
            }
            ENDCG
        }
    }
}
