Shader "_Misc/Sidebar" {
	Properties {
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
		_FromTex("From Texture", 2D) = "white" { }
		_ToTex("To Texture", 2D) = "white" { }
		_T("Time", Range(0, 3)) = 1
		_MaxT("Max Time", Float) = 3
		
		_BX("Blocks X", Float) = 6
		_BY("Blocks Y", Float) = 6
		_Speed("Speed", Float) = 6
		_Magnitude("Magnitude", Float) = 6
	}
	SubShader {
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
			#include "UnityCG.cginc"
            #include "Assets/Danmokou/CG/Noise.cginc"
			#pragma multi_compile __ FANCY

			struct vertex {
				float4 loc  : POSITION;
				float2 uv	: TEXCOORD0;
			};

			struct fragment {
				float4 loc  : SV_POSITION;
				float2 uv	: TEXCOORD0;
			};

			fragment vert(vertex v) {
				fragment f;
				f.loc = UnityObjectToClipPos(v.loc);
				f.uv = v.uv;
				return f;
			}

			sampler2D _FromTex;
			sampler2D _ToTex;
			
            float _BX;
            float _BY;
            float _T;
            float _Speed;
            float _Magnitude;
            
			float _MaxT;

			static const float _Smooth = 0.003f;

			float4 frag(fragment f) : SV_Target { 
				float4 c1 = tex2D(_FromTex, f.uv);
				float4 c2 = tex2D(_ToTex, f.uv);
				
				float t = _T * _Speed;
				//This makes the fire appear a bit more naturally
				float fill = lerp(-0.1, 1, _T / _MaxT);
			#ifdef FANCY
			    float2 uv = f.uv;
			    uv.y -= t * 0.8;
                float3 suvt = float3(s(uv, _BX, _BY), t);
                float noise = perlin3Dmlayer(suvt, float3(_BX, _BY, 10)) *_Magnitude;
			#else
			    float noise = 0;
			#endif
                float grad = pow(f.uv.y, 1.5);
		
			    return lerp(c1, c2, smoothstep(-_Smooth, _Smooth, lerp(0, 1 + _Magnitude, fill) - grad + noise));
			}
			ENDCG
		}
	}
}