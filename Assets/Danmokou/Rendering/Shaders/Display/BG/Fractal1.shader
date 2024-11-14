Shader "_Backgrounds/Fractal1" {
	Properties {
		_MainTex("Sprite Texture", 2D) = "white" {}
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
			#include "Assets/Danmokou/CG/BagoumShaders.cginc"

			struct vertex {
				float4 loc  : POSITION;
				float2 uv	: TEXCOORD0;
				float4 color: COLOR;
			};

			struct fragment {
				float4 loc  : SV_POSITION;
				float2 uv	: TEXCOORD0;
				float4 c    : COLOR;
			};


			fragment vert(vertex v) {
				fragment f;
				f.loc = UnityObjectToClipPos(v.loc);
				f.uv = v.uv;
				f.c = v.color;
				return f;
			}

			float3 mpallete(float t) {
				//http://dev.thi.ng/gradients/
				return palette(smoothstep(0, 1, softmod(t, 1)),
					//float3(0.788, 1.148, 0.158), float3(0.318,1.118,.32), float3(0.378,0.198,0.198),float3(0.965,2.265,0.758));
					float3(0.898,1.148,0.738), float3(0.198,1.118,0.538), float3(-0.632,0.448,0.318), float3(-0.132,2.265,1.478));
			}

			float4 frag(fragment f) : SV_Target {
				float3 fc = float3(0.1, 0.01, 0.09);
				float aspect = _MainTex_TexelSize.z / _MainTex_TexelSize.w;
				float2 uv0 = (f.uv - float2(0.61, 0.5)) * float2(2 * aspect, 2);
				float2 uv = uv0;
				float d0 = length(uv0);
				for (float i = 0; i < 3; ++i) {
					uv = frac(uv * 1.7) - 0.5;
					float d = length(uv) * exp(d0-2);
					float3 c = mpallete(d0 * 2 + i * 0.4 + _Time.y/3);
					d = sin(d*8+_Time.y);
					d = 0.04/abs(d);
					c *= d;
					c = pow(min((1).xxx, c), 1.2);
					fc += c * lerp(0.8, 0.3, smoothstep(0, 2, i)) * smoothstep(0.6, 1.6, length(uv0 * float2(1, 2)));
				}
				return float4(fc, 1);
			}
			ENDCG
		}
	}
}