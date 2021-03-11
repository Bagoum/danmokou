Shader "_Misc/BackgroundCombiner" {
	Properties {
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
		[PerRendererData] _FromTex("From Texture", 2D) = "white" {}
		[PerRendererData] _ToTex("To Texture", 2D) = "white" {}
		[PerRendererData] _FaderTex("Fade Texture", 2D) = "white" {}
		[PerRendererData] _T("Time", Range(0, 10)) = 1
		[PerRendererData] _A0("Angle0", Range(0, 10)) = 1
		[PerRendererData] _PMDir("PM Direction", Range(0, 10)) = 1
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
		
		Pass { // From and To, Pass 0
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			//If we use shader_feature, then the variants will get pruned if they're not used statically.
			//Since there is no static material, we have to use multi compile.
			#pragma multi_compile_local __ MIX_FROM_ONLY
			#pragma multi_compile_local __ MIX_TO_ONLY
			#pragma multi_compile_local __ MIX_ALPHA_BLEND			
			#pragma multi_compile_local __ MIX_WIPE_TEX		
			#pragma multi_compile_local __ MIX_WIPE1
			#pragma multi_compile_local __ MIX_WIPE_CENTER
			#pragma multi_compile_local __ MIX_WIPE_Y
            #include "Assets/Danmokou/CG/Math.cginc"
            #include "UnityCG.cginc"
        
            struct vertex {
                float4 loc  : POSITION;
                float2 uv	: TEXCOORD0;
            };
        
            struct fragment {
                float4 loc  : SV_POSITION;
                float2 uv	: TEXCOORD0;
            };
        
            fragment vert(vertex v) {
                fragment f;
                f.loc = UnityObjectToClipPos(v.loc);
                f.uv = v.uv;
                return f;
            }
        
            sampler2D _FromTex;
            sampler2D _ToTex;
            sampler2D _FaderTex;
            float _T;
            float _MaxT;
            float _A0;
            float _PMDir;
            
            static float smooth = 0.005;
            
            float ssd(float ref, float x) {
                return smoothstep(-smooth, smooth, x - ref);
            }

			float4 frag(fragment f) : SV_Target { 
        #ifdef MIX_FROM_ONLY
				return tex2D(_FromTex, f.uv);
        #endif
        #ifdef MIX_TO_ONLY
                return tex2D(_ToTex, f.uv);
        #endif
                float ratio = 1;
                float fill = 1;
        #ifdef MIX_ALPHA_BLEND
                fill = smoothstep(0.0, _MaxT, _T);
        #endif
        #ifdef MIX_WIPE_TEX
                float grad = tex2D(_FaderTex, f.uv);
                grad = 0.5 + _PMDir * (0.5 - grad);
                fill = ssd(grad, _T / _MaxT);
        #endif
        #ifdef MIX_WIPE1
                ratio = _T / _MaxT * TAU;
                fill = ssd(mod((uvToPolar2(f.uv).y - _A0) * _PMDir, TAU), ratio);
        #endif
        #ifdef MIX_WIPE_CENTER
                fill = ssd(uvToPolar2(f.uv).x, _T / _MaxT);
        #endif
        #ifdef MIX_WIPE_Y
                fill = ssd(.5 + _PMDir * (f.uv.y - 0.5), _T/ _MaxT);
        #endif
                return tex2D(_FromTex, f.uv) * (1 - fill) + tex2D(_ToTex, f.uv) * fill;
			}
			ENDCG
		}
	}
}