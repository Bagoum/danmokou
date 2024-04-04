Shader "_Misc/Pixelate" {
	Properties {
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
		[Enum(One,1,OneMinusSrcAlpha,10)] _BlendTo("Blend mode", Float) = 10
		_Px("Pixelize", Range(1,400)) = 50
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
		Blend SrcAlpha [_BlendTo], OneMinusDstAlpha One

		Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
            #include "Assets/Danmokou/CG/Noise.cginc"

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

			fragment vert(vertex v) {
				fragment f;
				f.loc = UnityObjectToClipPos(v.loc);
				f.uv = v.uv;
				f.c = v.color;
				return f;
			}

			sampler2D _MainTex;
			float4 _MainTex_TexelSize;
			float _Px;

			float4 frag(fragment f) : SV_Target {
				float _Py = _Px * (_MainTex_TexelSize.w / _MainTex_TexelSize.z);
				float2 uv = float2((floor(f.uv.x * _Px) + 0.5)/_Px,
						(floor(f.uv.y * _Py) + 0.5)/_Py);
			    float4 c = tex2D(_MainTex, uv) * f.c;
                return c;
			}
			ENDCG
		}
	}
}