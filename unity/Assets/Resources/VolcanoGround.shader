Shader "Training/VolcanoGround" {
    Properties {
        _Color("Basalt",Color)=(.24,.25,.26,1)
        _BackdropTex("Distant landscape",2D)="black"{}
        _BackdropBlend("Blend into distant landscape",Float)=1
        _Clock("Atmosphere clock",Float)=0
    }
    SubShader {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows finalcolor:BlendLandscape nofog
        #pragma target 3.0
        #include "UnityCG.cginc"
        #include "BackdropAtmosphere.cginc"
        fixed4 _Color;
        sampler2D _BackdropTex;
        float4x4 _BackdropWorldToLocal;
        float _BackdropBlend,_Clock;
        float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
        float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
        float relief(float2 p){return noise(p*2.8)*.55+noise(p*12)*.30+noise(p*37)*.15;}
        float basaltEdge(float2 p)
        {
            float2 cell=floor(p),fraction=frac(p);float closest=8,second=8;
            [unroll] for(int y=-1;y<=1;y++)[unroll] for(int x=-1;x<=1;x++)
            {
                float2 neighbor=float2(x,y),id=cell+neighbor;
                float2 delta=neighbor+float2(hash(id),hash(id+31.7))-fraction;
                float d=dot(delta,delta);
                second=min(second,max(closest,d));closest=min(closest,d);
            }
            return sqrt(second)-sqrt(closest);
        }
        struct Input { float3 worldPos; };
        void surf(Input i,inout SurfaceOutputStandard o) {
            float2 p=i.worldPos.xz+float2(i.worldPos.y*.57,i.worldPos.y*.83);
            float rock=relief(p),grain=noise(p*83),ash=noise(p*.48);
            float edge=basaltEdge(p*1.3+float2(noise(p*.7),noise(p*.7+23))*.8);
            float seam=1-smoothstep(.015,.06+fwidth(edge),edge);
            seam*=smoothstep(.30,.68,noise(p*.7+6));
            float crust=lerp(.72,1.24,noise(p*.9));
            o.Albedo=_Color.rgb*lerp(.44,1.45,rock)*crust*(1-seam*.32);
            o.Albedo=lerp(o.Albedo,o.Albedo*float3(1.10,1.04,.94),ash*.5);
            float dx=relief(p+float2(.025,0))-rock,dy=relief(p+float2(0,.025))-rock;
            o.Normal=normalize(float3(-dx*1.1,-dy*1.1,1));
            o.Metallic=.02;o.Smoothness=.07+grain*.07;o.Occlusion=.72+.28*rock;
        }
        void BlendLandscape(Input i,SurfaceOutputStandard o,inout fixed4 color)
        {
            // Project the same distant plate onto the far ground.  Near the feet
            // it remains fully lit geometry; the outer edge has no rectangular seam.
            float blend=smoothstep(6,24,length(i.worldPos.xz-float2(0,.4)))*_BackdropBlend;
            if(blend<=0)return;
            #ifdef UNITY_PASS_FORWARDADD
            color.rgb*=1-saturate(blend);
            #else
            float3 eye=mul(_BackdropWorldToLocal,float4(_WorldSpaceCameraPos,1)).xyz;
            float3 surfacePosition=mul(_BackdropWorldToLocal,float4(i.worldPos,1)).xyz;
            float3 ray=surfacePosition-eye;
            float t=-eye.z/(abs(ray.z)>.0001?ray.z:.0001);
            float2 uv=eye.xy+ray.xy*t+.5;
            fixed3 distant=tex2D(_BackdropTex,AtmosphereUV(uv,_Clock)).rgb*AtmosphereShade(uv);
            color.rgb=lerp(color.rgb,distant,saturate(blend));
            #endif
        }
        ENDCG
    }
    FallBack "Standard"
}
