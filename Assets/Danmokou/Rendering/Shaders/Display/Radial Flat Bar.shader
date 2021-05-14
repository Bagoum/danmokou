﻿Shader "_Misc/RadialFlatBar" {
	Properties{
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
		_R("Radius", Float) = 0.5
		_Subradius("Subradius", Float) = 0.1
		_Subradius2("Subradius2", Float) = 0.1
		_OutwardsPush("Outwards Push", Float) = 0.01
		_F("Ratio", Range(0,1)) = 0.6
		_PMDir("Direction", Float) = 1
		_CE("Shadow Color", Color) = (0, 0, 0, 1)
		_CF("Fill Color", Color) = (0, 1, 0, 1)
		_CFI("Inner Fill Color", Color) = (1, 0, 0, 1)
		_FI("Fixed Inner Fill Ratio", Float) = 0.4
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
        //Blend OneMinusDstColor OneMinusSrcAlpha

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Assets/Danmokou/CG/Math.cginc"

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
                f.uv = float2(v.uv.x - 0.5, v.uv.y - 0.5);
                f.color = v.color;
                return f;
            }

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _R;
            float _Subradius;
            float _Subradius2;
            float _F;
            float _FI;
            float _PMDir;
            float _OutwardsPush;
            
            float _RPPU;
            float4 _CE;
            float4 _CF;
            float4 _CFI;
            static const float rsmth = 0.004f;

            float4 frag(fragment f) : SV_Target {
                float r = length(f.uv) * _MainTex_TexelSize.z / _RPPU;
                float a = atan2(f.uv.y, f.uv.x) / TAU; 
                float ang = fmod(a + 1.75, 1); // 0 to 1, starting at 90
                ang = 1 - 2 * abs(0.5 - ang); // 90 = 0; -90 = 1; 0,180 = 0.5
                ang = 0.5 + _PMDir * (ang - 0.5);
                float4 c = f.color * _CF;
                c = lerp(_CFI, c, smoothstep(0, 0.002, ang - _FI));
                c = lerp(c, _CE, 
                    smoothstep(-rsmth, rsmth, abs(r + _OutwardsPush - _R) - _Subradius));
                float shadowFill = lerp(_F, 1, _FI);
                c = lerp(c, _CE, smoothstep(0, 0.002, ang - _F));
                c.a *= tex2D(_MainTex, f.uv).a;
                c.a *= 1-smoothstep(_Subradius2-rsmth, _Subradius2+rsmth, abs(r - _R));
                return c;
            }
            ENDCG
        }
	}
}