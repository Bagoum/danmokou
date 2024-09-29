Shader "_Misc/ViewfinderRender" {
	Properties {
		_MainTex("Sprite Texture", 2D) = "white" {}
        _Tint("Tint", Color) = (1,1,1,1)
		_OffX("X Offset", Float) = 0.5
		_OffY("Y Offset", Float) = 0.5
		_ScaleX("X Scale", Float) = 1
		_ScaleY("Y Scale", Float) = 1
		_Angle("Angle", Float) = 0
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
		//As the source texes are render textures accumulating
		// premulted colors, we use the merge (1 1-SrcA),
		// but since the target tex is always blank, we can optimize with Blend Off.
		Blend Off

		Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
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
			
			float _OffX;
			float _OffY;
			float _ScaleX;
			float _ScaleY;
			float _Angle;

			fragment vert(vertex v) {
				fragment f;
				f.loc = UnityObjectToClipPos(v.loc);
				f.c = v.color * _Tint;
				float texAspect = _MainTex_TexelSize.z / _MainTex_TexelSize.w;
				f.uv = v.uv;
				f.uv -= float2(0.5, 0.5);
				f.uv *= float2(texAspect, 1);
				f.uv *= float2(_ScaleX, _ScaleY);
				f.uv = rot2(_Angle, f.uv);
				f.uv /= float2(texAspect, 1);
				f.uv += float2(0.5, 0.5);
				f.uv += float2(_OffX, _OffY);
				return f;
			}

			float4 frag(fragment f) : SV_Target { 
			    if (max(f.uv.x, f.uv.y) > 1 || min(f.uv.x, f.uv.y) < 0) return float4(0,0,0,1);
				float4 c = tex2D(_MainTex, f.uv) * f.c;
				//The source is a render texture using premult colors and the output is consumed
				// as an exported texture, so convert back to linear colors.
				if (c.a > 0)
					c.rgb /= c.a;
				return c;
			}
			ENDCG
		}
	}
}