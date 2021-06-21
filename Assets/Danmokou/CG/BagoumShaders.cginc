#include "Assets/Danmokou/CG/Math.cginc"

sampler2D _MainTex;
float4 _MainTex_TexelSize;

float _CycleSpeed;
#ifdef FT_CYCLE
    #define CYCLE(uv, t) uv.x += _CycleSpeed * t
#else
    #define CYCLE(uv, t) { }
#endif

float _FadeInT;
#ifdef FT_FADE_IN
    #define FADEIN(c, t) c.a *= smoothstep(0.0, _FadeInT, t)
#else
    #define FADEIN(c, t) { }
#endif

// uv.x += 1 - smoothstep(0, 1, t / _SlideInT)
float _SlideInT;
#ifdef FT_SLIDE_IN
    #define SLIDEIN(uv, t) clip(uv.x - 1 + clamp(t / _SlideInT, 0, 1));
#else
    #define SLIDEIN(uv, t) { }
#endif

float _ScaleInMin;
float _ScaleInT;
#ifdef FT_SCALE_IN
    #define SCALEIN(dir, t) dir *= _ScaleInMin + (1 - _ScaleInMin) * smoothstep(0.0, _ScaleInT, t);
    //#define SCALEIN(dir, t) dir *= smoothstep(0.0, _ScaleInT, t);
#else
    #define SCALEIN(dir, t) { } 
#endif


sampler2D _DisplaceTex;
sampler2D _DisplaceMask;
float _DisplaceMagnitude;
float _DisplaceSpeed;
float _DisplaceXMul;
float2 getDisplace(float2 uv, float t) {
    t *= _DisplaceSpeed;
    float2 disp;
#ifdef FT_DISPLACE_RADIAL
    // Values from last test with gdcircle: mag 0.12 spd 7 xmul 6
    float2 puv = uvToPolar(uv);
    disp = (uv -center) * sin(2 * _DisplaceXMul * puv.y) * sin(t + sin(_DisplaceXMul * puv.y) + puv.y);
    return disp * _DisplaceMagnitude * tex2D(_DisplaceMask, uv).r;
#endif
    
#ifdef FT_DISPLACE_POLAR
    float mask = tex2D(_DisplaceMask, uv).r;
    uv = uvToPolar(uv);
    //Note: this *4/TAU basically maps the y into range [-2,2].
    //Make sure _DisplaceXMul * 4 is a whole number.
    uv.y *= 4 / TAU * _DisplaceXMul;
#else
    uv.x *= _DisplaceXMul;
    float mask = tex2D(_DisplaceMask, uv).r;
#endif
#ifdef FT_DISPLACE_BIVERT
    uv.xy = float2(0.5-abs(uv.y - 0.5), uv.x);
#endif
    uv.x += t;
    //uv = polarToUV(uv);
    disp = tex2D(_DisplaceTex, uv).xy;
    disp = ((disp * 2) - 1) * _DisplaceMagnitude * mask;
    return disp;
}
#if FT_DISPLACE || FT_DISPLACE_POLAR || FT_DISPLACE_RADIAL || FT_DISPLACE_BIVERT
    #define DISPLACE(uv, t) uv += getDisplace(uv, t)
#else
    #define DISPLACE(uv, t) { }
#endif
