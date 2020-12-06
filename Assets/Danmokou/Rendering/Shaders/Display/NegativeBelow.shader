Shader "_Misc/NegativeBelow" {
	Properties{
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
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
        Blend OneMinusDstColor OneMinusSrcAlpha, SrcAlpha OneMinusSrcAlpha

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

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

            sampler2D _MainTex;

            float4 frag(fragment f) : SV_Target {
                float4 c = tex2D(_MainTex, f.uv).a * float4(1,1,1,1);
                return c;
            }
            ENDCG
        }
	}
}