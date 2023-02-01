Shader "_Misc/LightningBG" {
	Properties {
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
		_T("Time", float) = 0
		_BX("BlocksX", Float) = 6
		_BY("BlocksY", Float) = 4
		_Speed("Speed", Float) = 1
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
		
		CGINCLUDE
		#pragma vertex vert
		#pragma fragment frag
		#include "UnityCG.cginc"
		#include "Assets/Danmokou/CG/Noise.cginc"

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
		sampler2D _MainTex;
		float _T, _BX, _BY, _Speed;

		float4 borealispass(fragment f, float bx, float by, float4 c1, float tmod){
				float t = (_T + _Time.y) * _Speed + tmod;
				float3 suvt = float3(f.uv.x * bx, f.uv.y * by, t);
				float3 noise = voronoi3D(suvt);
				c1.a *= pow(clamp(noise.x, 0, 1), 6);
				return c1;
		}

		ENDCG

		Pass {
			CGPROGRAM

			float4 frag(fragment f) : SV_Target {
				float4 c1 = float4(.04, .06, .11, 1);
				float4 c2 = float4(.07, .12, .19, 1);
				float t = (_T + _Time.y) * _Speed;
				float3 suvt = float3(s(f.uv, _BX, _BY), t);
				float ratio = sin(t) + sin(2 * t + 0.6 + perlin3D(suvt));
				return lerp(c1, c2, smoothstep(-2, 1.6, ratio));
			}
			ENDCG
		}

		Pass {
			CGPROGRAM
			float4 frag(fragment f) : SV_Target {
				return borealispass(f, 2, 0, float4(0.92, .79, .27, 0.5), 0);
			}
			ENDCG
		}
		
		Pass {
			CGPROGRAM
			float4 frag(fragment f) : SV_Target {
				return borealispass(f, _BX, 0, float4(0.36, .96, .71, 0.5), 5);
			}
			ENDCG
		}
		
		Pass {
			CGPROGRAM
			float4 frag(fragment f) : SV_Target {
				return borealispass(f, _BX, 0, float4(0.58, .83, 1, 0.5), 10);
			}
			ENDCG
		}
	}
}