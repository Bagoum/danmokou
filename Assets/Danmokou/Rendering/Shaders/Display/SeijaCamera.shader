Shader "DMKCamera/SeijaCamera" {
	Properties {
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
		_ScreenRectBound("Screen Rect Bound", Vector) = (0,0,1,1)
		_RotateX("X Rotate", Range(0, 6.3)) = 0
		_RotateY("Y Rotate", Range(0, 6.3)) = 0
		_RotateZ("Z Rotate", Range(0, 6.3)) = 0
		[Toggle(FT_PIXELIZE)] _DoPixelize("Show Pixellation?", Float) = 0
		_PixelizeX("Pixelize X Blocks", Float) = 640
		_PixelizeRO("Pixelize Outer Radius", Float) = 100
		_PixelizeRI("Pixelize Inner Radius", Float) = -1
		_PixelizeCenter("Pixelize Center", Vector) = (0, -3, 0, 0)
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
		//As the source tex is a render tex accumulating
		// premulted colors, we use the merge (1 1-SrcA),
		// but since the target tex is always blank, we can optimize with Blend Off.
		Blend Off

		Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile __ AYA_CAPTURE
			#pragma multi_compile __ FT_BLACKHOLE
			#pragma multi_compile __ FT_PIXELIZE
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
				float2 rloc	: POSITION1;
			};

			float4 _MainTex_TexelSize;
			float _RotateX;
			float _RotateY;
			float _RotateZ;
			float4 _ScreenRectBound;


			float2 rotate(float2 xy, float rad) {
				const float c = cos(rad);
				const float s = sin(rad);
				return float2(xy.x * c - xy.y * s, xy.x * s + xy.y * c);
			}
			
			fragment vert(vertex v) {
				fragment f;
				f.color = v.color;
				f.loc = UnityObjectToClipPos(v.loc);
				float aspect = (_MainTex_TexelSize.z / _MainTex_TexelSize.w);
				float2 p = float2((v.uv.x - _ScreenRectBound.x) * aspect, v.uv.y -  _ScreenRectBound.y);
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
				f.uv = float2(p.x / aspect + _ScreenRectBound.x, p.y + _ScreenRectBound.y);
				return f;
			}
			

			float easer(float x01) {
				return x01 * (1-x01) + (1-pow(x01, 5)) * x01;
			}

			
			sampler2D _MainTex;
			float _PixelizeX;
			float _PixelizeRO;
			float _PixelizeRI;
			float4 _PixelizeCenter;
			float _BlackHoleT;
			float _BlackHoleAbsorbT;
			float _BlackHoleBlackT;
			float _BlackHoleFadeT;

			bool oob(float2 uv) {
				return abs(uv.x - _ScreenRectBound.x) > _ScreenRectBound.z
					|| abs(uv.y - _ScreenRectBound.y) > _ScreenRectBound.w
					|| abs(uv.x - 0.5) > 0.5 || abs(uv.y - 0.5) > 0.5;
			}

			float4 frag(fragment f) : SV_Target {
				if (oob(f.uv)) return float4(0,0,0,1);
				float aspect = _MainTex_TexelSize.z / _MainTex_TexelSize.w;
				//note: locations are in screen units (<0,0> -> <1,1>),
				//distances are in screen Y units (0->16/9 for X, 0->1 for Y)
			#if FT_BLACKHOLE
				float2 rt = rectToPolar(f.rloc);
				if (_BlackHoleT > _BlackHoleAbsorbT)
					return lerp(float4(0, 0, 0, 1), tex2D(_MainTex, f.uv), smoothstep(0, _BlackHoleFadeT, _BlackHoleT - _BlackHoleAbsorbT - _BlackHoleBlackT));
				float tr = _BlackHoleT / _BlackHoleAbsorbT;
				float bhr = lerp(0, 0.11, einsine(smoothstep(0, 0.25, tr)));
				//ehr: closeness to event horizon
				float ehr = pow(1 - smoothstep(-0.017, 0.1, rt.x - bhr), 3);
				float outside = smoothstep(-0.003, 0.003, rt.x - bhr);

				//rt.x += 2;
				float per = TAU / 4;

				float tr_delay = ratio(0.1, 1, tr);

				//Increase rotation angle, especially for closer points
				rt.y += 3 * _BlackHoleAbsorbT * einsine(tr_delay) * lerp(0.2, 1, pow(1 - smoothstep(0, 0.65, rt.x), 3)) + 2.2 * ehr;
				float rd = mod(rt.y, per) / per * TAU;
				rt.x += 0.65 * einsine(tr_delay) * lerp(0.8, 1, 0.5 + 0.5 * sin(rd));
				
				float2 xy = polarToRect(rt);
				f.uv = float2(xy.x / aspect + _ScreenRectBound.x, xy.y + _ScreenRectBound.y);
				if (oob(f.uv)) return float4(0,0,0,1);
				return lerp(float4(0, 0, 0, 1), tex2D(_MainTex, f.uv),
					outside * (1 - smoothstep(-0.4, 0, _BlackHoleT - _BlackHoleAbsorbT)));
			#endif
			#if FT_PIXELIZE
				float _Px = _PixelizeX;
				float _Py = _Px * (_MainTex_TexelSize.w / _MainTex_TexelSize.z);
				float2 puv = float2((floor(f.uv.x * _Px) + 0.5)/_Px,
						(floor(f.uv.y * _Py) + 0.5)/_Py);
				float dist = length(float2((f.uv.x - _PixelizeCenter.x)*aspect, f.uv.y-_PixelizeCenter.y));
				float enable = smoothstep(0.01, -0.01, dist - _PixelizeRO) * smoothstep(-0.01, 0.01, dist - _PixelizeRI);
				f.uv = lerp(f.uv, puv, enable);
			#endif
				return tex2D(_MainTex, f.uv);
			}
			ENDCG
		}
	}
}