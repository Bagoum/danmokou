Shader "_Transition/WipeTransition" {
	Properties {
		[PerRendererData] _MainTex("[Unused]", 2D) = "white" {}
		_TrueTex("Sprite Texture", 2D) = "white" {}
		[PerRendererData] _FaderTex("Fade Controller", 2D) = "white" {}
		_F("Fill Ratio", Range(0, 1)) = 0.7
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
			#pragma multi_compile_local __ FT_REVERSE
			#pragma multi_compile_local __ REQ_CIRCLE
			#pragma multi_compile_local __ REQ_Y
			#pragma multi_compile_local __ REQ_EMPTY
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
			#include "Assets/Danmokou/CG/Math.cginc"

			struct vertex {
				float4 loc  : POSITION;
				float2 uv	: TEXCOORD0;
			};

			struct fragment {
				float4 loc  : SV_POSITION;
				float2 uv	: TEXCOORD0;
			};
			

			fragment vert(vertex v) {
				fragment f;
				f.loc = UnityObjectToClipPos(v.loc);
				f.uv = v.uv;
				return f;
			}

            float _F;
			sampler2D _FaderTex;
			sampler2D _TrueTex;
			static const float _Smooth = 0.0001f;

			float4 frag(fragment f) : SV_Target {
        #ifdef REQ_EMPTY
				return float4(0,0,0,0);
        #endif
				float4 c = tex2D(_TrueTex, f.uv);
        #ifdef REQ_CIRCLE
				float req = mod1(atan2(f.uv.y - 0.5, f.uv.x - 0.5) / TAU, 1);
        #else
        #ifdef REQ_Y
				float req = 1 - f.uv.y;
        #else
				float req = tex2D(_FaderTex, f.uv).r;
        #endif
        #endif
		#ifdef FT_REVERSE
			    //c.a *= step(1-req, _F);
				c.a *= smoothstep(1-req-_Smooth, 1-req+_Smooth, _F);
        #else
			    //c.a *= step(req, _F);
				c.a *= smoothstep(req-_Smooth, req+_Smooth, _F);
        #endif
				return c;
			}
			ENDCG
		}
	}
}