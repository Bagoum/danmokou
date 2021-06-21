Shader "_SMFriendly/SpriteRenderer" {
	Properties {
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
		_SharedOpacityMul("Opacity Multiplier", float) = 1
		[Enum(SrcAlpha,5,OneMinusSrcColor,6)] _BlendFrom("Blend mode from", Float) = 5
		[Enum(One,1,OneMinusSrcAlpha,10)] _BlendTo("Blend mode to", Float) = 10
		[Enum(Add,0,RevSub,2)] _BlendOp("Blend mode op", Float) = 0
		[PerRendererData] _T("Time", Float) = 0 
		[Toggle(FT_CYCLE)] _ToggleCycle("Do Cycle?", Float) = 0
		[Toggle(FT_FADE_IN)] _ToggleFadeIn("Do FadeIn?", Float) = 0
		[Toggle(FT_HUESHIFT)] _ToggleHueShift("Do Hue Shift?", Float) = 0
		[Toggle(FT_CIRCLECUT)] _ToggleCircleCut("Do Circle Cut?", Float) = 0
		_RecolorizeB("Recolorize Black", Color) = (1, 0, 0, 1)
		_RecolorizeW("Recolorize White", Color) = (0, 0, 1, 1)
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
			#pragma multi_compile_local __ FT_CYCLE
			#pragma multi_compile_local __ FT_FADE_IN
			#pragma multi_compile_local __ FT_DISPLACE FT_DISPLACE_POLAR FT_DISPLACE_RADIAL FT_DISPLACE_BIVERT
			#pragma multi_compile_local __ FT_HUESHIFT
			#pragma multi_compile_local __ FT_RECOLORIZE
			#pragma multi_compile_local __ FT_CIRCLECUT
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
			float _SharedOpacityMul;

			fragment vert(vertex v) {
				fragment f;
				f.loc = UnityObjectToClipPos(v.loc);
				f.uv = v.uv;
                CYCLE(f.uv, _T);
				f.c = v.color * _Tint;
				f.c.a *= _SharedOpacityMul;
                FADEIN(f.c, _T);
				return f;
			}
			
        #ifdef FT_RECOLORIZE
			float4 _RecolorizeB;
			float4 _RecolorizeW;
        #endif
        
            static float circleCutSmooth = 0.002;

			float4 frag(fragment f) : SV_Target { 
	            DISPLACE(f.uv, _T);
				float4 c = tex2D(_MainTex, f.uv) * f.c;
            #ifdef FT_HUESHIFT
				c.rgb = hueShift(c.rgb, _HueShift * DEGRAD);
            #endif
            #ifdef FT_RECOLORIZE
                c.rgb = lerp(_RecolorizeB, _RecolorizeW, c.r).rgb;
            #endif
            #ifdef FT_CIRCLECUT
                float r = length(f.uv - float2(0.5, 0.5));
                c = lerp(c, float4(0,0,0,0), smoothstep(-circleCutSmooth, circleCutSmooth, r - 0.5));
            #endif
				return c;
			}
			ENDCG
		}
	}
}