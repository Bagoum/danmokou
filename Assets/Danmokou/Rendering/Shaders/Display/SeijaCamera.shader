Shader "DMKCamera/SeijaCamera" {
	Properties {
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
		_RotateX("X Rotate", Range(0, 6.3)) = 0
		_RotateY("Y Rotate", Range(0, 6.3)) = 0
		_RotateZ("Z Rotate", Range(0, 6.3)) = 0
		_XBound("X Bound", Float) = 4
		_YBound("Y Bound", Float) = 4
		[Toggle(FT_BLACKHOLE)] _DoBlackHole("Show Black Hole?", Float) = 0
		_BlackHoleT("Black Hole Time", Range(0, 10)) = 0
		_BlackHoleAbsorbT("Black Hole Absorb Time", Float) = 5
		_BlackHoleBlackT("Black Hole Black Time", Float) = 1
		_BlackHoleFadeT("Black Hole Fade Time", Float) = 2
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
			#pragma multi_compile __ AYA_CAPTURE
			#pragma multi_compile __ FT_BLACKHOLE
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
				float4 color: COLOR;
				float2 rloc	: TEXCOORD1;
			};

			//Globals
            float _ScreenWidth;
			float _ScreenHeight;
			float _GlobalXOffset;

			float _RotateX;
			float _RotateY;
			float _RotateZ;
			float _XBound;
			float _YBound;


			float2 rotate(float2 xy, float rad) {
				const float c = cos(rad);
				const float s = sin(rad);
				return float2(xy.x * c - xy.y * s, xy.x * s + xy.y * c);
			}
			
			fragment vert(vertex v) {
				fragment f;
				f.color = v.color;
				f.loc = UnityObjectToClipPos(v.loc);
				float2 p = float2((v.uv.x - 0.5) * _ScreenWidth - _GlobalXOffset,
					(v.uv.y - 0.5) * _ScreenHeight);
				//Rotation process is slightly strange, since we are trying to effectively rotate
				// the source screen by rotating the sampling vector.
				// For Z-rotation, this is done by rotating in the other direction.
				// For X/Y-rotation, this is done by dividing *only the non-Z component* by the cosine of rotation.
				// The Z-component must be ignored at every step because it is discarded in the sampling process.
				p = rotate(p, -_RotateZ);
				p.y *= 1 / cos(_RotateX);
				p.x *= 1 / cos(_RotateY);
				f.rloc = p;
			#if AYA_CAPTURE
				//Do not perform screen flipping during player camera capture.
				// This makes the resulting photo unflipped, but located in the correct position.
				// Note that this is not a unique semantic solution to player camera capture during screen flipping,
				// but it is the easiest to deal with.
				f.uv = v.uv;
				return f;
			#endif
				f.uv = float2((p.x + _GlobalXOffset) / _ScreenWidth + 0.5, p.y / _ScreenHeight + 0.5);
				return f;
			}
			
			sampler2D _MainTex;

			float easer(float x01) {
				return x01 * (1-x01) + (1-pow(x01, 5)) * x01;
			}

			
			float _BlackHoleT;
			float _BlackHoleAbsorbT;
			float _BlackHoleBlackT;
			float _BlackHoleFadeT;

			float4 frag(fragment f) : SV_Target {
				if (abs(f.uv.x - 0.5) > 0.5 || abs(f.uv.y - 0.5) > 0.5 ||
					abs(f.rloc.x) > _XBound || abs(f.rloc.y) > _YBound) return float4(0,0,0,1);
			#if FT_BLACKHOLE
				float2 rt = rectToPolar(f.rloc);
				if (_BlackHoleT > _BlackHoleAbsorbT)
					return lerp(float4(0, 0, 0, 1), tex2D(_MainTex, f.uv), smoothstep(0, _BlackHoleFadeT, _BlackHoleT - _BlackHoleAbsorbT - _BlackHoleBlackT));
				float tr = _BlackHoleT / _BlackHoleAbsorbT;
				float bhr = lerp(0, 1, einsine(smoothstep(0, 0.25, tr)));
				float bhe = 0.6;

				float ehr = pow(1 - smoothstep(-bhe * 0.2, bhe, rt.x - bhr), 3);
				float blackness = smoothstep(-0.02, 0.02, rt.x - bhr);

				//rt.x += 2;
				float per = TAU / 4;

				float tr_delay = ratio(0.1, 1, tr);

				
				rt.y += 3 * _BlackHoleAbsorbT * einsine(tr_delay) * lerp(0.2, 1, pow(1 - smoothstep(0, 6, rt.x), 3)) + 2.2 * ehr;
				float rd = mod(rt.y + 0 * per / 2, per) / per * TAU;
				rt.x += 6 * einsine(tr_delay) * lerp(0.8, 1, 0.5 + 0.5 * sin(rd));


				//return float4(1 - abs(rt.x) / 4, 0, smoothstep(-PI/2, PI/2, rt.y), 1);
				float2 xy = polarToRect(rt);
				f.uv = float2((xy.x + _GlobalXOffset) / _ScreenWidth + 0.5, xy.y / _ScreenHeight + 0.5);
				if (abs(f.uv.x - 0.5) > 0.5 || abs(f.uv.y - 0.5) > 0.5 ||
					abs(xy.x) > _XBound || abs(xy.y) > _YBound) return float4(0,0,0,1);
				float4 c_ = tex2D(_MainTex, f.uv);
				return lerp(float4(0, 0, 0, 1), c_, blackness * (1 - smoothstep(-0.4, 0, _BlackHoleT - _BlackHoleAbsorbT)));
				
			#endif
				float4 c = tex2D(_MainTex, f.uv);
				return c;
			}
			ENDCG
		}
	}
}