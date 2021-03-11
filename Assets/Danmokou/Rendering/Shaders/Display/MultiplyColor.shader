Shader "_Misc/MultiplyColor" {
	Properties {
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
		_CR("Red Color", Color) = (.8314, 0,.32157,1)
		_CG("Green Color", Color) = (.859, 0, .745, 1)
		_CB("Blue Color", Color) = (.1255,.443,1,1)
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
			
			float4 _CR;
			float4 _CG;
			float4 _CB;

			float4 frag(fragment f) : SV_Target { 
				float4 c = tex2D(_MainTex, f.uv) * f.color;
				
				float4 remap = c.r * _CR + c.g * _CG + c.b * _CB;
				remap.a = c.a;
				return remap;
			}
			ENDCG
		}
	}
}