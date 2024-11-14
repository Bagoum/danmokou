using BagoumLib.Mathematics;
using UnityEngine;

namespace Danmokou.DMath {

/// <summary>
/// A function that converts a float into a float.
/// </summary>
public delegate float FXY(float t);


public static class MathHelpers {
    public static V2RV2 RotateAll(this V2RV2 v, float by_deg) {
        var newnxy = M.RotateVectorDeg(v.nx, v.ny, by_deg);
        return new V2RV2(newnxy.x, newnxy.y, v.rx, v.ry, v.angle + by_deg);
    }

    public static V2RV2 AddToNXY(this V2RV2 v, Vector2 nxy) {
        return new V2RV2(v.nx + nxy.x, v.ny + nxy.y, v.rx, v.ry, v.angle);
    }
    
    public static V2RV2 AddToRot(this V2RV2 v, Vector3 rxya) {
        return new V2RV2(v.nx, v.ny, v.rx + rxya.x, v.ry + rxya.y, v.angle + rxya.z);
    }
    
    public static Vector2 NV(this V2RV2 v) => new Vector2(v.nx, v.ny);
    public static Vector2 RV(this V2RV2 v) => new Vector2(v.rx, v.ry);
    
    public static Vector2 TrueLocation(this V2RV2 v) => new Vector2(v.nx, v.ny) + M.RotateVectorDeg(v.rx, v.ry, v.angle);

    public static V2RV2 V2RV2FromVectors(Vector2 nxy, Vector2 rxy, float angle) => new(nxy.x, nxy.y, rxy.x, rxy.y, angle);
}

}