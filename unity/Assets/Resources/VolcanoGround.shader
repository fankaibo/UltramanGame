Shader "Training/VolcanoGround" {
    Properties { _Color("Basalt",Color)=(.045,.035,.04,1) }
    SubShader {
        Tags { "Queue"="Geometry+10" "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        #include "UnityCG.cginc"
        fixed4 _Color;
        float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
        float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
        struct Input { float3 worldPos; };
        void surf(Input i,inout SurfaceOutputStandard o) {
            float2 p=i.worldPos.xz;
            float slab=1-smoothstep(.006,.018,min(frac(p.x/.72),1-frac(p.x/.72)));
            slab=max(slab,1-smoothstep(.006,.018,min(frac((p.y+.2)/.56),1-frac((p.y+.2)/.56))));
            float rock=noise(p*2.4)*.55+noise(p*14)*.25+noise(p*48)*.2;
            o.Albedo=_Color.rgb*lerp(.66,1.32,rock)*(1-slab*.22);
            o.Normal=normalize(float3(noise(p*9)-noise(p*9+float2(.04,0)),noise(p*9)-noise(p*9+float2(0,.04)),1));
            o.Metallic=.12;o.Smoothness=.25+rock*.18;o.Occlusion=1;
        }
        ENDCG
    }
    FallBack "Standard"
}
