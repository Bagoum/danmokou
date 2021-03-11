Shader "_Test/SRTest" {
	Properties {
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
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
			float4 _MainTex_TexelSize;


			float4 frag(fragment f) : SV_Target { 
				float4 c = tex2D(_MainTex, f.uv) * f.color;
				return c;
			}
			ENDCG
		}
	}
}