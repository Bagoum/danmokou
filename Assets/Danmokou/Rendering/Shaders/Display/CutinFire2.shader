Shader "_Misc/CutinFire2" {
	Properties {
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
		 _Content("Texture", 2D) = "white" {}
		[Enum(One,1,OneMinusSrcAlpha,10)] _BlendTo("Blend mode", Float) = 10
		_BX("BlocksX", Float) = 6
		_BY("BlocksY", Float) = 6
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
            
            float _Mult;
            
            float _XSpeed;
            sampler2D _Content;
            
			static const float _Smooth = 0.05;

			float4 frag(fragment f) : SV_Target { 
			    float2 uv = f.uv - center;
			    //when this mod loops, a separation line will appear
			    //uv = float2(length(uv), (PI + atan2(uv.y, uv.x)) / TAU);
			    uv.y -= _Time.y / 20; //rotation effect
			    uv.x += _Time.y / 7 * _XSpeed;
			    //uv.x = mod1(uv.x, 1);
                float3 suvt = float3(s(uv, _BX, _BY), _Time.y / 1);
                //float noise = c01(voronoi3D(suvt).z * 4);
                
                //This shader is used on the main menu, so I am avoiding using Layer in order to keep execution fast on first initialization.
                float noise = pm01(perlin3Dm(suvt, float3(_BX, _BY, 10)));
                float grad = tex2D(_MainTex, f.uv).r;
                grad = pow(1-grad, _Mult);
			    
			    float4 c = tex2D(_Content, f.uv);
                c = c * smoothstep(-_Smooth, _Smooth, grad - noise);
                return c;
			}
			ENDCG
		}
	}
}