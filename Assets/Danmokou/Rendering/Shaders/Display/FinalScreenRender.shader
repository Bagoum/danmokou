Shader "DMKCamera/FinalRender" {
	//See LetteredboxedInput for the code that handles dealing with mouse positions.
	Properties {
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
		_MonitorAspect("Monitor Aspect Ratio", float) = 1
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
			};

			struct fragment {
				float4 loc  : SV_POSITION;
				float2 uv	: TEXCOORD0;
			};
			
			fragment vert(vertex v) {
				fragment f;
				f.loc = UnityObjectToClipPos(v.loc);
				f.uv = v.uv - 0.5;
				return f;
			}
			
			sampler2D _MainTex;
			float4 _MainTex_TexelSize;

			float _MonitorAspect;
			
			float4 frag(fragment f) : SV_Target {
				float texAspect = _MainTex_TexelSize.z / _MainTex_TexelSize.w;
				float monitorIsWider = step(texAspect, _MonitorAspect);
				float2 scaledUv = monitorIsWider * float2(f.uv.x * _MonitorAspect / texAspect, f.uv.y) +
					(1 - monitorIsWider) * float2(f.uv.x, f.uv.y * texAspect / _MonitorAspect);
				//Scale-to-fit: draw black bars on the sides in empty area
				float4 c = lerp(float4(0, 0, 0, 1),
					tex2D(_MainTex, scaledUv + 0.5),
					step(max(abs(scaledUv.x), abs(scaledUv.y)), 0.5));
				return c;
			}
			ENDCG
		}
	}
}