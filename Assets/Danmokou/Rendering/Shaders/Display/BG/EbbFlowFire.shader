Shader "_Misc/EbbFlowFire" {
	Properties {
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
		[Enum(One,1,OneMinusSrcAlpha,10)] _BlendTo("Blend mode", Float) = 10
		_Speed("Speed", Float) = 1
		_BX("BlocksX", Float) = 6
		_BY("BlocksY", Float) = 6
		[Toggle(FT_VORONOI)] _DoVoronoi("Use Voronoi?", Float) = 0
		_C1("Color 1", Color) = (0.8, 0, 0, 1)
		_C2("Color 2", Color) = (0, 0.4, 0, 1)
		_C3("Color 3", Color) = (0, 0, 0.7, 1)
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
		Blend SrcAlpha [_BlendTo]

		Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_local __ FT_VORONOI
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
				float4 c    : COLOR;
			};

			sampler2D _MainTex;
			fragment vert(vertex v) {
				fragment f;
				f.loc = UnityObjectToClipPos(v.loc);
				f.uv = v.uv;
				f.c = v.color;
				return f;
			}

			float _Speed;
            float _BX;
            float _BY;
			float4 _C1, _C2, _C3;
            
            float sstep(float cmp, float g, float d) {
                return smoothstep(cmp - d/2, cmp + d/2, g);
            }

			float4 frag(fragment f) : SV_Target { 
			    float4 c = float4(1,1,1,1);
				float t = _Speed * _Time.y;
			    float2 uv = float2(f.uv.x, mod(f.uv.y - 1 - t, 1000));
                float3 suvt = float3(s(uv, _BX, _BY), t);
            #ifdef FT_VORONOI
                float noise = voronoi3D(suvt).x;
            #else
                float noise = perlin3Dlayer01(suvt);
            #endif
                //Fractal noise!
                //noise += perlin(suv.xy);
                //noise += perlin(suv * 2) * 0.5;
                //noise += perlin(suv * 4) * 0.25;
                //noise += perlin(suv * 8) * 0.125;
                float grad = 1 - pow(f.uv.y, 2) + .05 - 0.1 * cos(t / 2) + lerp(-0.5, 0, smoothstep(0, 4, t));
                //grad *= grad;
                //c.rgb *= noise;
                //return c;
                c = _C3 * sstep(noise, grad, 0.15);
                //lower bound at noise for faster border; 0 for full gradient
                c = lerp(c, _C2, smoothstep(noise-0.03, noise + 0.03, grad - 0.3));
                //c = lerp(c, float4(1,0.5,0,1), sstep(noise, grad-0.3, 0.04));
                c = lerp(c, _C1, smoothstep(noise-0.03, noise + 0.03, grad - 0.55));
                //c.rgb *= noise;
				return c;
			}
			ENDCG
		}
	}
}