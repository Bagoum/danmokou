#include "Math.cginc"

float valuenoise(float2 suv) {
    float2 uv0 = floor(suv);
    float2 buv = suv - uv0;
    float2 uv1 = uv0 + float2(1, 0);
    float2 uv2 = uv0 + float2(0, 1);
    float2 uv3 = uv0 + float2(1, 1);
    buv = cq2(buv);
    return lerp(
        lerp(hash21(uv0), hash21(uv1), buv.x),
        lerp(hash21(uv2), hash21(uv3), buv.x), buv.y);
}

static float2 perlinGradients[8] = {
    float2(1, 0),
    float2(0, 1),
    float2(-1, 0),
    float2(0, -1),
    normalize(float2(1, 1)),
    normalize(float2(1,-1)),
    normalize(float2(-1,1)),
    normalize(float2(-1,-1))
};
float2 perlinGradient(float zeroOne) {
    return perlinGradients[floor(zeroOne * 8)];
}

float perlin(float2 suv) {
    float2 uv0 = floor(suv);
    float2 t0 = suv - uv0;
    
    float2 g00 = perlinGradient(hash21(uv0));
    float2 g10 = perlinGradient(hash21(uv0 + float2(1, 0)));
    float2 g01 = perlinGradient(hash21(uv0 + float2(0, 1)));
    float2 g11 = perlinGradient(hash21(uv0 + float2(1, 1)));
    
    float v00 = dot(g00, t0);
    float v10 = dot(g10, t0 - float2(1, 0));
    float v01 = dot(g01, t0 - float2(0, 1));
    float v11 = dot(g11, t0 - float2(1, 1));
    
    t0 = cq2(t0);
    return lerp(
        lerp(v00, v10, t0.x),
        lerp(v01, v11, t0.x), t0.y) * SQR2;
}

float perlin01(float2 suv) {
    return pm01(perlin(suv));
}

static float3 perlinGradients3D[12] = {
    float3(1,1,0),
    float3(-1,1,0),
    float3(1,-1,0),
    float3(-1,-1,0),
    
    float3(1,0,1),
    float3(-1,0,1),
    float3(1,0,-1),
    float3(-1,0,-1),
    
    float3(0,1,1),
    float3(0,-1,1),
    float3(0,1,-1),
    float3(0,-1,-1),
    
    //float3(1,1,0),
    //float3(-1,1,0),
    //float3(0,-1,1),
    //float3(0,-1,-1)
};
float3 perlinGradient3D(float zeroOne) {
    return perlinGradients3D[floor(zeroOne * 12)];
}
float perlin3D(float3 suv) {
    float3 uv0 = floor(suv);
    float3 t0 = suv - uv0;
    
    float3 g000 = perlinGradient3D(hash31(uv0));
    float3 g100 = perlinGradient3D(hash31(uv0 + float3(1, 0, 0)));
    float3 g010 = perlinGradient3D(hash31(uv0 + float3(0, 1, 0)));
    float3 g110 = perlinGradient3D(hash31(uv0 + float3(1, 1, 0)));
    float3 g001 = perlinGradient3D(hash31(uv0 + float3(0, 0, 1)));
    float3 g101 = perlinGradient3D(hash31(uv0 + float3(1, 0, 1)));
    float3 g011 = perlinGradient3D(hash31(uv0 + float3(0, 1, 1)));
    float3 g111 = perlinGradient3D(hash31(uv0 + float3(1, 1, 1)));
    
    float v000 = dot(g000, t0);
    float v100 = dot(g100, t0 - float3(1, 0, 0));
    float v010 = dot(g010, t0 - float3(0, 1, 0));
    float v110 = dot(g110, t0 - float3(1, 1, 0));
    float v001 = dot(g001, t0 - float3(0, 0, 1));
    float v101 = dot(g101, t0 - float3(1, 0, 1));
    float v011 = dot(g011, t0 - float3(0, 1, 1));
    float v111 = dot(g111, t0 - float3(1, 1, 1));
    
    t0 = float3(cq(t0.x), cq(t0.y), cq(t0.z));
    return lerp(
            lerp(lerp(v000, v100, t0.x),
                lerp(v010, v110, t0.x), t0.y),
            lerp(lerp(v001, v101, t0.x),
                lerp(v011, v111, t0.x), t0.y), t0.z) * _2ISQR3;
}
float perlin3Dm(float3 suv, float3 m) {
    float3 uv0 = mod3(floor(suv), m);
    //Note: mod3(uv0 + float3(1,1,1)) creates a float rounding error
    float3 uv1 = mod3(floor(suv) + float3(1,1,1), m);
    float3 t0 = suv - floor(suv);
    
    float3 g000 = perlinGradient3D(hash31(uv0));
    float3 g100 = perlinGradient3D(hashv31(uv1.x, uv0.y, uv0.z));
    float3 g010 = perlinGradient3D(hashv31(uv0.x, uv1.y, uv0.z));
    float3 g110 = perlinGradient3D(hashv31(uv1.x, uv1.y, uv0.z));
    float3 g001 = perlinGradient3D(hashv31(uv0.x, uv0.y, uv1.z));
    float3 g101 = perlinGradient3D(hashv31(uv1.x, uv0.y, uv1.z));
    float3 g011 = perlinGradient3D(hashv31(uv0.x, uv1.y, uv1.z));
    float3 g111 = perlinGradient3D(hash31(uv1));
    
    float v000 = dot(g000, t0);
    float v100 = dot(g100, t0 - float3(1, 0, 0));
    float v010 = dot(g010, t0 - float3(0, 1, 0));
    float v110 = dot(g110, t0 - float3(1, 1, 0));
    float v001 = dot(g001, t0 - float3(0, 0, 1));
    float v101 = dot(g101, t0 - float3(1, 0, 1));
    float v011 = dot(g011, t0 - float3(0, 1, 1));
    float v111 = dot(g111, t0 - float3(1, 1, 1));
    
    t0 = float3(cq(t0.x), cq(t0.y), cq(t0.z));
    return lerp(
            lerp(lerp(v000, v100, t0.x),
                lerp(v010, v110, t0.x), t0.y),
            lerp(lerp(v001, v101, t0.x),
                lerp(v011, v111, t0.x), t0.y), t0.z) * _2ISQR3;
}

float perlin3D01(float3 suvt) {
    return pm01(perlin3D(suvt));
}
float perlin3Dlayer(float3 suvt) {
    return perlin3D(suvt) + 0.5 * perlin3D(suvt * 2) + 0.25 * perlin3D(suvt * 4);
}
float perlin3Dlayer01(float3 suvt) {
    return pm01(perlin3Dlayer(suvt));
}
float perlin3Dmlayer(float3 suvt, float3 m) {
    return perlin3Dm(suvt, m) + 0.5 * perlin3Dm(suvt * 2, m * 2) + 0.25 * perlin3Dm(suvt * 4, m * 4);
}
float perlin3Dmlayer01(float3 suvt, float3 m) {
    return pm01(perlin3Dmlayer(suvt, m));
}
//Value (randomly placed circles with 0 in a field of 1), 
//Random hash corresponding to closest node,
//Distance to border
float3 voronoi3D(float3 suv) {
    float3 uv0 = floor(suv);
    float minDist = 20;
    float3 minCell;
    float3 minVec;
    [unroll]
    for (int x = -1; x <= 1; ++x) {
        [unroll]
        for (int y = -1; y <= 1; ++y) {
            [unroll]
            for (int z = -1; z <= 1; ++z) {
                float3 cell = uv0 + float3(x,y,z);
                float3 toCellLoc = cell + hash33(cell) - suv;
                float distToCellLoc = length(toCellLoc);
                if (distToCellLoc < minDist) {
                    minDist = distToCellLoc;
                    minCell = cell;
                    minVec = toCellLoc;
                }
            }
        }
    }
    float minEdgeDist = 20;
    [unroll]
    for (int x2 = -1; x2 <= 1; ++x2) {
        [unroll]
        for (int y2 = -1; y2 <= 1; ++y2) {
            [unroll]
            for (int z2 = -1; z2 <= 1; ++z2) {
                float3 cell = uv0 + float3(x2,y2,z2);
                float3 toCellLoc = cell + hash33(cell) - suv;
                float distToCellLoc = length(toCellLoc);
                if (!approx(toCellLoc, minVec)) {
                    float3 toMidp = 0.5 * (toCellLoc + minVec);
                    float3 minCellLocToMidp = normalize(toCellLoc - minVec);
                    minEdgeDist = min(minEdgeDist, dot(toMidp, minCellLocToMidp));
                }
            }
        }
    }
    return float3(minDist, hash21(minCell), minEdgeDist);
}