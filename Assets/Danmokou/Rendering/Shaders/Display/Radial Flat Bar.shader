﻿Shader "_Misc/RadialFlatBar" {
	Properties{
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
		_R("Radius", Float) = 0.5
		_Subradius("Subradius", Float) = 0.1
		_F("Ratio", Range(0,1)) = 0.6
		_PMDir("Direction", Float) = 1
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
            float _F;
            float _PMDir;
            
            float _RPPU;
            static const float rsmth = 0.004f;

            float4 frag(fragment f) : SV_Target {
                float r = length(f.uv) * _MainTex_TexelSize.z / _RPPU;
                float4 c = tex2D(_MainTex, f.uv).a * f.color;
                float a = atan2(f.uv.y, f.uv.x) / TAU; 
                float ang = fmod(a + 1.75, 1); // 0 to 1, starting at 90
                ang = 1 - 2 * abs(0.5 - ang); // 90 = 0; -90 = 1; 0,180 = 0.5
                ang = 0.5 + _PMDir * (ang - 0.5);
                c *= smoothstep(-0.001, 0.00, _F - ang);
                c.a *=  1-smoothstep(_Subradius-rsmth, _Subradius+rsmth, abs(r - _R));
                return c;
            }
            ENDCG
        }
	}
}