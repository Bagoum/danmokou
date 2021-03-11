Shader "_Misc/PowerAura" {
	Properties{
		[PerRendererData] _MainTex("Texture", 2D) = "white" {}
		[Enum(One,1,OneMinusSrcAlpha,10)] _BlendTo("Blend mode", Float) = 10
		_T("Time", Range(0,30)) = 0
		_Speed("Speed", float) = 1
		_Spokes("Spokes", Float) = 4
		_DisplaceTex("Displace Tex", 2D) = "white" {}
		[PerRendererData] _DisplaceMask("Displace Mask", 2D) = "white" {}
		_DisplaceMagnitude("Displace Magnitude", float) = 1
		_DisplaceSpeed("Displace Speed", float) = 1
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
        Blend SrcAlpha [_BlendTo], OneMinusDstAlpha One

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Assets/Danmokou/CG/Math.cginc"
			#pragma multi_compile __ FANCY

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
            
            fragment vert(vertex v) {
                fragment f;
                f.loc = UnityObjectToClipPos(v.loc);
                f.uv = v.uv;
                f.color = v.color;
                return f;
            }

            sampler2D _MainTex;
            sampler2D _DisplaceTex;
            float _DisplaceMagnitude;
            float _DisplaceSpeed;
            float _T;
            float _Speed;
            float _Spokes;
            
            float2 getDisplace(float2 rt, float t) {
                rt.x += t * _DisplaceSpeed;
                float2 disp = tex2D(_DisplaceTex, rt).xy;
                disp = ((disp * 2) - 1) * _DisplaceMagnitude;
                return disp;
            }

            float4 frag(fragment f) : SV_Target {
                float2 rt = uvToPolar(f.uv);
                rt.y *= _Spokes / TAU;
                clip(0.5 - rt.x); //Render in a circle
                float4 c = f.color;
                c.a *= 1 - smoothstep(0.45, 0.5, rt.x);
                rt.x += _T * _Speed;
                return c * tex2D(_MainTex, rt + getDisplace(rt, _T));
            }
            ENDCG
        }
	}
}