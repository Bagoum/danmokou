Shader "_Test/DisplaceTest" {
	Properties {
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
		_DisplaceTex("Displacer", 2D) = "white" {}
		_DisplaceMagnitude("Displace Magnitude", Range(0,0.2)) = 0.1
		_DisplaceSpeed("Displace Speed", float) = 1
		_T("Time", Range(0,10)) = 0
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
				float4 color: COLOR;
			};

			fragment vert(vertex v) {
				fragment f;
				f.loc = UnityObjectToClipPos(v.loc);
				f.uv = v.uv;
				f.color = v.color;
				return f;
			}
			sampler2D _MainTex;
			float4 _MainTex_TexelSize;
            sampler2D _DisplaceTex;
            float _DisplaceMagnitude;
            float _DisplaceSpeed;
			float _T;
            float2 getDisplace(float2 uv, float t) {
                float2 disp = tex2D(_DisplaceTex, uv + float2(_T * _DisplaceSpeed, 0)).xy;
                disp = ((disp * 2) - 1) * _DisplaceMagnitude;
                return disp;
            }
            float2 uvToPolar(float2 uv) {
                uv -= float2(0.5, 0.5);
                //-pi to pi
                return float2(length(uv), atan2(uv.y, uv.x));
            }
            float2 polarToUV(float2 rt) {
                return float2(0.5 + rt.x * cos(rt.y), 0.5 + rt.x * sin(rt.y));
            }

			float4 frag(fragment f) : SV_Target { 
				float4 c = tex2D(_MainTex, f.uv + getDisplace(f.uv, _T));
				return c * f.color;
			}
			ENDCG
		}
	}
}