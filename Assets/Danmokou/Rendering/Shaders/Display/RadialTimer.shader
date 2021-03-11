Shader "_Misc/RadialTimer" {
	Properties{
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
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
                f.uv = v.uv;
                f.color = v.color;
                return f;
            }

            sampler2D _MainTex;
            float _F;
            float _PMDir;

            float4 frag(fragment f) : SV_Target {
                float4 c = tex2D(_MainTex, f.uv).a * f.color;
                float a = uvToPolar(f.uv).y / TAU;
                float ang = fmod(a + 1.75, 1); // 0 to 1, starting at 90
                ang = 1 - 2 * abs(0.5 - ang); // 90 = 0; -90 = 1; 0,180 = 0.5
                ang = 0.5 + _PMDir * (ang - 0.5);
                c *= smoothstep(-0.001, 0.001, _F - ang);
                return c;
            }
            ENDCG
        }
	}
}