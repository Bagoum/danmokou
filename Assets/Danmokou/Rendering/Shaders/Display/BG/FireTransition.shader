Shader "_Transition/FireTransition" {
	Properties {
		[PerRendererData] _MainTex("[Unused]", 2D) = "white" {}
		_TrueTex("Sprite Texture", 2D) = "white" {}
		_F("Fill Ratio", Range(0, 1)) = 0.7
		_T("Time", Range(0, 10)) = 5
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
				float4 color: COLOR;
			};

			struct fragment {
				float4 loc  : SV_POSITION;
				float2 uv	: TEXCOORD0;
				float4 color: COLOR;
			};
			

			fragment vert(vertex v) {
				fragment f;
				f.loc = UnityObjectToClipPos(v.loc);
				f.uv = v.uv;
				f.color = v.color;
				return f;
			}

            float _F;
			sampler2D _FaderTex;
			sampler2D _TrueTex;
			
            float _BX;
            float _BY;
            float _T;
            float _Speed;
            float _Magnitude;
            
			static const float _Smooth = 0.003f;

			float4 frag(fragment f) : SV_Target { 
			float t = _T * _Speed;
			#ifdef FANCY
			    float2 uv = f.uv;
			    uv.y -= t * 0.8;
                float3 suvt = float3(s(uv, _BX, _BY), t);
                float noise = perlin3Dmlayer(suvt, float3(_BX, _BY, 10)) * _Magnitude;
			#else
			    float noise = 0 * _Magnitude;
			#endif
                float grad = pow(f.uv.y, 1.5);
		
			    float4 c = tex2D(_TrueTex, f.uv) * f.color;
                c = c * smoothstep(-_Smooth, _Smooth, lerp(0, 1+_Magnitude, _F) - grad + noise);
				return c;
			}
			ENDCG
		}
	}
}