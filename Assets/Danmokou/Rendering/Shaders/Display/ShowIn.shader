Shader "_Misc/SnowIn" {
	Properties {
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
		_T("Time", float) = 0
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
		#include "Assets/Danmokou/CG/Math.cginc"

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
		float _T;

		float snowYBound(float t, float x){
			return 0.2 + 0.03*(t + 0.5 * pow(sin(t + PHI + sin(x) * 6), 3));
		}
		
		ENDCG

		Pass {
			CGPROGRAM

			float4 frag(fragment f) : SV_Target {
				float4 c = float4(.76,.80,.84,1);
				float t = _T + _Time.y;
				float x = f.uv.x;
				float lim = (x + 0.11*sin(3*PI*x));
				float yBound = lerp(0, lim,  clamp(snowYBound(t, x) / sqrt(lim), 0, 1));
				c.a *= smoothstep(0, 0.05, yBound - f.uv.y);
				return c;
			}
			ENDCG
		}

		Pass {
			CGPROGRAM

			float4 frag(fragment f) : SV_Target {
				float4 c = tex2D(_MainTex, f.uv) * f.color;
				float t = _T + _Time.y;
				float x = f.uv.x;
				float yBound = min(snowYBound(t, x), 0.03*(t + pow(sin(t + x * 6), 3)) - 0.04);
				return c * smoothstep(0, 0.05, yBound - f.uv.y);
			}
			ENDCG
		}
	}
}