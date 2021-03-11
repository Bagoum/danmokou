Shader "DMKCamera/SeijaCamera" {
	Properties {
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
		_RotateX("X Rotate", Range(0, 6.3)) = 0
		_RotateY("Y Rotate", Range(0, 6.3)) = 0
		_RotateZ("Z Rotate", Range(0, 6.3)) = 0
		_XBound("X Bound", Float) = 4
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

			float2 rotate(float2 xy, float rad) {
				const float c = cos(rad);
				const float s = sin(rad);
				return float2(xy.x * c - xy.y * s, xy.x * s + xy.y * c);
			}
			
			fragment vert(vertex v) {
				fragment f;
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
			#else
				f.uv = float2((p.x + _GlobalXOffset) / _ScreenWidth + 0.5, p.y / _ScreenHeight + 0.5);
			#endif
				f.color = v.color;
				return f;
			}
			
			sampler2D _MainTex;

			float4 frag(fragment f) : SV_Target {
				if (abs(f.uv.x - 0.5) > 0.5 || abs(f.uv.y - 0.5) > 0.5 ||
					abs(f.rloc.x) > _XBound) return float4(0,0,0,1);
				float4 c = tex2D(_MainTex, f.uv);
				return c;
			}
			ENDCG
		}
	}
}