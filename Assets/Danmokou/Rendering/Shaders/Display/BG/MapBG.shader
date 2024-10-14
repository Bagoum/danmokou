Shader "_Backgrounds/MapBG"
{
    Properties {
		_MainTex("Sprite Texture", 2D) = "white" {}
        _Tint("Tint", Color) = (1,1,1,1)
    	_BX("X Blocks", Float) = 32
    	_BY("Y Blocks", Float) = 18
    	_LineColor("Line Color", Color) = (1,0,0,1)
    	_ViewportWrapColor("Viewport Wrap Color", Color) = (0,1,0,1)
    	_Viewport("Viewport", Vector) = (0.5,0.5,0.35,0.45)
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
			#include "UnityCG.cginc"

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
			float4 _MainTex_TexelSize;
			float4 _Tint;
			float _BX;
			float _BY;
			float4 _LineColor;
			float4 _ViewportWrapColor;
			float4 _Viewport;

			fragment vert(vertex v) {
				fragment f;
				f.loc = UnityObjectToClipPos(v.loc);
				f.uv = v.uv;
				f.c = v.color * _Tint;
				return f;
			}

			float4 frag(fragment f) : SV_Target { 
				float4 c = tex2D(_MainTex, f.uv);
				float xLine = smoothstep(0, 0.04, abs(0.5 - frac(f.uv.x * _BX)));
				float yLine = smoothstep(0, 0.04, abs(0.5 - frac(f.uv.y * _BY)));
				c = lerp(c, _LineColor, 1 - xLine * yLine);
				float aspect = _MainTex_TexelSize.z / _MainTex_TexelSize.w;
				float viewportX = 1-smoothstep(0, 0.025/aspect, abs(f.uv.x-_Viewport.x)-_Viewport.z);
				float viewportY = 1-smoothstep(0, 0.025, abs(f.uv.y-_Viewport.y)-_Viewport.w);
				float viewport = pow(viewportX * viewportY, 0.7);
				if (viewport > 0.94) {
					viewport = 1;
				} else {
					viewport = lerp(0, 0.9, viewport);
				}
				c = lerp(c, _ViewportWrapColor, viewport);
				return c * f.c;
			}
			ENDCG
		}
	}
}