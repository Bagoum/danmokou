Shader "_SMFriendly/Indirect" {
	Properties {
		_MainTex("Texture", 2D) = "white" {}
		[PerRendererData] _InvFrameT("1/Time per Frame", Float) = 0.5
		[PerRendererData] _Frames("#Frames", Float) = 1
		[PerRendererData] _CycleSpeed("Cycle Speed", Float) = 10
		[PerRendererData] _FadeInT("Fade In Time", Float) = 1
		[PerRendererData] _SlideInT("Slide in Time", Float) = 1
		[PerRendererData] _ScaleInMin("Scale in Minimum", Float) = 1
		[PerRendererData] _ScaleInT("Scale in Time", Float) = 1
		[PerRendererData] _DisplaceTex("Displace Tex", 2D) = "white" {}
		[PerRendererData] _DisplaceMask("Displace Mask", 2D) = "white" {}
		[PerRendererData] _DisplaceMagnitude("Displace Magnitude", float) = 1
		[PerRendererData] _DisplaceSpeed("Displace Speed", float) = 1
		[PerRendererData] _DisplaceXMul("Displace X Multiplier", float) = 1
		[PerRendererData] _SharedOpacityMul("Opacity Multiplier", float) = 1
		[PerRendererData] [Enum(SrcAlpha,5,OneMinusSrcColor,6)] _BlendFrom("Blend mode from", Float) = 5
		[PerRendererData] [Enum(One,1,OneMinusSrcAlpha,10)] _BlendTo("Blend mode to", Float) = 10
		[PerRendererData] [Enum(Add,0,RevSub,2)] _BlendOp("Blend mode op", Float) = 0
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
		BlendOp [_BlendOp]
		Blend [_BlendFrom] [_BlendTo], OneMinusDstAlpha One

		Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			//If we use shader_feature, then the variants will get pruned if they're not used statically.
			//Since there is no static material, we have to use multi compile.
			#pragma multi_compile_local __ FT_ROTATIONAL
			#pragma multi_compile_local __ FT_FRAME_ANIM
			#pragma multi_compile_local __ FT_SLIDE_IN
			#pragma multi_compile_local __ FT_FADE_IN
			#pragma multi_compile_local __ FT_SCALE_IN
			//Radial/bivert not currently in use
			#pragma multi_compile_local __ FT_DISPLACE FT_DISPLACE_POLAR //FT_DISPLACE_RADIAL FT_DISPLACE_BIVERT
			#pragma multi_compile_local __ FT_TINT
			#pragma multi_compile_local __ FT_RECOLORIZE
			#pragma multi_compile_instancing
			#include "UnityCG.cginc"
			#include "Assets/Danmokou/CG/BagoumShaders.cginc"
			#pragma instancing_options procedural:setup
        #if defined(UNITY_INSTANCING_ENABLED) || defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
			#define INSTANCE_TIME timeBuffer[unity_InstanceID]
		#else
			#define INSTANCE_TIME timeBuffer[0]
		#endif
			    

			struct vertex {
				float4 loc	: POSITION;
				float2 uv	: TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};
			struct fragment {
				float4 loc	: SV_POSITION;
				float2 uv	: TEXCOORD0;
				float4 c	: COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

        CBUFFER_START(NormalData)
			float4 posDirBuffer[511];
			float4 tintBuffer[511];
			float timeBuffer[511];
        CBUFFER_END
    #ifdef FT_RECOLORIZE
        CBUFFER_START(RecolorData)
			float4 recolorBBuffer[511];
			float4 recolorWBuffer[511];
        CBUFFER_END
    #endif
			
	#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
			void setup() {
				float4 posdir = posDirBuffer[unity_InstanceID];
		#ifdef FT_ROTATIONAL
		#else
		        //Since scale is baked into the direction vector, we need to extract it like this.
		        //This makes nonrotational bullets more expensive! Don't use them for normal circles.
				posdir.zw = float2(length(posdir.zw), 0.0);
		#endif
		        SCALEIN(posdir.zw, INSTANCE_TIME);

				unity_ObjectToWorld = float4x4(
					posdir.z, -posdir.w, 0, posdir.x,
					posdir.w,  posdir.z, 0, posdir.y,
					0, 0, 1, 0,
					0, 0, 0, 1
					);
                // No WorldToObject neccessary.
			}
    #endif

			//FadeIn, frame-selection can be handled uniformly per sprite
			float _InvFrameT;
			float _Frames;
			float _SharedOpacityMul;

			fragment vert(vertex v) {
				fragment f;
				UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, f);  
				f.loc = UnityObjectToClipPos(v.loc);
				f.uv = v.uv;
				f.c = float4(1, 1, 1, _SharedOpacityMul);
		#if defined(FT_TINT) && (defined(UNITY_INSTANCING_ENABLED) || defined(UNITY_PROCEDURAL_INSTANCING_ENABLED))
				f.c *= tintBuffer[unity_InstanceID];
		#endif
				//f.uv = TRANSFORM_TEX(v.uv, _MainTex);
		#ifdef FT_FRAME_ANIM
				f.uv.x = (f.uv.x + trunc(fmod(INSTANCE_TIME * _InvFrameT, _Frames))) / _Frames;
		#endif
		        FADEIN(f.c, INSTANCE_TIME);
				return f;
			}

			float4 frag(fragment f) : SV_Target{
                UNITY_SETUP_INSTANCE_ID(f);
		        SLIDEIN(f.uv, INSTANCE_TIME);
	            DISPLACE(f.uv, INSTANCE_TIME);
				float4 c = tex2D(_MainTex, f.uv);
    #if (defined(UNITY_INSTANCING_ENABLED) || defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)) && defined(FT_RECOLORIZE)
                c.rgb = lerp(recolorBBuffer[unity_InstanceID], recolorWBuffer[unity_InstanceID], c.r).rgb;
    #endif	
				return c * f.c;
			}
			ENDCG
		}
	}
}
