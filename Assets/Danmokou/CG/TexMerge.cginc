#include "Assets/Danmokou/CG/Noise.cginc"

sampler2D _FaderTex; //texture used for transition
float _A0;
float _T;
float _MaxT;
float _PMDir;

static float smooth = 0.005;
            
float ssd(float ref, float x) {
    return smoothstep(-smooth, smooth, x - ref);
}

float fill(float2 uv) {
#if MIX_ALPHA_BLEND
    return smoothstep(0.0, _MaxT, _T);
#elif MIX_WIPE_TEX
    float grad = tex2D(_FaderTex, uv);
    grad = 0.5 + _PMDir * (0.5 - grad);
    return ssd(grad, _T / _MaxT);
#elif MIX_WIPE1
    float ratio = _T / _MaxT * TAU;
    return ssd(mod((uvToPolar2(uv).y - _A0) * _PMDir, TAU), ratio);
#elif MIX_WIPE_CENTER
    return ssd(uvToPolar2(uv).x, _T / _MaxT);
#elif MIX_WIPE_Y
    return ssd(.5 + _PMDir * (uv.y - 0.5), _T/ _MaxT);
#else
    return 1;
#endif
}

#ifdef MIX_FROM_ONLY
    #define MERGE(from, to, uv) tex2D(from, uv)
#elif MIX_TO_ONLY
    #define MERGE(from, to, uv) tex2D(to, uv)
#else
    #define MERGE(from, to, uv) lerp(tex2D(from, uv), tex2D(to, uv), fill(uv))
#endif
