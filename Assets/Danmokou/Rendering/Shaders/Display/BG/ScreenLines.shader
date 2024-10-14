Shader "_Transition/ScreenLines" {
	Properties {
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
		_F("Fill Ratio", Range(0, 1)) = 0.7
		//When direction mult is -1, the fill will "recede" instead of "advance".
		_FMult("Direction Mult", Range(-1, 1)) = 1
		_XMult("X Mult", Range(-1, 1)) = 1
		_B("Blocks", Float) = 6
		_Speed("Speed", Float) = 6
		_Magnitude("Magnitude", Float) = 6
		//Positive bias makes the bottom triangle take up more space
		_Bias("Bias", Float) = 0.1
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
            #include "Assets/Danmokou/CG/Noise.cginc"

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

            float _F;
			float _FMult;
			sampler2D _MainTex;

			float _XMult;
            float _B;
            float _Speed;
            float _Magnitude;
			float _Bias;
			
			static const float _Smooth = 0.03f;

			float fillcmp(float2 uv, float pmnoise) {
				return (1-uv.y)+0.4*(1-uv.x)+pmnoise*_Magnitude;
			}

			float4 frag(fragment f) : SV_Target { 
				float2 uv = f.uv - float2(0,_Bias);
				if (_XMult < 0) {
					uv.x = 1-uv.x;
				}
				if (uv.y < uv.x) {
					uv = float2(1,1) - uv;
				}
				float noise = perlin(float2(_B * (uv.y - uv.x), _Time.y * _Speed));
				noise = sign(noise)* (1 - pow(1-abs(noise), 2));
				float fill = smoothstep(-_Smooth, _Smooth, _FMult * (_F-
					ratio(fillcmp(float2(1,1),-1), fillcmp(float2(0,0),1), fillcmp(uv, noise))));
				float4 c = tex2D(_MainTex, f.uv) * f.color;
				c.a *= fill;
				return c;
			}
			ENDCG
		}
	}
}