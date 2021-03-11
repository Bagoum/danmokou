Shader "_Misc/SpellCircleFire" {
	Properties{
		[PerRendererData] _MainTex("Texture", 2D) = "white" {}
		_T("Time", Range(0, 5)) = 1
		_R("Radius", Float) = 0.5
		_RG("Gradient Radius (Ratio)", Float) = 0.1
		_Thick("Bar Thickness (Ratio)", Float) = 0.1
		_OThick("Outline Thickness", Float) = 0.1
		_BX("Blocks Along Radial Length", Float) = 6
		_BY("Blocks Around Perimeter", Float) = 6
		_C1("Color1", Color) = (1,0,0.8,1)
		_C2("Color2", Color) = (0.4,1,0.8,1)
		_C3("Color3", Color) = (1,0.5,0,1)
		_C4("Color4", Color) = (1,1,1,0.7)
		_Speed("Speed", Float) = 6
		_Magnitude("Magnitude", Float) = 6
	}
    SubShader{
        Tags {
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "Queue" = "Transparent"
        }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha, OneMinusDstAlpha One
       
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Assets/Danmokou/CG/Noise.cginc"
            #include "UnityCG.cginc"
			#pragma multi_compile __ FANCY

            struct vertex {
                float4 loc  : POSITION;
                float2 uv	: TEXCOORD0;
            };

            struct fragment {
                float4 loc   : SV_POSITION;
                float2 uv	 : TEXCOORD0;
            };
            
            fragment vert(vertex v) {
                fragment f;
                f.loc = UnityObjectToClipPos(v.loc);
                f.uv = v.uv;
                return f;
            }

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            
            float _BX;
            float _BY;
            float _T;
            float _R;
            float _RG;
            float _Thick;
            float _OThick;
            float4 _C1;
            float4 _C2;
            float4 _C3;
            float4 _C4;
            float _Speed;
            float _Magnitude;
            
            float _RPPU;

            float4 frag(fragment f) : SV_Target {
                float t = _T * _Speed;
                float2 uv = f.uv - center;
			    //when this mod loops, a separation line will appear
			    uv = float2(length(uv), (PI + atan2(uv.y, uv.x)) / TAU);
			    float units_len = uv.x * _MainTex_TexelSize.z / _RPPU;
            #ifdef FANCY
			    uv.x -= _R * _RPPU * _MainTex_TexelSize.x;
			    uv.y -= t / 30; //rotation effect
			    uv.x -= t / 16;
                float3 suvt = float3(s(uv, _BX, _BY), t);
                //float noise = c01(voronoi3D(suvt).z * 4);
                float noise = c01(pm01(perlin3Dmlayer(suvt, float3(_BX, _BY, 10)))) * _Magnitude;
			    float grad = 1 - c01((units_len - _R) / (_RG * _R));
			    float4 c = float4(1,1,1,1);
			    
                c = _C3 * smoothstep(noise, noise + 0.1, grad);
                c = lerp(c, _C2, smoothstep(noise, noise+0.15, grad-0.25));
                c = lerp(c, _C1, smoothstep(noise, noise+0.15, grad-0.5));
            #else
                float4 c = _C1;
            #endif
                c = lerp(c, _C4, smoothstep(0, 0.01, _R*(1+_OThick)- units_len));
                c = lerp(c, _C4, smoothstep(-0.01, 0, units_len-_R*(1+_Thick-_OThick)));
                c.a *= smoothstep(0, 0.01, _R*(1+_Thick) - units_len);
                c.a *= smoothstep(-0.01,0, units_len-_R);
                return c;
            }
            ENDCG
        }
	}
}