Shader "_Transition/DiamondTransition" {
	Properties {
		[PerRendererData] _MainTex("[Unused]", 2D) = "white" {}
		_TrueTex("Sprite Texture", 2D) = "white" {}
		[PerRendererData] _FaderTex("Fade Controller", 2D) = "white" {}
		_F("Fill Ratio", Range(0, 1)) = 0.7
		_BX("Blocks X", Float) = 32
		_BY("Blocks Y", Float) = 18
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
			#pragma multi_compile_local __ FT_REVERSE
			#pragma multi_compile_local __ REQ_CIRCLE
			#pragma multi_compile_local __ REQ_Y
			#pragma multi_compile_local __ REQ_EMPTY
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
			#include "Assets/Danmokou/CG/Math.cginc"

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

            float _F;
			sampler2D _FaderTex;
			sampler2D _TrueTex;
			static const float _Smooth = 0.02f;

			float _BX;
			float _BY;

			float getrot(float2 block_center) {
				return TAU * 0.4 * _F - 0.03 * block_center.y;
			}

			float4 frag(fragment f) : SV_Target {
				float size = max(0, 2 * _F - f.uv.y);
				float2 block_pos = float2(f.uv.x * _BX, f.uv.y * _BY);
				float2 block_center_1 = floor(block_pos) + float2(0, 0);
				float2 block_center_2 = floor(block_pos) + float2(0, 1);
				float2 block_center_3 = floor(block_pos) + float2(1, 0);
				float2 block_center_4 = floor(block_pos) + float2(1, 1);
				float2 rel_block_1 = rot2(getrot(block_center_1), block_pos - block_center_1);
				float2 rel_block_2 = rot2(getrot(block_center_2), block_pos - block_center_2);
				float2 rel_block_3 = rot2(getrot(block_center_3), block_pos - block_center_3);
				float2 rel_block_4 = rot2(getrot(block_center_4), block_pos - block_center_4);
				float rel_dist_1 = abs(rel_block_1.x) + abs(rel_block_1.y);
				float rel_dist_2 = abs(rel_block_2.x) + abs(rel_block_2.y);
				float rel_dist_3 = abs(rel_block_3.x) + abs(rel_block_3.y);
				float rel_dist_4 = abs(rel_block_4.x) + abs(rel_block_4.y);
				float4 c = tex2D(_TrueTex, f.uv);
				return lerp(float4(0, 0, 0, 0), c, clamp(
					smoothstep(-_Smooth, _Smooth, size - rel_dist_1) +
					smoothstep(-_Smooth, _Smooth, size - rel_dist_2) +
					smoothstep(-_Smooth, _Smooth, size - rel_dist_3) +
					smoothstep(-_Smooth, _Smooth, size - rel_dist_4)
					, 0, 1));
			}
			ENDCG
		}
	}
}