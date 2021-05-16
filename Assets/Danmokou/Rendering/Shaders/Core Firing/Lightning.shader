﻿Shader "Custom/PatherLightning" {
	Properties {
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
		_LNTex("Lightning Texture", 2D) = "white" {}
		_DisplaceTex("(Low-Res) Displacement Texture", 2D) = "white" {}
        _Tint("Tint", Color) = (1,1,1,1)
        _HueShift("Hue Shift", Float) = 0
		[Enum(One,1,OneMinusSrcAlpha,10)] _BlendTo("Blend mode", Float) = 10
		_BX("BlocksX", Float) = 6
		_BY("BlocksY", Float) = 4
		_T("Time", Range(0, 10)) = 1
		_TM("Time Multiplier", Range(0, 10)) = 1
		_NM("Noise Multiplier", Float) = 0.4
		_RecolorizeB("Recolorize Black", Color) = (1, 0, 0, 1)
		_RecolorizeW("Recolorize White", Color) = (0, 0, 1, 1)
	}
	
	CGINCLUDE
    #pragma vertex vert
    #pragma fragment frag
    #include "UnityCG.cginc"
    #include "Assets/Danmokou/CG/Supernoise.cginc"
    #pragma multi_compile __ FANCY
    #pragma multi_compile_local __ FT_RECOLORIZE
    
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
	
	float4 _Tint;

    fragment vert(vertex v) {
        fragment f;
        f.loc = UnityObjectToClipPos(v.loc);
        f.uv = v.uv;
        f.c = v.color * _Tint;
        return f;
    }
    
    sampler2D _MainTex;
    float _T;
    float _TM;
    float _BX;
    float _BY;
    float _NM;
    sampler2D _LNTex;
    sampler2D _DisplaceTex;
    float _PPU; //Global
	float _HueShift;
	static float _DisplaceSpeed = 0.4;
	static float _DisplaceMagnitude = 1;

    float4 fragLightning(fragment f, int ii) {
    #ifdef FANCY
        return tex2D(_LNTex, lightningDistort2(f.uv, s(f.loc.xy/_PPU, _BX, _BY), rehash(_T * _TM, ii), _NM));
    #else
        float disp = tex2D(_DisplaceTex, f.loc.xy/_PPU * 0.3 + float2(0, ii * _DisplaceSpeed)).x;
        disp = ((disp * 2) - 1) * _DisplaceMagnitude;
        f.uv.y += disp * (1-cos(HPI * f.uv.x)) * _NM * cos(PI * (f.uv.y - 0.5));
        return tex2D(_LNTex, f.uv);
    #endif
    }
	
	ENDCG
	
	SubShader {
		Tags {
			"RenderType" = "Transparent"
			"IgnoreProjector" = "True"
			"Queue" = "Transparent"
		}
		Cull Off
		Lighting Off
		ZWrite Off
		
		Pass {
		    Blend SrcAlpha One, OneMinusDstAlpha One
			CGPROGRAM
		    float4 frag(fragment f) : SV_Target {
		        return fragLightning(f, 0);
		    }
		    ENDCG
		}

		Pass {
		    Blend SrcAlpha [_BlendTo], OneMinusDstAlpha One
			CGPROGRAM
			
        #ifdef FT_RECOLORIZE
			float4 _RecolorizeB;
			float4 _RecolorizeW;
        #endif

			float4 frag(fragment f) : SV_Target { 
			    float4 c = tex2D(_MainTex, f.uv) * f.c;
                c.rgb = hueShift(c.rgb, _HueShift * DEGRAD);
            #ifdef FT_RECOLORIZE
                c.rgb = lerp(_RecolorizeB, _RecolorizeW, c.r).rgb;
            #endif
                return c;
			}
			ENDCG
		}
		Pass {
		    Blend SrcAlpha One, OneMinusDstAlpha One
			CGPROGRAM
		    float4 frag(fragment f) : SV_Target {
		        return fragLightning(f, 1);
		    }
		    ENDCG
		}
	}
}