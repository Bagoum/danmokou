Shader "_Transition/GeneralTexMerge" {
	//Basic shader that can be used to test texture merge
	// functionality used in Background Orchestrator.
	//MainTex is ignored; this only performs a transition from _FromTex to _ToTex.
	Properties {
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "black" {}
		_FromTex("From Texture", 2D) = "black" {}
		_ToTex("To Texture", 2D) = "black" {}
		_FaderTex("Fade Texture", 2D) = "black" {}
		_T("Time", Range(0, 10)) = 1
		_MaxT("Max Transition Time", Range(0, 10)) = 1
		_A0("Angle0", Range(0, 10)) = 1
		_PMDir("PM Direction", Range(-1, 1)) = 1
		
		[Toggle(MIX_FROM_ONLY)] _Toggle2("MIX_FROM_ONLY", Float) = 0.0
		[Toggle(MIX_TO_ONLY)] _Toggle3("MIX_TO_ONLY", Float) = 0.0
		[Toggle(MIX_WIPE_TEX)] _Toggle4("MIX_WIPE_TEX", Float) = 0.0
		[Toggle(MIX_WIPE1)] _Toggle5("MIX_WIPE1", Float) = 0.0
		[Toggle(MIX_WIPE_CENTER)] _Toggle6("MIX_WIPE_CENTER", Float) = 0.0
		[Toggle(MIX_WIPE_Y)] _Toggle7("MIX_WIPE_Y", Float) = 0.0
		[Toggle(MIX_ALPHA_BLEND)] _Toggle8("MIX_ALPHA_BLEND", Float) = 0.0
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
		Blend One Zero
		
		Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_local __ MIX_FROM_ONLY MIX_TO_ONLY MIX_WIPE_TEX MIX_WIPE1 MIX_WIPE_CENTER MIX_WIPE_Y MIX_ALPHA_BLEND
            #include "UnityCG.cginc"
            #include "Assets/Danmokou/CG/TexMerge.cginc"
        
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
        
            sampler2D _MainTex; //objects captured by camera
			float4 _MainTex_TexelSize;
            sampler2D _FromTex; //main background texture
            sampler2D _ToTex; //transition target background texture

			float4 frag(fragment f) : SV_Target {
				return MERGE(_FromTex, _ToTex, f.uv);
			}
			ENDCG
		}
	}
}