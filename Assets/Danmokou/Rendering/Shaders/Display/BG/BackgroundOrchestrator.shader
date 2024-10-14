Shader "_Backgrounds/BackgroundOrchestrator" {
	Properties {
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "black" {}
		_FromTex("From Texture", 2D) = "black" {}
		_ToTex("To Texture", 2D) = "black" {}
		_FaderTex("Fade Texture", 2D) = "black" {}
		_T("Time", Range(0, 10)) = 1
		_MaxT("Max Transition Time", Range(0, 10)) = 1
		_A0("Angle0", Range(0, 10)) = 1
		_PMDir("PM Direction", Range(-1, 1)) = 1
		_ScreenCenterOffset("Screen Center", Vector) = (0,0,0,0)
		
		_DistortCenter("Distortion Center", Vector) = (0.5,0.5,0,0)
		_RI("Distortion Inner Radius", Float) = 1.2
		_R("Distortion Radius", Float) = 1.5
		_Shadow("Distortion Shadow Color", Color) = (1,0,0.8,1)
		_SRI("Distortion Shadow Inner Radius", Float) = 1.2
		_SR("Distortion Shadow Radius", Float) = 1.5
		_BX("Distortion Blocks Radial", Float) = 6
		_BY("Distortion Blocks Angular", Float) = 30
		_DistortT("Distortion Time", Range(0,30)) = 0
		_Speed("Distortion Speed", Float) = 1
		_MagnitudeAngle("Distortion Magnitude Angle", Float) = 0.1
		_MagnitudeRadius("Distortion Magnitude Radius", Float) = 0.1
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
			//If we use shader_feature, then the variants will get pruned if they're not used statically.
			//Since there is no static material, we have to use multi compile.
			#pragma multi_compile_local __ NO_BG_RENDER MIX_FROM_ONLY MIX_TO_ONLY MIX_WIPE_TEX MIX_WIPE1 MIX_WIPE_CENTER MIX_WIPE_Y MIX_ALPHA_BLEND
			#pragma multi_compile_local __ SHADOW_ONLY SHADOW_AND_DISTORT
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
			float4 _ScreenCenterOffset; //UV offset of BG camera offset
			float4 _DistortCenter; //UV offset of distortion center
			
            float _DistortT;
            float _R;
            float _RI;
            float4 _Shadow;
            float _SR;
            float _SRI;
            float _BX;
            float _BY;
            float _Speed;
            float _MagnitudeAngle;
            float _MagnitudeRadius;

			float4 frag(fragment f) : SV_Target {
		#ifdef NO_BG_RENDER
				return opaque(tex2D(_MainTex, f.uv));
		#endif
				float4 shadow = float4(0,0,0,0);
				float2 sampleUV;
		#if (SHADOW_ONLY || SHADOW_AND_DISTORT)
				float aspect = _MainTex_TexelSize.z / _MainTex_TexelSize.w;
				float2 rt = rectToPolar(uvToYScreenCoord(f.uv, _DistortCenter, aspect));
				shadow = _Shadow * (1 - smoothstep(_SRI, _SR, rt.x));
			#ifdef SHADOW_AND_DISTORT
				if (rt.x < _R) {
					float3 srt = float3(rt.x / (_R*SQR2) * _BX, rt.y * _BY / TAU, _DistortT * _Speed);
					float2 noise = (1-smoothstep(_RI, _R, rt.x)) * float2(
						perlin3Dm(srt, float3(_BX, _BY, 10 / PHI)) * _MagnitudeRadius,
						perlin3Dm(srt, float3(_BX, _BY, 10)) * _MagnitudeAngle * TAU
					);
					sampleUV = yScreenCoordToUV(polarToRect(rt+noise), _DistortCenter, aspect);
				} else
			#endif
					sampleUV = f.uv;
		#else
				sampleUV = f.uv;
		#endif 
				float2 bguv = sampleUV - _ScreenCenterOffset.xy;
				float4 bgc = MERGE(_FromTex, _ToTex, bguv);
				//shadow renders on top of background
				bgc = shadow + (1-shadow.a) * bgc;
				float4 mc = tex2D(_MainTex, f.uv);
				//normally we would just do mc + (1-mc.a)*bgc,
				//but we also want to clear to opaque black once backgrounds are rendered
				return float4(mc.rgb + (1-mc.a)*bgc.rgb, 1);
			}
			ENDCG
		}
	}
}