Shader "_Misc/Ghost" {
	Properties {
		[PerRendererData] _MainTex("Texture", 2D) = "white" {}
		_AIBS("Blur Step", Range(0.0001, 0.1)) = 0.1
		[PerRendererData] _BlurRad("Blur Radius", Float) = 1.0
		[Toggle(DO_BLUR)] _ToggleBlur("Do Blur?", Float) = 0.0
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

		//Ghosts are additive
		Pass {
			Blend SrcAlpha One, OneMinusDstAlpha One
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma shader_feature DO_BLUR
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
			float _BlurRad;
			float _AIBS;

			float4 frag(fragment f) : SV_Target{
	#ifdef DO_BLUR
				float a = 4.0 * tex2D(_MainTex, f.uv).a;
				float blur_count = 1;
				for (float blur = _AIBS; blur <= _BlurRad; blur += _AIBS, ++blur_count) {
					a += tex2D(_MainTex, f.uv + float2(-blur, 0)).a;
					a += tex2D(_MainTex, f.uv + float2(blur, 0)).a;
					a += tex2D(_MainTex, f.uv + float2(0, -blur)).a;
					a += tex2D(_MainTex, f.uv + float2(0, blur)).a;
				}
				a *= 0.25 / blur_count;
	#else
				float a = tex2D(_MainTex, f.uv).a;
	#endif
				return a  * f.color;
			}
			ENDCG

		}
	}
}