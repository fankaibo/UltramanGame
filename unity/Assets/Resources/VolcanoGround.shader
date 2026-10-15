Shader "Training/VolcanoGround" {
    Properties {
        _Color("Basalt",Color)=(.24,.25,.26,1)
        _Albedo("Scanned rock surface",2D)="gray"{}
        [Normal] _Normal("Surface normal",2D)="bump"{}
        _ARM("Occlusion roughness metal",2D)="white"{}
        _TextureScale("World texture scale",Float)=.35
        _RockFace("Project onto rock faces",Float)=0
        _NormalStrength("Relief strength",Float)=.85
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
        sampler2D _BackdropTex,_Albedo,_Normal,_ARM;
        float4x4 _BackdropWorldToLocal;
        float _BackdropBlend,_Clock;
        float _TextureScale,_RockFace,_NormalStrength;
        float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
        float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
        void surfaceSample(float2 uv,out float3 color,out float3 normal,out float3 arm)
        {
            color=tex2D(_Albedo,uv).rgb;
            normal=UnpackNormal(tex2D(_Normal,uv));
            arm=tex2D(_ARM,uv).rgb;
        }
        struct Input { float3 worldPos; float3 worldNormal; INTERNAL_DATA };
        void surf(Input i,inout SurfaceOutputStandard o) {
            float3 p=i.worldPos*_TextureScale;
            float3 geometric=normalize(WorldNormalVector(i,float3(0,0,1)));
            float3 color,normal,arm,perturb;
            surfaceSample(p.xz,color,normal,arm);
            perturb=float3(normal.x,0,normal.y);
            if(_RockFace>.5)
            {
                // World projection keeps the sides of broken rocks as detailed
                // as their tops; the original planar UV stretched on vertical faces.
                float3 weights=pow(abs(geometric),4);weights/=max(dot(weights,1),.001);
                float3 cx,nx,ax,cz,nz,az;
                surfaceSample(p.zy,cx,nx,ax);surfaceSample(p.xy,cz,nz,az);
                color=color*weights.y+cx*weights.x+cz*weights.z;
                arm=arm*weights.y+ax*weights.x+az*weights.z;
                perturb=perturb*weights.y+float3(0,nx.y,nx.x)*weights.x+float3(nz.x,nz.y,0)*weights.z;
            }
            else
            {
                // Offset/rotated patches break the recognizable repeated tile.
                float blend=smoothstep(.28,.72,noise(i.worldPos.xz*.16+7));
                float2 uv2=float2(-p.z,p.x)*.77+float2(.37,.61);
                float3 c2,n2,a2;surfaceSample(uv2,c2,n2,a2);
                color=lerp(color,c2,blend);arm=lerp(arm,a2,blend);
                perturb=lerp(perturb,float3(n2.y,0,-n2.x),blend);
            }
            float gray=dot(color,float3(.2126,.7152,.0722));
            color=lerp(color,gray.xxx,.82);
            float deposits=lerp(.76,1.14,noise(i.worldPos.xz*.24));
            o.Albedo=color*_Color.rgb*deposits;
            perturb-=geometric*dot(perturb,geometric);
            float3 worldNormal=normalize(geometric+perturb*_NormalStrength);
            o.Normal=float3(dot(worldNormal,WorldNormalVector(i,float3(1,0,0))),dot(worldNormal,WorldNormalVector(i,float3(0,1,0))),dot(worldNormal,geometric));
            o.Metallic=0;o.Smoothness=(1-arm.g)*.45;o.Occlusion=lerp(.48,1,arm.r);
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
