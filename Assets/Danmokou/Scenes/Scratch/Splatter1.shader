Shader "BasicShader/Splatter1" {
	Properties {
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
		[Enum(One,1,OneMinusSrcAlpha,10)] _BlendTo("Blend mode", Float) = 10
		_T("Time", Float) = 1
		_F("Fill", Range(0, 1)) = 0.4
		_BX("BlocksX", Float) = 6
		_BY("BlocksY", Float) = 6
		[Toggle(FT_VORONOI)] _DoVoronoi("Use Voronoi?", Float) = 0
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
			#pragma multi_compile_local __ FT_VORONOI
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

			float _T;
			float _F;
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

			float4 frag(fragment f) : SV_Target { 
			    float2 uv = f.uv;
				float4 c = tex2D(_MainTex, uv) * f.c;
                float3 suvt = float3(s(uv, _BX, _BY), _T + _Time.y);
            #ifdef FT_VORONOI
                float noise = voronoi3D(suvt).x;
            #else
                float noise = perlin3D01(suvt);
            #endif
                noise = lerp(0, _F, noise);
            
                c = lerp(c, float4(0,1,.6,1), smoothstep(.35, .55, noise));
                c = lerp(c, float4(0,0.6, 1,1), smoothstep(.65, .85, noise));
				return c;
			}
			ENDCG
		}
	}
}