Shader "_Misc/Voronoi" {
	Properties {
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
		[Enum(One,1,OneMinusSrcAlpha,10)] _BlendTo("Blend mode", Float) = 10
		_Speed("Speed", Range(0,10)) = 1
		_BX("BlocksX", Float) = 6
		_BY("BlocksY", Float) = 6
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
			#pragma multi_compile_local __ FT_NOISE1
			#pragma multi_compile_local __ FT_BLOCKS
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

			sampler2D _MainTex;
			fragment vert(vertex v) {
				fragment f;
				f.loc = UnityObjectToClipPos(v.loc);
				f.uv = v.uv;
				f.c = v.color;
				return f;
			}

            float _BX;
            float _BY;
			float _Speed;

			float4 frag(fragment f) : SV_Target { 
			    float2 uv = f.uv;
				float4 c = tex2D(_MainTex, uv) * f.c;
                float2 suv = s(uv, _BX, _BY);
                float3 suvt = float3(s(uv, _BX, _BY), _Time.y * _Speed);
                float3 vn = voronoi3D(suvt);
                c = lerp(
                    lerp(float4(0.4,0,0,1), float4(1,0,1,1), vn.z*2),
                    lerp(float4(0,0,0.4,1), float4(1,0,1,1), vn.z*2), vn.y);
                c.rgb *= lerp(0.2, 1, 1-vn.x);
				return c;
			}
			ENDCG
		}
	}
}