Shader "_Misc/Fragment" {
    Properties {
        _MainTex("Texture", 2D) = "white" {}
		[Toggle(FT_MOD)] _ToggleMod("Modularize UV?", Float) = 0
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
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
			#pragma multi_compile_local __ FT_MOD
            #include "UnityCG.cginc"
            #include "Assets/Danmokou/CG/Math.cginc"
            
            struct vertex {
                float4 loc  : POSITION;
                float2 uv   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct fragment {
                float4 loc  : SV_POSITION;
                float2 uv   : TEXCOORD0;
                float2 buv   : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            /*
            void setup() {
	#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                float4 posUv = posUvBuffer[unity_InstanceID];
                float3 rotation = rotationBuffer[unity_InstanceID]; //rot.w is the polygon rotation
                
                float cx = cos(rotation.x);
                float sx = sin(rotation.x);
                float cy = cos(rotation.y);
                float sy = sin(rotation.y);
                float cz = cos(rotation.z);
                float sz = sin(rotation.z);
                
                // ZXY rotation. See https://www.wolframalpha.com/input/?i=%7B%7Bcos%28gamma%29%2C-sin%28gamma%29%2C0%7D%2C%7Bsin%28gamma%29%2Ccos%28gamma%29%2C0%7D%2C%7B0%2C0%2C1%7D%7D+%7B%7B1%2C0%2C0%7D%2C%7B0%2Ccos%28alpha%29%2C-sin%28alpha%29%7D%2C%7B0%2Csin%28alpha%29%2Ccos%28alpha%29%7D%7D+%7B%7Bcos%28beta%29%2C0%2Csin%28beta%29%7D%2C%7B0%2C1%2C0%7D%2C%7B-sin%28beta%29%2C0%2Ccos%28beta%29%7D%7D
                unity_ObjectToWorld = float4x4(
                    cy*cz-sx*sy*sz, -cx*sz, cz*sy+cy*sx*sz, posUv.x,
                    cy*sz+sx*sy*cz, cx*cz, sz*sy-cy*sx*cz, posUv.y,
                    -cx*sy, sx, cx*cy, 0,
                    0, 0, 0, 1
                    );
    #endif
            }*/
            
            sampler2D _MainTex;
            float _TexWidth;
            float _TexHeight;
            float _FragDiameter;
            float _FragSides;
            
            float4 uvRBuffer[511];
            
        #ifdef UNITY_INSTANCING_ENABLED
			#define UVR uvRBuffer[unity_InstanceID]
		#else
			#define UVR uvRBuffer[0]
		#endif
            
            fragment vert(vertex v) {
                UNITY_SETUP_INSTANCE_ID(v);
                fragment f;
                UNITY_TRANSFER_INSTANCE_ID(v, f);
                f.loc = UnityObjectToClipPos(v.loc);
                f.buv = (v.uv - float2(0.5,0.5));
                
                f.uv = UVR.xy + f.buv * 
                    float2(_FragDiameter/_TexWidth, _FragDiameter/_TexHeight);
        #ifdef FT_MOD
                //This mitigates a few issues with shattering backgrounds that are not particularly wide
                f.uv = mod2(f.uv, float2(1,1));
        #endif
                //f.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return f;
            }
            
            float polyRad(float theta, float f) {
                return cos(f) / cos(fmod(theta, 2*f) - f);
            }
            float atan02pi(float2 v) {
                return fmod(atan2(v.y, v.x) + TAU, TAU);
            }
            
            float4 frag(fragment f) : SV_Target{
                UNITY_SETUP_INSTANCE_ID(f);
                float r = length(f.buv) * 2;
                clip(polyRad(atan02pi(f.buv) + UVR.z, PI/_FragSides) - r);
                clip(0.5 - abs(f.uv.x - 0.5));
                clip(0.5 - abs(f.uv.y - 0.5));
                float4 c = tex2D(_MainTex, f.uv);
                return c;
            }
            ENDCG
        }
    }
}