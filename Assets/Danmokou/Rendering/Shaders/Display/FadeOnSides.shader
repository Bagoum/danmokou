Shader "_Display/FadeOnSides" {
    Properties {
		_MainTex("Sprite Texture", 2D) = "white" {}
    	_Cutoff1("XminmaxYminmax", Vector) = (0.1,0.15,0,0.2)
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
				float4 c    : COLOR;
			};

			sampler2D _MainTex;
			float4 _Cutoff1;

			fragment vert(vertex v) {
				fragment f;
				f.loc = UnityObjectToClipPos(v.loc);
				f.uv = v.uv;
				f.c = v.color;
				return f;
			}

			float4 frag(fragment f) : SV_Target { 
				float4 c = tex2D(_MainTex, f.uv) * f.c;
				float xfill = smoothstep(_Cutoff1.x, _Cutoff1.y, 0.5-abs(0.5-f.uv.x));
				float yfill = smoothstep(_Cutoff1.z, _Cutoff1.w, 0.5-abs(0.5-f.uv.y));
				//return float4(xfill * yfill, 0, 0, 1);
				return (xfill * yfill) * c;
			}
			ENDCG
		}
	}
}