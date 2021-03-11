Shader "_Misc/SpellCircleV2" {
	Properties {
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
		_FillTex("Fill Texture", 2D) = "white" {}
		_CR("Red Color", Color) = (.8314, 0,.32157,1)
		_CG("Green Color", Color) = (.859, 0, .745, 1)
		_CB("Blue Color", Color) = (.1255,.443,1,1)
		_R("Radius", Float) = 3.2
		_Subradius("Subradius", Float) = 0.8
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
                f.uv = float2(v.uv.x - 0.5, v.uv.y - 0.5);
				f.color = v.color;
				return f;
			}

			//Global
            float _RPPU;

			sampler2D _MainTex;
			sampler2D _FillTex;
            float4 _MainTex_TexelSize;
			
			float4 _CR;
			float4 _CG;
			float4 _CB;

			float _R;
			float _Subradius;

			float4 frag(fragment f) : SV_Target {
				float r = length(f.uv) * _MainTex_TexelSize.z / _RPPU;
				float ang = atan2(f.uv.y, f.uv.x);

				r = smoothstep(_R - _Subradius, _R + _Subradius, r);
				
				float2 uv = float2(ang, r);
				
				float4 c = tex2D(_FillTex, uv) * f.color;

				float4 remap = float4(1, 1, 1, c.a);
				//Special handling for white pixels
				if (c.r < 0.99 || c.g < 0.99 || c.b < 0.99){
					remap *= c.r * _CR + c.g * _CG + c.b * _CB;
					remap.a *= 1 / (c.r + c.g + c.b);
				}
				return remap;
			}
			ENDCG
		}
	}
}