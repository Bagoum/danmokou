Shader "_Misc/Darkness" {
	Properties {
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
		_R("Radius", Float) = 0.5
		_ScaleX("Scale", Float) = 1
		_Smooth("Smoothing", Float) = 0.05
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
            #include "Assets/Danmokou/CG/Math.cginc"

			struct vertex {
				float4 loc  : POSITION;
				float2 uv	: TEXCOORD0;
				float4 color: COLOR;
			};

			struct fragment {
				float4 loc  : SV_POSITION;
				float2 uv	: TEXCOORD0;
				float4 color: COLOR;
			};

			fragment vert(vertex v) {
				fragment f;
				f.loc = UnityObjectToClipPos(v.loc);
				f.uv = v.uv;
				f.color = v.color;
				return f;
			}
			
			float _R;
			float _Smooth;
			float _ScaleX;
			sampler2D _MainTex;
            float4 _MainTex_TexelSize;
			
            float _RPPU;

			float4 frag(fragment f) : SV_Target { 
				float4 c = tex2D(_MainTex, f.uv) * f.color;
                float r = length(f.uv - center) * _MainTex_TexelSize.z / _RPPU * _ScaleX;
				c *= smoothstep(-_Smooth, _Smooth, r - _R);
				return c;
			}
			ENDCG
		}
	}
}