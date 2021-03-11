Shader "_Misc/ProfileMask" {
	Properties {
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
		_TrueTex("Content", 2D) = "white" {}
		_OffX("OffsetX", Range(-1,1)) = 0
		_OffY("OffsetY", Range(-1,1)) = 0
		_Zoom("Zoom", Range(0.1, 10)) = 2
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
			sampler2D _TrueTex;
			float4 _TrueTex_TexelSize;
			
			float _Zoom;
			float _OffX;
			float _OffY;


			float4 frag(fragment f) : SV_Target { 
				float4 c = f.color;
				c.a = min(c.a, tex2D(_MainTex, f.uv).r);
				float2 uv = f.uv / _Zoom * (_MainTex_TexelSize.zw / _TrueTex_TexelSize.zw);
				return c * tex2D(_TrueTex, uv + float2(_OffX, _OffY));
			}
			ENDCG
		}
	}
}