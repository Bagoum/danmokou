Shader "_Misc/Rainy" {
	Properties {
		_MainTex("Sprite Texture", 2D) = "white" {}
		_BX("X blocks", float) = 8
		_BY("Y blocks", float) = 4
		_Speed("Speed", float) = 1
		_Distort("Distortion", float) = -4
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

			float _T;
			float _BX, _BY, _Speed, _Distort;

			fragment vert(vertex v) {
				fragment f;
				f.loc = UnityObjectToClipPos(v.loc);
				f.uv = v.uv;
				f.c = v.color;
				return f;
			}

			float3 npass(float2 fuv, float t) {
				float2 aspect = float2(_BY / _BX * _MainTex_TexelSize.z / _MainTex_TexelSize.w, 1);
				float2 uvScroll = float2(0, 0.24 * t);
				float2 uv = fuv * float2(_BX, _BY) + uvScroll;
				float2 block = floor(uv);
				if (block.y < _BY)
					return float3(0, 0, 0);
				float2 cellUv = frac(uv) - 0.5;
				float noise = hash21(block);
				t += noise * TAU;
				
				float2 dropletOffset = float2(
					z1pm(noise) * 0.4 + (sin(fuv.y*30)*pow(sin(fuv.y*10),6))*0.2,
					sin(t+sin(t+sin(t)*0.5)) * 0.45);
				dropletOffset.x = pm1SigmoidBound(dropletOffset.x, 3, 0.38);
				dropletOffset.y += (cellUv.x+dropletOffset.x) * (cellUv.x+dropletOffset.x);
				
				float2 dropletPos = (cellUv + dropletOffset) * aspect;
				float droplet = smoothstep(.06, .04, length(dropletPos));

				float2 trailPos = (cellUv + float2(dropletOffset.x, 0)) * aspect;
				float trailMask = saturate(smoothstep(-0.05, 0.05, dropletPos.y) + smoothstep(0.05, 0.03, length(dropletPos))) *
					smoothstep(0.5, -dropletOffset.y, cellUv.y);
				float2 trailDropletPos = float2(trailPos.x, (frac((trailPos.y - uvScroll.y) * 8) - 0.5) / 8);
				float trailDroplet = smoothstep(0.03, 0.01, length(trailDropletPos)) * trailMask * 0;
				float fog = smoothstep(0.055, 0.036, abs(trailPos.x)) * trailMask;
				return float3((dropletPos * droplet + trailDropletPos * trailDroplet) * _Distort, fog);
			}

			float4 frag(fragment f) : SV_Target {
				float t = fmod(_Time.y * _Speed, 3600);
				
				float3 layer = npass(f.uv, t);
				//layer += npass(f.uv * 1.37 + 1.37 * PHI, t);
				layer += npass(f.uv * 0.82 + 0.82 * PHI, t);
				layer += npass(f.uv * IPHI + 1, t);
				
				float4 c = tex2Dlod(_MainTex, float4(f.uv +
					lerp(0, layer.xy, smoothstep(1, 7, t)), 0, 5 * lerp(0.6, (1 - layer.z), smoothstep(-2, 8, t)))) * f.c;
				//c.rgb = 0;
				//c += droplet + trailDroplet + fog;
				//if (cellUv.x > 0.49 || cellUv.y > 0.49)
				//	c.r = 1;
				return c;
			}
			ENDCG
		}
	}
}