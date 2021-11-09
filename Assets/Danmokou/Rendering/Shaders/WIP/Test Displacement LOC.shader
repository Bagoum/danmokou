Shader "_Test/DisplacementLOC" {
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
				//This doesn't work very well since it only calculates offsets at vertices
				f.loc = UnityObjectToClipPos(v.loc + float4(0, 2 * sin(v.uv.x * 4), 0, 0));
				f.uv = v.uv;
				f.color = v.color;
				return f;
			}

			sampler2D _MainTex;


			float4 frag(fragment f) : SV_Target {
				//This works consistently but can push the samples OOB
				float4 c = tex2D(_MainTex, f.uv + float2(0, 0 * sin(f.uv.x * 4))) * f.color;
				return c;
			}
			ENDCG
		}
	}
}