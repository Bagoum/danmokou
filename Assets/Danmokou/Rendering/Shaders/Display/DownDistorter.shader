Shader "_Misc/DownDistorter" {
	Properties{
		[PerRendererData] _MainTex("Texture", 2D) = "white" {}
		_RI("Inner Radius", Float) = 1.2
		_R("Radius", Float) = 1.5
		_Shadow("Shadow Color", Color) = (1,0,0.8,1)
		_SRI("Shadow Inner Radius", Float) = 1.2
		_SR("Shadow Radius", Float) = 1.5
		_BX("Blocks Radial", Float) = 6
		_BY("Blocks Angular", Float) = 30
		_T("Time", Range(0,30)) = 0
		_Speed("Speed", Float) = 1
		_MagnitudeAngle("Magnitude Angle", Float) = 0.1
		_MagnitudeRadius("Magnitude Radius", Float) = 0.1
		_ScreenX("Normalized Screen X", Float) = 0.5
		_ScreenY("Normalized Screen Y", Float) = 0.5
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
        
        GrabPass {
            "_BGTex"
        }

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Assets/Danmokou/CG/Noise.cginc"
            #include "UnityCG.cginc"
			#pragma multi_compile __ FANCY
			#pragma multi_compile_local __ SHADOW_ONLY

            struct vertex {
                float4 loc  : POSITION;
                float2 uv	: TEXCOORD0;
            };

            struct fragment {
                float4 loc   : SV_POSITION;
            	//same as v.loc. For a 6u square, this would range from <-3,-3> to <3,3>
                float4 model_loc : POSITION1;
            };
            
            fragment vert(vertex v) {
                fragment f;
                f.loc = UnityObjectToClipPos(v.loc);
            	f.model_loc = v.loc;
                return f;
            }

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _R;
            float _RI;
            float4 _Shadow;
            float _SR;
            float _SRI;
            static const float smth = 0.01f;
            static const float rsmth = 0.01f;

            //6 is the screen unit size of the sprite this is normally attached to.
            // May need to be moved into a parameter for support with other configurations.
            static const float OBJECT_SIZE = 6;
            
            float _T;
            float _BX;
            float _BY;
            float _Speed;
            float _MagnitudeAngle;
            float _MagnitudeRadius;
            
            sampler2D _BGTex;
            float4 _BGTex_TexelSize;

            float4 frag(fragment f) : SV_Target {
            	float2 rt = rectToPolar(f.model_loc.xy);
	            float4 shadow = _Shadow * (1 - smoothstep(_SRI, _SR, rt.x));
            
            #if defined(FANCY) && !defined(SHADOW_ONLY)
                float3 srt = float3(rt.x / (OBJECT_SIZE*ISQR2) * _BX,
                					rt.y * _BY / TAU, _T * _Speed);
                float noise = perlin3Dm(srt, float3(_BX, _BY, 10));
                float noise2 = perlin3Dm(srt, float3(_BX, _BY, 10 / PHI));
               
                noise *= 1 - smoothstep(_RI, _R, rt.x);
                noise2 *= 1 - smoothstep(_RI, _R, rt.x);
                
                rt.y += noise * _MagnitudeAngle * TAU;
                rt.x += noise2 * _MagnitudeRadius;
            	float2 effPos = polarToRect(rt);

				float2 loc = ComputeGrabScreenPos(UnityObjectToClipPos(
					float4(effPos.x, effPos.y, 0, 1)));
            	float4 bgc = tex2D(_BGTex, loc.xy);
                
                //bgc.a = 1; //This may not be 1 (if the background isn't full opacity),
            	// but if you lighten it, it won't match!
                return shadow + bgc * (1 - shadow.a) + float4(0.3, 0, 0, 0);
            #else
                return shadow;
            #endif
            }
            ENDCG
        }
	}
}