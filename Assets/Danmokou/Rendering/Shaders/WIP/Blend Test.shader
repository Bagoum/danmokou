Shader "_Test/BlendTest" {
	Properties {
		[PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
		[Enum(One,1,OneMinusSrcAlpha,10)] _BlendTo("Blend mode", Float) = 1

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
		Blend OneMinusSrcColor OneMinusSrcAlpha, OneMinusDstAlpha One

		GrabPass { "_PreUITex" }

		Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			struct vertex {
				float4 loc  : POSITION;
				float2 uv	: TEXCOORD0;
				float4 color: COLOR;
			};

			struct fragment {
				float4 loc  : SV_POSITION;
				float2 uv	: TEXCOORD0;
				float4 color: COLOR;
				float4 suv  : TEXCOORD1;
			};


			fragment vert(vertex v) {
				fragment f;
				f.loc = UnityObjectToClipPos(v.loc);
				f.suv = f.loc;
				f.uv = v.uv;
				f.color = v.color;
				return f;
			}

			fixed G(fixed4 c) { return .299 * c.r + .587 * c.g + .114 * c.b; }

			//Like Multiply, but brighter.
			fixed4 Darken(fixed4 a, fixed4 b) {
				fixed4 r = min(a, b);
				r.a = b.a;
				return r;
			}

			//Like LinearBurn. 
			fixed4 Multiply(fixed4 a, fixed4 b) {
				fixed4 r = a * b;
				r.a = b.a;
				return r;
			}

			//Don't use
			fixed4 ColorBurn(fixed4 a, fixed4 b) {
				fixed4 r = 1.0 - (1.0 - a) / b;
				r.a = b.a;
				return r;
			}

			//One: A slightly different original, but has problems with overlapping
			//OMSA: Deletes whites, darkens.
			fixed4 LinearBurn(fixed4 a, fixed4 b) {
				fixed4 r = a + b - 1.0;
				r.a = b.a;
				return r;
			}

			//Don't use, creates artifacts
			fixed4 DarkerColor(fixed4 a, fixed4 b) {
				fixed4 r = G(a) < G(b) ? a : b;
				r.a = b.a;
				return r;
			}

			//Same as Screen, but no brightness on OMSA.
			fixed4 Lighten(fixed4 a, fixed4 b) {
				fixed4 r = max(a, b);
				r.a = b.a;
				return r;
			}

			//Same as LinearDodge, but less bright on OMSA.
			fixed4 Screen(fixed4 a, fixed4 b) {
				fixed4 r = 1.0 - (1.0 - a) * (1.0 - b);
				r.a = b.a;
				return r;
			}

			//Basically identical to lineardodge
			fixed4 ColorDodge(fixed4 a, fixed4 b) {
				fixed4 r = a / (1.0 - b);
				r.a = b.a;
				return r;
			}

			//One: Glow with some colorization
			//OMSA: softer original
			fixed4 LinearDodge(fixed4 a, fixed4 b) {
				fixed4 r = a + b;
				r.a = b.a;
				return r;
			}

			//Don't use, creates artifacts
			fixed4 LighterColor(fixed4 a, fixed4 b) {
				fixed4 r = G(a) > G(b) ? a : b;
				r.a = b.a;
				return r;
			}

			//Same as SoftLight, but more saturated.
			fixed4 Overlay(fixed4 a, fixed4 b) {
				fixed4 r = a > .5 ? 1.0 - 2.0 * (1.0 - a) * (1.0 - b) : 2.0 * a * b;
				r.a = b.a;
				return r;
			}

			//One: Glow with faint colorization
			//OMSA: half-transparent original???
			fixed4 SoftLight(fixed4 a, fixed4 b) {
				fixed4 r = (1.0 - a) * a * b + a * (1.0 - (1.0 - a) * (1.0 - b));
				r.a = b.a;
				return r;
			}

			//Same as LinearLight, but slightly less bright
			fixed4 HardLight(fixed4 a, fixed4 b) {
				fixed4 r = b > .5 ? 1.0 - (1.0 - a) * (1.0 - 2.0 * (b - .5)) : a * (2.0 * b);
				r.a = b.a;
				return r;
			}

			//Nonfunctional
			fixed4 VividLight(fixed4 a, fixed4 b) {
				fixed4 r = b > .5 ? a / (1.0 - (b - .5) * 2.0) : 1.0 - (1.0 - a) / (b * 2.0);
				r.a = b.a;
				return r;
			}

			//Same as PinLight, with less severe dark point exclusion
			fixed4 LinearLight(fixed4 a, fixed4 b) {
				fixed4 r = b > .5 ? a + 2.0 * (b - .5) : a + 2.0 * b - 1.0;
				r.a = b.a;
				return r;
			}

			//One: Slightly faintly colored glow, with missing dark points
			//OMSA: About normal, with dark points missing
			fixed4 PinLight(fixed4 a, fixed4 b) {
				fixed4 r = b > .5 ? max(a, 2.0 * (b - .5)) : min(a, 2.0 * b);
				r.a = b.a;
				return r;
			}

			//Don't use-- creates edge
			fixed4 HardMix(fixed4 a, fixed4 b) {
				fixed4 r = (b > 1.0 - a) ? 1.0 : .0;
				r.a = b.a;
				return r;
			}

			//Same as exclusion, but more sensitive to darkness in the second sprite.
			fixed4 Difference(fixed4 a, fixed4 b) {
				fixed4 r = abs(a - b);
				r.a = b.a;
				return r;
			}

			//One: Faintly colored glows
			//OMSA: Extremely faint color
			fixed4 Exclusion(fixed4 a, fixed4 b) {
				fixed4 r = a + b - 2.0 * a * b;
				r.a = b.a;
				return r;
			}

			fixed4 Subtract(fixed4 a, fixed4 b) {
				fixed4 r = a - b;
				r.a = b.a;
				return r;
			}

			//Bad
			fixed4 Divide(fixed4 a, fixed4 b) { 
				fixed4 r = a / b;
				r.a = b.a;
				return r;
			}

			sampler2D _MainTex;
			sampler2D _PreUITex;

			float4 frag(fragment f) : SV_Target { 
				float4 c = tex2D(_MainTex, f.uv) * f.color;

				float2 bg = f.suv.xy / f.suv.w;
				bg.x = (bg.x + 1.0) * .5;
				bg.y = (bg.y + 1.0) * .5;
#if UNITY_UV_STARTS_AT_TOP
				bg.y = 1.0 - bg.y;
#endif

				fixed4 bgc = tex2D(_PreUITex, bg);
				
				return c;
			}
			ENDCG
		}
	}
}