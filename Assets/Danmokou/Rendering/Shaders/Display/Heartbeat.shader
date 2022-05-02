Shader "_Misc/Heartbeat" {
	Properties {
		_MainTex("Sprite Texture", 2D) = "white" {}
		_Speed("Speed", float) = 1
		_Scale("Scale", float) = 1
		_T("Time", float) = 1
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

			float _Speed;
			float _Scale;
			float _T;

			fragment vert(vertex v) {
				fragment f;
				f.loc = UnityObjectToClipPos(v.loc);
				f.uv = v.uv;
				f.c = v.color;
				return f;
			}

			float4 frag(fragment f) : SV_Target {
				float2 uv = f.uv - 0.5;
				float t = _Time.y * _Speed;
				float m = 0.7 + intpow(sin(t), 61) * sin(t + 1.45) * _Scale;
				uv /= m;
				uv += 0.5;
				float4 c = tex2D(_MainTex, uv) * f.c;
				return c;
			}
			ENDCG
		}
	}
}