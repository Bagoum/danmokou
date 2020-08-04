﻿Shader "Custom/PatherLightning" {
	Properties {
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
		_LNTex("Lightning Texture", 2D) = "white" {}
        _HueShift("Hue Shift", Float) = 0
		[Enum(One,1,OneMinusSrcAlpha,10)] _BlendTo("Blend mode", Float) = 10
		_BX("BlocksX", Float) = 6
		_BY("BlocksY", Float) = 4
		_T("Time", Range(0, 10)) = 1
		_TM("Time Multiplier", Range(0, 10)) = 1
		_NM("Noise Multiplier", Float) = 0.4
	}
	
	CGINCLUDE
    #pragma vertex vert
    #pragma fragment frag
    #include "UnityCG.cginc"
    #include "Assets/CG/Supernoise.cginc"
    #pragma multi_compile __ FANCY
    
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
    
    sampler2D _MainTex;
    float _T;
    float _TM;
    float _BX;
    float _BY;
    float _NM;
    sampler2D _LNTex;
    float _PPU; //Global
	float _HueShift;

    float4 fragLightning(fragment f, int ii) {
    #ifdef FANCY
        return tex2D(_LNTex, lightningDistort2(f.uv, s(f.loc.xy/_PPU, _BX, _BY), rehash(_T * _TM, ii), _NM));
    #else
        return float4(0,0,0,0);
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
		    Blend SrcAlpha One
			CGPROGRAM
		    float4 frag(fragment f) : SV_Target {
		        return fragLightning(f, 0);
		    }
		    ENDCG
		}

		Pass {
		    Blend SrcAlpha [_BlendTo]
			CGPROGRAM
			float4 frag(fragment f) : SV_Target { 
			    float4 c = tex2D(_MainTex, f.uv) * f.c;
                c.rgb = hueShift(c.rgb, _T * _HueShift);
                return c;
			}
			ENDCG
		}
		Pass {
		    Blend SrcAlpha One
			CGPROGRAM
		    float4 frag(fragment f) : SV_Target {
		        return fragLightning(f, 1);
		    }
		    ENDCG
		}
	}
}