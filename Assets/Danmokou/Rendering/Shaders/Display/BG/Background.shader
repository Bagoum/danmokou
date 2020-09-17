Shader "_Misc/Background" {
	Properties {
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
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
		Blend SrcAlpha OneMinusSrcAlpha
		
		Pass {
			CGPROGRAM
			#include "Assets/Danmokou/CG/Math.cginc"

			sampler2D _MainTex;
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

    
            fragment vert(vertex v) {
                fragment f;
                f.loc = UnityObjectToClipPos(v.loc);
                f.uv = TRANSFORM_TEX(v.uv, _MainTex);
                f.color = v.color;
                return f;
            }
            
			float4 frag(fragment f) : SV_Target { 
			    //f.uv = float2(length(f.uv) + _T / 12.0, 0.5 + atan2(f.uv.y, f.uv.x) / TAU + 0.5 * sin(_T / 24.0));
			    f.uv += _T * float2(_ScrollX, _ScrollY);
			    f.uv += _CircularRadius * float2((1-step(abs(_CircularXSpeed), 0)) * cos(_CircularXSpeed*_T), (1-step(abs(_CircularYSpeed), 0)) * sin(_CircularYSpeed*_T));
			    
			    
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
			    
				float4 c = tex2D(_MainTex, f.uv) * f.color * _Tint;
				return c;
			}
			ENDCG
		}
		
		//Superposed
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