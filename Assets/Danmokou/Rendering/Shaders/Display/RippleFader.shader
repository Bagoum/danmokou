Shader "_Misc/Ripple Fade" {
	Properties {
		_MainTex("Sprite Texture", 2D) = "white" {}
		_WLen("Wavelength", float) = 1
		_TStart("Start time", float) = 1
		_Speed("Speed", float) = 1
		_TRipple("Time for rippling", float) = 1
		_Displace("Displace magnitude", float) = 1
		
		_TScrub("Time scrubber", float) = 0
		[Toggle(FT_FADE_IN)] _ToggleFadeIn("Do FadeIn?", Float) = 1
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
			#pragma multi_compile_local __ FT_FADE_IN
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

			float _TScrub, _TStart, _TRipple, _Speed;
			float _WLen, _Displace;

			fragment vert(vertex v) {
				fragment f;
				f.loc = UnityObjectToClipPos(v.loc);
				f.uv = v.uv;
				f.c = v.color;
				return f;
			}

			float4 frag(fragment f) : SV_Target {
				float t = (_Time.y - _TStart) * _Speed + _TScrub;

				float2 uv = f.uv - 0.5;
				uv.x *= _MainTex_TexelSize.z / _MainTex_TexelSize.w;

				float d = length(uv);
				float effTime = lerp(0, t-d, smoothstep(-0.05, 0.05, t-d));
				float dispMag = _Displace * sin(effTime * TAU / _WLen);
				if (_TRipple > 0)
					dispMag *= smoothstep(1, -1, t - d - _TRipple);

				float2 disp = dispMag * normalize(uv);
				
				
				float4 c = tex2D(_MainTex, f.uv + disp) * f.c;
			#ifdef FT_FADE_IN
				c.a *= lerp(0, 1, smoothstep(0, 0.3, t-d));
			#endif
				//c.rgb = 0;
				//c.rg += abs(disp);
				//if (cellUv.x > 0.49 || cellUv.y > 0.49)
				//	c.r = 1;
				return c;
			}
			ENDCG
		}
	}
}