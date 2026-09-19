Shader "Training/VolcanoBackdrop" {
    Properties { _Clock("Clock",Float)=0 }
    SubShader {
        Tags { "Queue"="Background" "RenderType"="Opaque" }
        ZWrite Off Cull Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Clock;
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            v2f vert(appdata_base v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.texcoord; return o; }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float star(float2 p) {
                float2 cell=floor(p), f=frac(p)-.5;
                float h=hash(cell);
                float size=lerp(.025,.09,hash(cell+13.7));
                float d=length(f);
                float blink=.48+.52*sin(_Clock*(1.4+hash(cell+7.1)*2.2)+h*30);
                return smoothstep(size,0,d)*step(.93,h)*blink;
            }
            half4 frag(v2f i):SV_Target {
                float2 uv=i.uv;
                float horizon=smoothstep(.18,.58,uv.y);
                float3 sky=lerp(float3(.035,.012,.055),float3(.14,.035,.075),horizon);
                sky+=float3(.14,.025,.012)*pow(saturate(1-abs(uv.x-.66)*2.4),5)*smoothstep(.15,.65,uv.y);
                sky+=float3(.20,.045,.012)*smoothstep(.44,.55,uv.y)*(1-smoothstep(.55,.75,uv.y));
                float2 grid=uv*float2(34,18);
                sky+=star(grid)*float3(.65,.78,1.0);
                sky+=star(grid*1.73+4.2)*float3(1,.38,.16)*.42;
                // Distant volcanic ridgeline, with a soft red glow at the crater.
                float ridge=.24+.08*sin(uv.x*9.0)+.045*sin(uv.x*22.0+1.2);
                float mountain=smoothstep(ridge+.04,ridge,uv.y);
                sky=lerp(sky,float3(.018,.009,.018),mountain);
                float crater=exp(-pow((uv.x-.63)*18,2)-pow((uv.y-.29)*34,2));
                sky+=crater*float3(1,.12,.015)*(.28+.16*sin(_Clock*2.4));
                return half4(sky,1);
            }
            ENDCG
        }
    }
}
