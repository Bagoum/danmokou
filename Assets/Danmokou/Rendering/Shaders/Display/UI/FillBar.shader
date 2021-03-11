Shader "_Misc/FillBar" {
	Properties{
		[PerRendererData] _MainTex("Texture", 2D) = "white" {}
		_FillTex("Fill Texture", 2D) = "white" {}
		
		_YX("Yield X", Float) = 0.1
		_YY("Yield Y", Float) = 0.1
		
		_F("Fill Ratio", Range(0, 1)) = 0.7
		_FI("Inner Fill Ratio", Range(0, 1)) = 0.7
		_CF("Filled Color", Color) = (0.5,0.1,0.5,1)
		_CFI("Inner Filled Color", Color) = (0.5,0.1,0.5,1)
		_CF2("Fire Fill Color", Color) = (1,0.1,0.5,1)
		_CE("Unfilled Color", Color) = (0,0.5,0,0.4)
		_CS("Shadow Color", Color) = (0, 0, 0, 1)
		_BX("Blocks X", Float) = 6
		_BY("Blocks Y", Float) = 30
		_Speed("Speed", Float) = 1
		_FireMagnitude("Fire Magnitude", Float) = 0.005
		_OPC("Opacity Multiplier", Float) = 1
		_StartMultModifier("Empty Bar Multiplier", Float) = 1
		[Toggle(FT_FIRE)] _ToggleFire("Do Fire", Float) = 0
		
		_T("Time", Range(0,30)) = 0
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
            #include "Assets/Danmokou/CG/Noise.cginc"
            #include "UnityCG.cginc"
			#pragma multi_compile __ FANCY
			#pragma multi_compile __ FT_FIRE

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
            sampler2D _FillTex;
            float _R;
            float _F;
            float _FI;
            float4 _CF;
            float4 _CFI;
            float4 _CF2;
            float4 _CE;
            float4 _CS;
            float _YX;
            float _YY;
            
            float _PPU;
            
            float _T;
            float _BX;
            float _BY;
            float _Speed;
            float _FireMagnitude;
            float _OPC;
            float _StartMultModifier;

            float4 frag(fragment f) : SV_Target {
                float4 ce = _CE;
                float4 cf2 = _CF2;
                if (f.uv.x > _YX && f.uv.y < 1 - _YY) {
                    ce = _CE.a * _CE + _CS;
                    cf2 = _CF2.a * _CF2 + _CS;
                    if (f.uv.x < 1 - _YX && f.uv.y > _YY) {
                    } else return _CS * f.c;
                } else if (f.uv.x < 1 - _YX && f.uv.y > _YY) {
                } else  return float4(0,0,0,0);
            #if defined(FANCY) && defined(FT_FIRE)
                float3 srt = float3(f.uv.x * _BX, f.uv.y * _BY, _T * _Speed);
                float noise = perlin3Dlayer(srt);
                //Square [0,1] noise to make it have less an effect on where shifts are
                //Negative so that the noise always increases the effective size of the hp bar
                noise = pow(noise * noise, 0.7) * -_FireMagnitude;
                float grad = lerp(_StartMultModifier * _FireMagnitude, 1, f.uv.x);
                
                float4 fc = ce;
                fc = lerp(fc, cf2, smoothstep(noise-0.15, noise, _F - grad + lerp(0.15, 0.2, _F)));
                fc = lerp(fc, _CF, smoothstep(noise-0.02, noise, _F - grad));
                if (_FI > 0) {
                    fc = lerp(fc, _CFI, smoothstep(noise-0.05, noise, _F * _FI - grad));
                }
            #else
                float4 fc = lerp(ce, _CF, smoothstep(0, 0.004, _F - f.uv.x));
                fc = lerp(fc, _CFI, smoothstep(0, 0.004, _F * _FI - f.uv.x));
            #endif
                
                fc = fc * tex2D(_FillTex, f.uv);
                fc.a *= _OPC;
                fc *= smoothstep(0, 0.01, fc.a); //Sends empty pixels to 0,0,0,0
                float4 bc = tex2D(_MainTex, f.uv);
                bc *= smoothstep(0, 0.01, bc.a); //Sends empty pixels to 0,0,0,0
                return bc * bc.a + fc * (1 - bc.a) * f.c;
            }
            ENDCG
        }
	}
}