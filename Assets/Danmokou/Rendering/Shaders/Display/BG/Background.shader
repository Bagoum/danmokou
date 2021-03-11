Shader "_Misc/Background" {
	Properties {
		_MainTex("Sprite Texture", 2D) = "white" {}
		_SuperposeTex("Superposed Texture", 2D) = "white" { }
		_RotationSpeed("Rotation Speed", Float) = 1
		_CircularXSpeed("Move Circular Speed X", Float) = 0
		_CircularYSpeed("Move Circular Speed Y", Float) = 0
		_CircularRadius("Move Circular Radius", Float) = 0
		_ScrollX("Scroll X Speed", Float) = 0
		_ScrollY("Scroll Y Speed", Float) = 0
		_T("Time", Float) = 1
		_Tint("Tint", Color) = (1,1,1,1)
		_TintS("Tint Superposed", Color) = (1,1,1,1)
		_ZoomSpeed("Zoom Polar", Float) = 0
		_ZoomSpeedC("Zoom Cartesian", Float) = 0
		[Toggle(FT_SUPERPOSE)] _ToggleSuperpose("Do Superpose?", Float) = 0
		_HueShift("Hue Shift", Range(0, 6.3)) = 0
		[Toggle(FT_DISPLACE)] _ToggleDisplace("Do Displace?", Float) = 0
		_DisplaceTex("Displace Tex", 2D) = "white" {}
		_DisplaceMask("Displace Mask", 2D) = "white" {}
		_DisplaceMagnitude("Displace Magnitude", float) = 0
		_DisplaceSpeed("Displace Speed", float) = 0
		_DisplaceXMul("Displace X Multiplier", float) = 0
		[Toggle(FT_FADE_IN)] _ToggleFadein("Do Fadein?", Float) = 0
		_FadeInT("Fade in time", Float) = 1
		[Toggle(FT_MUL_ALPHA_BY_GRAYSCALE)] _ToggleMulAlpha("Multiply alpha by grayscale?", Float) = 0
		[Toggle(FT_GRAYSCALE)] _ToggleGrayscale("Read texture as grayscale?", Float) = 0
		[Toggle(FT_WHITE)] _ToggleWhite("Read texture as white?", Float) = 0
		
		[Toggle(FT_OVERLAY)] _ToggleOverlay("Overlay blend (use normal)?", Float) = 0
		
		[Enum(SrcAlpha,5,OneMinusSrcColor,6)] _BlendFrom("Blend mode from", Float) = 5
		[Enum(One,1,OneMinusSrcAlpha,10)] _BlendTo("Blend mode to", Float) = 10
		[Enum(Add,0,Sub,1,RevSub,2)] _BlendOp("Blend mode op", Float) = 0
	}
	
	CGINCLUDE
	
    #pragma vertex vert
    #pragma fragment frag
	#include "UnityCG.cginc"
	
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
		BlendOp [_BlendOp]
		Blend [_BlendFrom] [_BlendTo], OneMinusDstAlpha One
		
		Pass {
			CGPROGRAM
			#pragma multi_compile_local __ FT_DISPLACE
			#pragma multi_compile_local __ FT_FADE_IN
			#pragma multi_compile_local __ FT_GRAYSCALE
			#pragma multi_compile_local __ FT_WHITE
			#pragma multi_compile_local __ FT_OVERLAY
			#pragma multi_compile_local __ FT_MUL_ALPHA_BY_GRAYSCALE
			#include "Assets/Danmokou/CG/BagoumShaders.cginc"

			float4 _MainTex_ST;
			
			float _T;
			float4 _Tint;
			float _RotationSpeed;
			float _CircularXSpeed;
			float _CircularYSpeed;
			float _CircularRadius;
			float _ScrollX;
			float _ScrollY;
			float _ZoomSpeed;
			float _ZoomSpeedC;
            float _HueShift;

    
            fragment vert(vertex v) {
                fragment f;
                f.loc = UnityObjectToClipPos(v.loc);
                f.uv = TRANSFORM_TEX(v.uv, _MainTex);
                f.color = v.color;
                FADEIN(f.color, _T);
                return f;
            }
            
            float grayscale(float4 c) {
                return 0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b;
            }
            
			float4 frag(fragment f) : SV_Target { 
			    //f.uv = float2(length(f.uv) + _T / 12.0, 0.5 + atan2(f.uv.y, f.uv.x) / TAU + 0.5 * sin(_T / 24.0));
			    f.uv += _T * float2(_ScrollX, _ScrollY);
			    f.uv += _CircularRadius * float2((1-step(abs(_CircularXSpeed), 0)) * cos(_CircularXSpeed*_T), (1-step(abs(_CircularYSpeed), 0)) * sin(_CircularYSpeed*_T));
			    
			    DISPLACE(f.uv, _T);
			    
			    f.uv = uvToPolar(f.uv);
			    f.uv.y += _T * _RotationSpeed;
			    
			    if (abs(_ZoomSpeed) > 0) {
			        f.uv.x = mod(f.uv.x + _T * _ZoomSpeed, 0.5);
			    }
			    
			    //i cant make this work lmao
			    /*
			    if (abs(_ZoomSpeedC) > 0) {
			        //f.uv.x = mod(f.uv.x + _T * _ZoomSpeedC, 0.5);
			        float add = f.uv.x + _T * _ZoomSpeedC;
			        float rat = max(abs(cos(f.uv.y)), sin(f.uv.y));
			        float rd = 0.5 / rat;
			        float itrs = floor(add/rd);
			        float extra = add - itrs * rd;
			        f.uv.x = mod(add, 0.5 / rat);
			    }*/
			    
			    f.uv = polarToUV(f.uv);
			    
				float4 c = tex2D(_MainTex, f.uv);
			#ifdef FT_GRAYSCALE
			    float g = grayscale(c);
			    c.rgb = float3(g, g, g);
            #endif
            #ifdef FT_WHITE
                c.rgb = float3(1, 1, 1);
			#endif
				c *= f.color * _Tint;
				c.rgb = hueShift(c.rgb, _HueShift);
            #ifdef FT_MUL_ALPHA_BY_GRAYSCALE
                c.a *= grayscale(c);
            #endif
            #ifdef FT_OVERLAY
                float g2 = grayscale(c) * 2;
                float4 c2 = float4(0, 0, 0, 0);
                c.a *= clamp(abs(0.5 - grayscale(c)) / 0.5, 0, 1);
            #endif
				return c;
			}
			ENDCG
		}
		
		//Superposed
		Blend SrcAlpha OneMinusSrcAlpha, OneMinusDstAlpha One
		Pass {
			CGPROGRAM
            #pragma multi_compile_local __ FT_SUPERPOSE
			#include "Assets/Danmokou/CG/Math.cginc"

			sampler2D _SuperposeTex;
			float4 _SuperposeTex_ST;
           
    
            fragment vert(vertex v) {
                fragment f;
                f.loc = UnityObjectToClipPos(v.loc);
                f.uv = TRANSFORM_TEX(v.uv, _SuperposeTex);
                f.color = v.color;
                return f;
            }
    		float4 _TintS;

			float4 frag(fragment f) : SV_Target { 
			#ifdef FT_SUPERPOSE
				float4 c = tex2D(_SuperposeTex, f.uv) * _TintS;
            #else
                float4 c = float4(0,0,0,0);
			#endif
				return c;
			}
			ENDCG
		}
	}
}