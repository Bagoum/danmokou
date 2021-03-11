﻿Shader "_Misc/FillBar MoF2" {
	Properties{
		[PerRendererData] _MainTex("Texture", 2D) = "white" {}
		_F("Fill Ratio", Range(0, 1)) = 0.7
		_FI("Inner Fill Ratio", Range(0, 1)) = 0.7
		_YX("Yield X", Float) = 0.1
		_YY("Yield Y", Float) = 0.1
		
		_CF("Filled Color", Color) = (1, 1, 1, 1)
		_Threshold("Threshold", Float) = 2
		_CF2("Filled Color Over Threshold", Color) = (1, 1, 1, 1)
		_CI("Inner Filled Color", Color) = (0, 1, 1, 1)
		_CS("Shadow Color", Color) = (0, 0, 0, 1)
		
	}
    SubShader{
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
                float4 loc   : SV_POSITION;
                float2 uv	 : TEXCOORD0;
				float4 c     : COLOR;
            };

            
            fragment vert(vertex v) {
                fragment f;
                f.loc = UnityObjectToClipPos(v.loc);
                f.uv = v.uv;
                f.c = v.color;
                return f;
            }

            sampler2D _MainTex;
            
            float _F;
            float _FI;
            float _YX;
            float _YY;
            float _Threshold;
            float4 _CF;
            float4 _CF2;
            float4 _CI;
            float4 _CS;
            

            float4 frag(fragment f) : SV_Target {
                f.uv.x = 2 * abs(0.5 - f.uv.x);
                if (f.uv.x < _F - _YX && f.uv.y > _YY) {
                    return lerp(_CI, lerp(_CF, _CF2, step(_Threshold, _F)), 
                        smoothstep(-0.002, 0, f.uv.x / (1 - _YX) - _FI)) * f.c;
                }
                if (f.uv.x > _YX  && f.uv.y < 1 - _YY) return _CS * f.c;
                return float4(0,0,0,0);
            }
            ENDCG
        }
	}
}