Shader "_SMFriendly/TileRenderer PROOF OF CONCEPT" {
	Properties {
	// THIS IS A PROOF OF CONCEPT AND MAY NOT BE UPDATED
		_MainTex("Sprite Texture", 2D) = "white" {}
        _Tint("Tint", Color) = (1,1,1,1)
        _HueShift("Hue Shift", Float) = 0
		//SpriteRenderer sets color in the vertex struct.
		_CycleSpeed("Cycle Speed", Float) = 10
		_FadeInT("Fade In Time", Float) = 1
		_DisplaceTex("Displace Tex", 2D) = "white" {}
		_DisplaceMask("Displace Mask", 2D) = "white" {}
		_DisplaceMagnitude("Displace Magnitude", float) = 1
		_DisplaceSpeed("Displace Speed", float) = 1
		_DisplaceXMul("Displace X Multiplier", float) = 1
		[Enum(SrcAlpha,5,OneMinusSrcColor,6)] _BlendFrom("Blend mode from", Float) = 5
		[Enum(One,1,OneMinusSrcAlpha,10)] _BlendTo("Blend mode to", Float) = 10
		[Enum(Add,0,RevSub,2)] _BlendOp("Blend mode op", Float) = 0
		[PerRendererData] _T("Time", Float) = 0 
		[Toggle(FT_CYCLE)] _ToggleCycle("Do Cycle?", Float) = 0
		[Toggle(FT_FADE_IN)] _ToggleFadeIn("Do FadeIn?", Float) = 0
		[Toggle(FT_HUESHIFT)] _ToggleHueShift("Do Hue Shift?", Float) = 0
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
		BlendOp [_BlendOp]
		Blend [_BlendFrom] [_BlendTo], OneMinusDstAlpha One

		Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.5
			#pragma multi_compile_local __ FT_CYCLE
			#pragma multi_compile_local __ FT_FADE_IN
			#pragma multi_compile_local __ FT_DISPLACE
			#pragma multi_compile_local __ FT_DISPLACE_POLAR
			#pragma multi_compile_local __ FT_DISPLACE_BIVERT
			#pragma multi_compile_local __ FT_HUESHIFT
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

			float _T;
			float4 _Tint;
			float _HueShift;
			
			//float4 locUVBuffer[511];
	// THIS IS A PROOF OF CONCEPT AND MAY NOT BE UPDATED

			fragment vert(vertex v, uint idx : SV_VertexID) {
				fragment f;
				f.loc = v.loc;
				//f.loc.xy = locUVBuffer[idx].xy;
				//f.loc = UnityObjectToClipPos(f.loc);
				//f.uv = locUVBuffer[idx].zw;
				f.loc = v.loc;
				f.uv = v.uv;
                CYCLE(f.uv, _T);
				f.c = v.color * _Tint;
                FADEIN(f.c, _T);
				return f;
			}
	// THIS IS A PROOF OF CONCEPT AND MAY NOT BE UPDATED

			float4 frag(fragment f) : SV_Target { 
	            DISPLACE(f.uv, _T);
				float4 c = tex2D(_MainTex, f.uv) * f.c;
            #ifdef FT_HUESHIFT
				c.rgb = hueShift(c.rgb, _HueShift * DEGRAD);
            #endif
				return c;
			}
			ENDCG
		}
	}
}