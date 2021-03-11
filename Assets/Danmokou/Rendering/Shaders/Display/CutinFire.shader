Shader "_Misc/CutinFire" {
	Properties {
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
		[Enum(One,1,OneMinusSrcAlpha,10)] _BlendTo("Blend mode", Float) = 10
		_T("Time", Range(0, 5)) = 1
		_BX("BlocksX", Float) = 6
		_BY("BlocksY", Float) = 6
		_C1("Color1", Color) = (1,0,0.8,1)
		_C2("Color2", Color) = (0.4,1,0.8,1)
		_C3("Color3", Color) = (1,0.5,0,1)
		_Mult("Multiplier", Range(0.5, 2)) = 1
		_XSpeed("XSpeed", Float) = 1
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
		Blend SrcAlpha [_BlendTo], OneMinusDstAlpha One

		Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
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

            float _BX;
            float _BY;
            float _T;
            
          
            float4 _C1;
            float4 _C2;
            float4 _C3;
            float _Mult;
            
            float _XSpeed;

			float4 frag(fragment f) : SV_Target { 
			    float2 uv = f.uv - center;
			    //when this mod loops, a separation line will appear
			    //uv = float2(length(uv), (PI + atan2(uv.y, uv.x)) / TAU);
			    uv.y -= _T / 20; //rotation effect
			    uv.x += _T / 7 * _XSpeed;
			    //uv.x = mod1(uv.x, 1);
                float3 suvt = float3(s(uv, _BX, _BY), _T / 1);
                //float noise = c01(voronoi3D(suvt).z * 4);
                float noise = pm01(perlin3Dmlayer(suvt, float3(_BX, _BY, 10)));
                float grad = tex2D(_MainTex, f.uv).r;
                float ograd = grad;
                grad = 1 - pow(1-grad, _Mult);
			    float4 c = float4(1,1,1,1);
			    
			    //Using lerp over grad-X gives the result that high gradients always appear high.
			    //This is useful if modifying an existing texture.
                c = _C3 * smoothstep(noise-0.1, noise, lerp(-0.2, 1, grad));
                c = lerp(c, _C2, smoothstep(noise-0.25, noise, lerp(-1, 1, grad)));
                c = lerp(c, _C1, smoothstep(noise-0.25, noise, lerp(-5, 1.01, grad)));
                return c;
			}
			ENDCG
		}
	}
}