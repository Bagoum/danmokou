#include "Noise.cginc"


float lightningDistortNoiseMult(float2 uv, float noise) {
    return noise
        * (0.5 - 0.5 * cos(PI * uv.x)) //0 noise at x=0 (head); 1 noise at x=1 (tail)
        * cos(PI * (uv.y - 0.5)); //minimize noise for uvs close to the vertical border
}

float2 lightningDistort(float2 uv, float2 distorter, float t, float nm) {
    uv.y += lightningDistortNoiseMult(uv, nm * perlin3Dlayer(float3(distorter, t)));
    return uv;
}
