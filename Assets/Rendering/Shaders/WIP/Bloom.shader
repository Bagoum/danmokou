Shader "_Misc/Bloom" {
	Properties {
		[PerRendererData] _MainTex("Main Texture", 2D) = "white" {}
	}
	
	CGINCLUDE
	
    #include "UnityCG.cginc"

    struct vertex {
        float4 loc  : POSITION;
        float2 uv	: TEXCOORD0;
    };

    struct fragment {
        float4 loc  : SV_POSITION;
        float2 uv	: TEXCOORD0;
    };
    

	sampler2D _MainTex;
	float4 _MainTex_TexelSize;
	float _BloomThreshold;
	float3 Prefilter(float3 c) {
	    float brightness = max(c.r, max(c.g, c.b));
	    float multiplier = max(0, brightness - _BloomThreshold);
        return c * multiplier / max(brightness, 0.01);
	}
	
    float3 Sample(float2 uv) {
        return tex2D(_MainTex, uv).rgb;
    }
    float3 SampleBox(float2 uv, float d) {
        float4 o = _MainTex_TexelSize.xyxy * float2(-d, d).xxyy;
        float3 s =
            Sample(uv + o.xy) + Sample(uv + o.zy) +
            Sample(uv + o.xw) + Sample(uv + o.zw);
        return s * 0.25f;
    }

    fragment vert(vertex v) {
        fragment f;
        f.loc = UnityObjectToClipPos(v.loc);
        f.uv = v.uv;
        return f;
    }
	
	ENDCG
	
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

		Pass { // Filter and downsample, Pass 0
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float4 frag(fragment f) : SV_Target { 
			    return float4(Prefilter(SampleBox(f.uv, 1)), 1);
				//return tex2D(_MainTex, f.uv) * float4(1,0,0,0);
			}
			ENDCG
		}
		Pass { // Downsample, Pass 1
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float4 frag(fragment f) : SV_Target { 
			    return float4(SampleBox(f.uv, 1), 1);
				//return tex2D(_MainTex, f.uv) * float4(1,0,0,0);
			}
			ENDCG
		}
		Pass { // Upsample, Pass 2
		    Blend One One
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float4 frag(fragment f) : SV_Target { 
			    return float4(SampleBox(f.uv, 0.5), 1);
				//return tex2D(_MainTex, f.uv) * float4(1,0,0,0);
			}
			ENDCG
		}
		
		Pass { // Upsample and apply, Pass 3
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			sampler2D _SourceTex;
			
			float3 remap(float3 rgb) {
			    float maxv = max(1, max(rgb.r, max(rgb.g, rgb.b)));
			    return rgb / maxv;
			    //return float3(maxv/2, 0, 0);
			    //return rgb / float3(maxv, maxv, maxv);
			}

			float4 frag(fragment f) : SV_Target { 
				float4 c = tex2D(_SourceTex, f.uv) + float4(SampleBox(f.uv, 0.5), 1);
				c.rgb = remap(c.rgb);
				return c;
			}
			ENDCG
		}
		
		Pass { // Debug final pass, 4
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float4 frag(fragment f) : SV_Target { 
				return float4(SampleBox(f.uv, 0.5), 1);
			}
			ENDCG
		}
		
		Pass { // Remap only, 5
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			sampler2D _SourceTex;
			
			float3 remap(float3 rgb) {
			    float maxv = max(1, max(rgb.r, max(rgb.g, rgb.b)));
			    return rgb / maxv;
			}

			float4 frag(fragment f) : SV_Target { 
				float4 c = tex2D(_SourceTex, f.uv);
				c.rgb = remap(c.rgb);
				return c;
			}
			ENDCG
		}
	}
}