Shader "_Misc/AyaPhoto" {
	Properties {
		_MainTex("Sprite Texture", 2D) = "white" {}
        _Indraw("Indraw", float) = 0.1
        _CIndraw("Indraw Color", Color) = (1,1,1,1)
        _PhotoPPU("Photo PPU", float) = 240
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


			fragment vert(vertex v) {
				fragment f;
				f.loc = UnityObjectToClipPos(v.loc);
				f.uv = v.uv;
				f.c = v.color;
				return f;
			}
            float _Indraw;
            float _PhotoPPU;
            float4 _CIndraw;
            static float ps = 2;
            static float xys = 0.01f;

			float4 frag(fragment f) : SV_Target { 
			    float2 pxy = f.uv * _MainTex_TexelSize.zw;
			    float yield = _Indraw * _PhotoPPU;
			    float4 c0 = tex2D(_MainTex, f.uv);
			    c0.a *= lerp(1, 0, smoothstep(0.5 - xys, 0.5 + xys, max(abs(f.uv.x - 0.5), abs(f.uv.y - 0.5))));
			    float mtxl = _MainTex_TexelSize.z / 2 - yield;
			    float mtyl = _MainTex_TexelSize.w / 2 - yield;
			    float4 c1 = _CIndraw;
			    c1.rgb *= c0.a;
			    float4 c = lerp(c0, c1, max(
			        smoothstep(mtxl - ps, mtxl + ps, abs(pxy.x - _MainTex_TexelSize.z / 2)),
			        smoothstep(mtyl - ps, mtyl + ps, abs(pxy.y - _MainTex_TexelSize.w / 2))
			        ));
			    return c * f.c;
			    if (pxy.x < yield || pxy.y < yield || 
			            pxy.x > _MainTex_TexelSize.z - yield || 
			            pxy.y > _MainTex_TexelSize.w - yield) return _CIndraw * f.c * c0.a;
				return c0 * f.c;
			}
			ENDCG
		}
	}
}