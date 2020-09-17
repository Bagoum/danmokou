#include "Noise.cginc"

float2 lightningDistort( float2 uv, float bx, float by, float t, float nm) {
    float3 suvt = float3(s(uv, bx, by), t);
    float noise = perlin3Dlayer(suvt);
    //cos(hpi*x) for pointed head and spread tails. sin(pi*x) for pointed ends
    float dy = uv.y - 0.5;
    uv.y += noise * cos(HPI * uv.x) * nm * cos(PI * dy);
    return uv;
}
float2 lightningDistort2(float2 uv, float2 distorter, float t, float nm) {
    float noise = perlin3Dlayer(float3(distorter, t));
    //cos(hpi*x) for pointed head and spread tails. (1- for trail renderer reversal) sin(pi*x) for pointed ends
    float dy = uv.y - 0.5;
    uv.y += noise * (0.5 - 0.5 * cos(PI * uv.x)) * nm * cos(PI * dy);
    return uv;
}