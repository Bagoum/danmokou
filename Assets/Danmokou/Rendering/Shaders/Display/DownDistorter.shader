Shader "_Misc/DownDistorter" {
	Properties{
		[PerRendererData] _MainTex("Texture", 2D) = "white" {}
		_RI("Inner Radius", Float) = 1.2
		_R("Radius", Float) = 1.5
		_BX("Blocks Radial", Float) = 6
		_BY("Blocks Angular", Float) = 30
		_T("Time", Range(0,30)) = 0
		_Speed("Speed", Float) = 1
		_Magnitude("Magnitude", Float) = 0.1
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
        Blend SrcAlpha OneMinusSrcAlpha
        
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
			#pragma multi_compile __ ALLOW_DISTORTION
			#pragma multi_compile __ AYA_CAPTURE

            struct vertex {
                float4 loc  : POSITION;
                float2 uv	: TEXCOORD0;
            };

            struct fragment {
                float4 loc   : SV_POSITION;
                float2 uv	 : TEXCOORD0;
            };
            
            fragment vert(vertex v) {
                fragment f;
                f.loc = UnityObjectToClipPos(v.loc);
                f.uv = v.uv;
                return f;
            }

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _R;
            float _RI;
            static const float smth = 0.01f;
            static const float rsmth = 0.01f;
            
            float _PPU;
            float _RPPU;
            float _RenderR;
            float _ScreenWidth;
            float _ScreenHeight;//global
            float _PixelWidth;
            float _PixelHeight;//global
            float _GlobalXOffset;//global
            
            float _T;
            float _BX;
            float _BY;
            float _Speed;
            float _Magnitude;
            float _ScreenX;
            float _ScreenY;
            
            sampler2D _BGTex;
            float4 _BGTex_TexelSize;

            float4 frag(fragment f) : SV_Target {
            //For now, I still can't get Aya rendering to work... seems like it forces it to render in a smaller space
            #if defined(FANCY) && defined(ALLOW_DISTORTION) && !defined(AYA_CAPTURE)
                f.uv -= float2(0.5,0.5);
                float ang = atan2(f.uv.y, f.uv.x) / TAU; // -1/2 (@-180) to 1/2 (@180)
                //Assume that this object is square, otherwise this is incorrect
                float r = length(f.uv);
                
                float3 srt = float3(r / ISQR2 * _BX, ang * _BY, _T * _Speed);
                float noise = perlin3Dm(srt, float3(_BX, _BY, 10));
               
                r *= _MainTex_TexelSize.z * _RenderR;
                noise *= 1 - smoothstep(_RI, _R, r / _PPU);
                
                float effang = ang + noise * _Magnitude;
                
                float y = _ScreenY + r * sin(effang*TAU)/_BGTex_TexelSize.w;
                
                //for some reason, when rendering via Aya, this needs to be removed
                #if defined(UNITY_UV_STARTS_AT_TOP) && !defined(AYA_CAPTURE)
                y = 1 - y;
                #endif
                
                float4 bgc = tex2D(_BGTex, float2(_ScreenX + _GlobalXOffset / _ScreenWidth + r * cos(effang*TAU)/_BGTex_TexelSize.z, y));
                bgc.a = 1; //Don't know why it isn't already 1...
                return bgc;
                
                float4 c = float4(0,0,0,0.5);
                return c + bgc * (1-c.a);
            #else
                return float4(0,0,0,0);
            #endif
            }
            ENDCG
        }
	}
}