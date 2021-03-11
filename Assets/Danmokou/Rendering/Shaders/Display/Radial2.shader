Shader "_Misc/Radial2" {
	Properties{
		[PerRendererData] _MainTex("Texture", 2D) = "white" {}
		_R("Radius", Float) = 0.5
		_Subradius("Subradius", Float) = 0.1
		_DarkenRatio("Darken Ratio", Float) = 0.9
		_DarkenAmount("Darken Amount", Float) = 0.5
		[Enum(One,1,OneMinusSrcAlpha,10)] _BlendTo("Blend mode", Float) = 10
		
		[PerRendererData] _P1("Curr Phase Begins", Range(0, 1)) = 0.8
		_P2("Next Phase Begins", Range(0, 1)) = 0.3
		
		_F("Curr Phase Fill Ratio", Range(0, 1)) = 0.7
		[PerRendererData] _CF("Curr Phase Color", Color) = (0.5,0.1,0.5,1)
		[PerRendererData] _CN("Next Phase Color", Color) = (0.8,0,0,1)
		[PerRendererData] _CE("Unfilled Color", Color) = (0,0.5,0,1)
		_BX("Blocks Radial", Float) = 6
		_BY("Blocks Angular", Float) = 30
		_Speed("Speed", Float) = 1
		_FireMagnitude("Fire Magnitude", Float) = 0.005
		
		_T("Time", Range(0,30)) = 0
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
        Blend SrcAlpha [_BlendTo], OneMinusDstAlpha One

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
				float4 color: COLOR;
            };

            struct fragment {
                float4 loc   : SV_POSITION;
                float2 uv	 : TEXCOORD0;
				float4 color: COLOR;
                float effF : EXTRADATA;
            };

            float _F;
            float _P1;
            float _P2;
            
            fragment vert(vertex v) {
                fragment f;
                f.loc = UnityObjectToClipPos(v.loc);
                f.uv = float2(v.uv.x - 0.5, v.uv.y - 0.5);
                f.effF = _P2 + (_P1 - _P2) * _F;
            	f.color = v.color;
                return f;
            }

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _R;
            float _Subradius;
            float _DarkenRatio;
            float _DarkenAmount;
            float4 _CF;
            float4 _CN;
            float4 _CE;
            static const float smth = 0.002f;
            static const float nsmth = 0.003f;
            static const float rsmth = 0.004f;
            static const float dsmth = 0.012f;
            
            float _RPPU;
            
            float _T;
            float _BX;
            float _BY;
            float _Speed;
            float _FireMagnitude;

            float4 frag(fragment f) : SV_Target {
                float baseang = atan2(f.uv.y, f.uv.x) / TAU; // -1/2 (@-180) to 1/2 (@180)
                //float side = abs(ang) < 0.25 ? 1 : 0;
                float ang = fmod(baseang + 1.75, 1); // 0 to 1, starting at 90
                ang = 1 - 2 * abs(0.5 - ang); // 90 = 0; -90 = 1; 0,180 = 0.5
                float r = length(f.uv) * _MainTex_TexelSize.z / _RPPU;
                float3 srt = float3(r * _BX, ang * _BY, _T * _Speed);
                
            #ifdef FANCY
                float noise = perlin3Dlayer01(srt);
                //Square [0,1] noise to make it have less an effect on where shifts are
                //Negative so that the noise always increases the effective size of the hp bar
                float noise_ang = clamp(0, 1.4, ang + noise * noise * -_FireMagnitude);
            #else
                float noise_ang = ang;
            #endif
                
                //float is_empty = step(f.effF + 0.0001, noise_ang); //+0.0001 solves rounding errors for full health
                float is_empty = smoothstep(f.effF, f.effF + smth*2, noise_ang); //Smooth for noise. One-way to avoid bugs at end
                float is_next = 0;
                if (_P2 > 0) {
                    is_next = 1 - smoothstep(max(0, _P2 - nsmth), _P2 + nsmth, ang);
                }
                float is_curr = (1-is_empty) * (1-is_next);
                float4 c = float4(1,1,1,1) * (is_curr * _CF + is_empty * _CE + is_next * _CN);
            	float darken = 1-_DarkenAmount *
            		smoothstep(-dsmth, +dsmth, abs(r - _R) - _Subradius*_DarkenRatio);
            	c.rgb *= float3(darken, darken, darken);
                c.a *= 1-smoothstep(-rsmth, rsmth, abs(r - _R) - _Subradius);
            	//c.a *= smoothstep(0.000, 0.006, abs(abs(r-_R) - _Subradius*_DarkenRatio));
                return c * f.color;
            }
            ENDCG
        }
	}
}