Shader "SRPG/Sprite"
{
    Properties {
		_MainTex("Sprite Texture", 2D) = "white" {}
        _Tint("Tint", Color) = (1,1,1,1)
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
			#pragma multi_compile_local __ FT_GRAYSCALE
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
				float4 c    : COLOR;
			};

			sampler2D _MainTex;
			float4 _Tint;

			fragment vert(vertex v) {
				fragment f;
				f.loc = UnityObjectToClipPos(v.loc);
				f.uv = v.uv;
				f.c = v.color * _Tint;
				return f;
			}

			float4 frag(fragment f) : SV_Target { 
				float4 c = tex2D(_MainTex, f.uv) * f.c;
			#if FT_GRAYSCALE
				float g = grayscale(c);
			    c.rgb = float3(g, g, g);
			#endif
				return c;
			}
			ENDCG
		}
	}
}