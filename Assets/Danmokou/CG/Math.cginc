#include "Hash.cginc"

static float PI = 3.14159265358979323846264338327950288419716939937510;
static float TAU = 2 * PI;
static float HPI = PI / 2;
static float SQR2 = 1.41421356237;
static float ISQR2 = 0.70710678119;
static float _2ISQR3 = 1.154700538379251529018297561;
static float PHI = 1.6180339887498948482045868343656381177203091798057628621354486227;
static float IPHI = 1.6180339887498948482045868343656381177203091798057628621354486227 - 1;
static float DEGRAD = 0.0174532925199432957692369076848861271344287188854172545609719144;
static float RADDEG = 57.295779513082320876798154814105170332405472466564321549160243861;
static float2 center = float2(0.5, 0.5);

static float REHASH = 43758.5453123;

float intpow(float x, float p) {
    float acc = 1;
    while (p > 0) {
        if (p % 2 == 1) {
            acc *= x;
        }
        p = floor(p/2);
        x = x * x;
    }
    return acc;
        
}

float mod(float x, float by) {
    return x - by * floor(x/by);
}
float mod1(float x, float by) {
    return x - by*floor(x/by);
}
float2 mod2(float2 x, float2 by) {
    return x - by*floor(x/by);
}
float3 mod3(float3 x, float3 by) {
    return x - by*floor(x/by);
}
float softmod(float x, float by) {
    float vd = mod(x, 2 * by);
    if (vd > by)
        return 2 * by - vd;
    else
        return vd;
}

float rehash(float x, int ii) {
    return x + REHASH * ii;
}

/** A function that modifies a 0-1 lerp controller so using it in `lerp` produces smooth ends.
 */
float cq(float w) {
    //return w * w * w * (10.0 + w * (-15.0 + 6.0 * w));
    return w * w * (3.0 - 2.0 * w);
}	
float2 cq2(float2 xy) {
    return float2(cq(xy.x), cq(xy.y));
}
float3 cq3(float3 xyz) {
    return float3(cq(xyz.x), cq(xyz.y), cq(xyz.z));
}
float c01(float x) {
    return clamp(x, 0, 1);
}
			
float2 s(float2 uv, float bx, float by) {
    return float2(uv.x * bx, uv.y * by);
}
float2 ds(float2 uv, float bx, float by) {
    return float2(uv.x / bx, uv.y / by);
}
bool approx(float3 x, float3 y) {
    x = abs(x - y);
    return x.x + x.y + x.z < 0.01;
}
float z1pm(float z1) {
    return 2 * z1 - 1;
}
float4 z1pm4(float4 z1) {
    return 2 * z1 - 1;
}
float pm01(float pm) {
    return 0.5 + pm / 2;
}
float pm01c(float pm) {
    return clamp(0.5 + pm / 2, 0, 1);
}
float2 rotv2(float2 rot, float2 vec) {
    return float2(rot.x * vec.x - rot.y * vec.y, rot.y * vec.x + rot.x * vec.y);
}
float2 rot2(float angle, float2 vec) {
    return rotv2(float2(cos(angle), sin(angle)), vec);
}
float2 rot2c(float angle, float2 vec) {
    return center + rot2(angle, vec - center);
}
float2 rectToPolar(float2 rect) {
    return float2(length(rect), atan2(rect.y, rect.x));
}
float2 polarToRect(float2 rt) {
    return float2(rt.x * cos(rt.y), rt.x * sin(rt.y));
}
float2 polarToRect2(float r, float t) {
    return float2(r * cos(t), r * sin(t));
}
//-pi to pi
float2 uvToPolar(float2 uv) {
    uv -= float2(0.5, 0.5);
    return float2(length(uv), atan2(uv.y, uv.x));
}
//0 to 2pi
float2 uvToPolar2(float2 uv) {
    uv -= float2(0.5, 0.5);
    return float2(length(uv), mod1(atan2(uv.y, uv.x), TAU));
}
//theta in radians
float2 polarToUV(float2 rt) {
    return float2(0.5 + rt.x * cos(rt.y), 0.5 + rt.x * sin(rt.y));
}
float3 hueShift(float3 color, float hue) {
    const float3 k = float3(0.57735, 0.57735, 0.57735);
    float cosAngle = cos(hue);
    return float3(color * cosAngle + cross(k, color) * sin(hue) + k * dot(k, color) * (1.0 - cosAngle));
}

float einsine(float x){
    return 1 - cos(HPI * x);
}
float ratio(float a, float b, float x) {
    return clamp((x - a) / (b - a), 0, 1);
}

float pm1Sigmoid(float x, float pow) {
    return 1 - 2 / (exp(x * pow) + 1);
}
float pm1SigmoidBound(float x, float pow, float bound){
    return pm1Sigmoid(x / bound, pow) * bound;
}