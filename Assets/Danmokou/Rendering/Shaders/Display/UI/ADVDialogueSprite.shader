﻿Shader "_SZYU/ADVDialogueSprite" {
	Properties {
		_MainTex("Sprite Texture", 2D) = "white" {}
        _Tint("Tint", Color) = (1,1,1,1)
		_FadeStart("Fade start point", float) = 0.8
		_FadeEnd("Fade end point", float) = 1
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

			float _FadeStart;
			float _FadeEnd;
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
				c.a *= 1 - smoothstep(_FadeStart, _FadeEnd, f.uv.x);
				return c;
			}
			ENDCG
		}
	}
}