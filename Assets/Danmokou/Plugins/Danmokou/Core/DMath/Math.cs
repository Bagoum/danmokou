using System;
using System.Runtime.CompilerServices;
using BagoumLib.Mathematics;
using UnityEngine;
using Danmokou.Core;
using Ex = System.Linq.Expressions.Expression;
using static BagoumLib.Mathematics.BMath;

namespace Danmokou.DMath {
public static class M {
    public const float MAG_ERR = 1e-10f;
    public const int IntFloatMax = int.MaxValue / 2;

    /// <summary>
    /// Returns the number with the smaller magnitude.
    /// </summary>
    public static float MinA(float a, float b) => Math.Abs(a) < Math.Abs(b) ? a : b;
    
    /// <summary>
    /// Returns the number with the larger magnitude.
    /// </summary>
    public static float MaxA(float a, float b) => Math.Abs(a) > Math.Abs(b) ? a : b;
    
    /// <summary>
    /// Return 1 if the value is even, and -1 if the value is odd.
    /// </summary>
    public static int PM1Mod(int x) => 1 - 2 * Mod(2, x);

    public static float HMod(float by, int x) {
        float y = Mod(by, x);
        return (y < by / 2) ? 
            y : 
            y - (float)Math.Floor(by/2f);
    }
    public static float HNMod(float by, int x) {
        float y = Mod(by, x);
        return (y < by / 2) ? 
            y + (float)Math.Floor(by/2f) + 0.5f - by/2f : 
            by/2f - 0.5f - y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte Float01ToByte(float f) =>
        f <= 0 ? byte.MinValue :
        f >= 1 ? byte.MaxValue :
        (byte) (f * 256f);

    /// <summary>
    /// Return the degrees from d1 to d2.
    /// <br/>Usually, this is just d2-d1, but in some cases it isn't:
    /// eg. DeltaD(179, -179) = 2.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DeltaD(float d1, float d2) {
        var d = d2 - d1;
        if (d > 180)
            return d - 360;
        if (d <= -180)
            return d + 360;
        return d;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Sin(float rad) => (float)Math.Sin(rad);

    public static float Sine(float period, float amp, float t) => amp * (float) Math.Sin(t * TAU / period);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Cos(float rad) => (float)Math.Cos(rad);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 CosSin(float rad) => new Vector2((float)Math.Cos(rad), (float)Math.Sin(rad));
    
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float SinDeg(float deg) => (float)Math.Sin(deg * degRad);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float CosDeg(float deg) => (float)Math.Cos(deg * degRad);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 CosSinDeg(float deg) => new((float)Math.Cos(deg * degRad), (float)Math.Sin(deg * degRad));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 PolarToXY(float deg) => CosSinDeg(deg);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 PolarToXY(float r, float deg) {
        return new Vector2(r * CosDeg(deg), r * SinDeg(deg));
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float AtanD(Vector2 v2) => radDeg * (float)Math.Atan2(v2.y, v2.x);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ToDeg(this Vector2 v2) => radDeg * (float)Math.Atan2(v2.y, v2.x);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Atan2D(float y, float x) => radDeg * (float)Math.Atan2(y, x);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Atan(Vector2 v2) => (float) Math.Atan2(v2.y, v2.x);

    /// <summary>
    /// Get a point on the unit sphere.
    /// </summary>
    /// <param name="theta">Angle in the XY plane (0 = X-axis)</param>
    /// <param name="phi">Divergence from the Z axis (pi/2 = XY-plane)</param>
    /// <returns></returns>
    public static Vector3 Spherical(float theta, float phi) {
        var sp = Math.Sin(phi);
        return new Vector3((float)(Math.Cos(theta)*sp), (float)(Math.Sin(theta)*sp), (float)Math.Cos(phi));
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 RotateVector(Vector2 init, float cos_rot, float sin_rot) {
        return new Vector2(cos_rot * init.x - sin_rot * init.y, sin_rot * init.x + cos_rot * init.y);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 RotateVector(Vector2 init, Vector2 cosSinRot) {
        return new Vector2(cosSinRot.x * init.x - cosSinRot.y * init.y, cosSinRot.y * init.x + cosSinRot.x * init.y);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 RotateVector(Vector2 init, float ang_rad) {
        float cos_rot = Mathf.Cos(ang_rad);
        float sin_rot = Mathf.Sin(ang_rad);
        return new Vector2(cos_rot * init.x - sin_rot * init.y, sin_rot * init.x + cos_rot * init.y);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 RotateVectorDeg(Vector2 init, float ang_deg) {
        float ang = ang_deg * degRad;
        float cos_rot = (float)Math.Cos(ang);
        float sin_rot = (float)Math.Sin(ang);
        return new Vector2(cos_rot * init.x - sin_rot * init.y, sin_rot * init.x + cos_rot * init.y);
    }

    public static Vector3 RotateXYDeg(Vector3 init, float ang_deg) {
        var xy = RotateVectorDeg(new Vector2(init.x, init.y), ang_deg);
        init.x = xy.x;
        init.y = xy.y;
        return init;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 RotateVectorDeg(float x, float y, float ang_deg) {
        float ang = ang_deg * degRad;
        float cos_rot = (float)Math.Cos(ang);
        float sin_rot = (float)Math.Sin(ang);
        return new Vector2(cos_rot * x - sin_rot * y, sin_rot * x + cos_rot * y);
    }
    public static Vector2 ConvertBasis(Vector2 source, Vector2 basis1) => RotateVector(source, basis1.x, -basis1.y);
    public static Vector2 DeconvertBasis(Vector2 source, Vector2 basis1) => RotateVector(source, basis1.x, basis1.y);

    /// <summary>
    /// Project the vector `v2` onto unit vector `ontoUnit`.
    /// </summary>
    public static Vector2 ProjectVectorUnit(Vector2 v2, Vector2 ontoUnit) {
        float dot = v2.x * ontoUnit.x + v2.y * ontoUnit.y;
        return dot * ontoUnit;
    }
    
    /// <summary>
    /// Project the vector `v2` onto vector `onto`.
    /// </summary>
    public static Vector2 ProjectVector(Vector2 v2, Vector2 onto) {
        var ontoSqrMag = (onto.x * onto.x + onto.y * onto.y);
        return (ontoSqrMag < M.MAG_ERR) ? Vector2.zero : ((v2.x * onto.x + v2.y * onto.y) / ontoSqrMag) * onto;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 WithZ(this Vector2 v2, float z) => new Vector3(v2.x, v2.y, z);

    /// <summary>
    /// Returns 1 if the input is positive, -1 if it is negative, and 0 if it is 0.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Sign(float f) {
        if (f > float.Epsilon)
            return 1;
        else if (f < -float.Epsilon)
            return -1;
        else
            return 0;
    }
    /// <summary>
    /// Returns 1 if the input is nonnegative, and -1 if it is negative.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Sign1(float f) {
        if (f < -float.Epsilon)
            return -1;
        else
            return 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short ClampS(short low, short high, short x) => 
        x < low ? low 
        : x > high ? high 
        : x;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Clamp(int low, int high, int x) => 
        x < low ? low 
        : x > high ? high 
        : x;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Clamp(double low, double high, double x) => 
        x < low ? low 
        : x > high ? high 
        : x;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Clamp(float low, float high, float x) => 
        x < low ? low 
        : x > high ? high 
        : x;

    public static float AngleFromTo(Vector2 src, Vector2 target) {
        Vector2 diff = target - src;
        return (float)Math.Atan2(diff.y, diff.x);
    }

    public static float AngleFromToDeg(Vector2 src, Vector2 target) => AngleFromTo(src, target) * radDeg;
    
    //https://stackoverflow.com/a/1968345
    public static bool SegmentIntersection(float p0_x, float p0_y, float p1_x, float p1_y, 
        float p2_x, float p2_y, float p3_x, float p3_y, out float i_x, out float i_y) {
        var s1_x = p1_x - p0_x;     var s1_y = p1_y - p0_y;
        var s2_x = p3_x - p2_x;     var s2_y = p3_y - p2_y;

        var det = (-s2_x * s1_y + s1_x * s2_y);

        if (det > -M.MAG_ERR && det < M.MAG_ERR) {
            i_x = i_y = 0;
            return false;
        }

        var s = (-s1_y * (p0_x - p2_x) + s1_x * (p0_y - p2_y)) / det;
        var t = ( s2_x * (p0_y - p2_y) - s2_y * (p0_x - p2_x)) / det;

        if (s >= 0 && s <= 1 && t >= 0 && t <= 1) {
            i_x = p0_x + (t * s1_x);
            i_y = p0_y + (t * s1_y);
            return true;
        }
        i_x = i_y = 0;
        return false; 
    }
    public static bool SegmentIntersection(float p0_x, float p0_y, float p1_x, float p1_y, 
        float p2_x, float p2_y, float p3_x, float p3_y) {
        var s1_x = p1_x - p0_x;     var s1_y = p1_y - p0_y;
        var s2_x = p3_x - p2_x;     var s2_y = p3_y - p2_y;

        var det = (-s2_x * s1_y + s1_x * s2_y);

        if (det > -M.MAG_ERR && det < M.MAG_ERR) {
            return false;
        }

        var s = (-s1_y * (p0_x - p2_x) + s1_x * (p0_y - p2_y)) / det;
        var t = ( s2_x * (p0_y - p2_y) - s2_y * (p0_x - p2_x)) / det;

        return (s >= 0 && s <= 1 && t >= 0 && t <= 1);
    }

    /// <summary>
    /// Returns true if the second point is counterclockwise from the first point relative to the source.
    /// </summary>
    public static bool IsCounterClockwise(float source_x, float source_y, float p1_x, float p1_y, float p2_x,
        float p2_y) {
        p1_x -= source_x;
        p1_y -= source_y;
        p2_x -= source_x;
        p2_y -= source_y;
        return p1_x * p2_y - p1_y * p2_x > 0;
    }

    /// <summary>
    /// Returns true if the second vector is counterclockwise from the first vector.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsCounterClockwise(in float v1x, in float v1y, in float v2x, in float v2y) =>
        v1x * v2y - v1y * v2x > 0;

    public static Vector3 CylinderWrap(float R, float a0, float aMax, float axis, Vector2 loc) {
        var cs = CosSin(axis);
        var xyd = ConvertBasis(loc, cs);
        float a = xyd.x / R;
        float aRem = 0;
        if (Math.Abs(a) > aMax) {
            if (a < 0) aMax *= -1;
            aRem = a - aMax;
            a = aMax;
        }
        a += a0;
        xyd.x = R * (Sin(a) - Sin(a0) + aRem *Cos(a));
        Vector3 v3 = DeconvertBasis(xyd, cs);
        v3.z = R * (Cos(a) - Cos(a0) - aRem *Sin(a));
        return v3;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double LerpU(double a, double b, double t) => a * (1 - t) + b * t;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Lerp(double a, double b, double t) {
        if (t < 0) return a;
        if (t > 1) return b;
        return a + (b - a) * t;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Lerp(float a, float b, float t) {
        if (t < 0) return a;
        if (t > 1) return b;
        return a + (b - a) * t;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Lerp(double low, double high, double controller, double a, double b) =>
        Lerp(a, b, (controller - low) / (high - low));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Lerp(float controllerZeroBound, float controllerOneBound, float controller, float a, float b) =>
        BMath.Lerp(a, b, (controller - controllerZeroBound) / (controllerOneBound - controllerZeroBound));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 Lerp(float controllerZeroBound, float controllerOneBound, float controller, Vector3 a, Vector3 b) =>
        Vector3.Lerp(a, b, (controller - controllerZeroBound) / (controllerOneBound - controllerZeroBound));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Lerp3(float lowest, float low, float high, float highest, float controller, float a, float b,
        float c) =>
        controller < high ? Lerp(lowest, low, controller, a, b) : Lerp(high, highest, controller, b, c);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 Lerp3(float lowest, float low, float high, float highest, float controller, Vector3 a, Vector3 b,
        Vector3 c) =>
        controller < high ? Lerp(lowest, low, controller, a, b) : Lerp(high, highest, controller, b, c);
    
    public static Vector3 MulBy(this Vector3 x, Vector3 m) => new Vector3(x.x * m.x, x.y * m.y, x.z * m.z);
    public static Bounds MulBy(this Bounds b, Vector3 m) {
        return new Bounds(b.center.MulBy(m), b.size.MulBy(m));
    }

    public static double BlockRound(double block, double value) => Math.Round(value / block) * block;

    public static Vector2 ShortestDistance(Rect src, Rect dst) {
        float dx = 0;
        if (dst.xMin > src.xMax)
            dx = dst.xMin - src.xMax;
        else if (dst.xMax < src.xMin)
            dx = dst.xMax - src.xMin;
        float dy = 0;
        if (dst.yMin > src.yMax)
            dy = dst.yMin - src.yMax;
        else if (dst.yMax < src.yMin)
            dy = dst.yMax - src.yMin;
        return new Vector2(dx, dy);
    }

    /// <summary>
    /// Find the shortest distance between two AABBs. On either axis, if the rects overlap but do not contain each other,
    /// then enforce a minimum distance.
    /// <example>Rect 1 has x [0, 10], Rect 2 has x [6, 20]. Enforce minimum distance</example>
    /// <example>Rect 1 has x [0, 10], Rect 2 has x [6, 8]. X-distance is 0</example>
    /// <example>Rect 1 has x [0, 10], Rect 2 has x [12, 20]. X-distance is 2</example>
    /// </summary>
    public static Vector2 ShortestDistancePushOutOverlap(Rect src, Rect dst, float enforce=1) {
        float dx = 0;
        if (dst.xMin > src.xMax)
            dx = dst.xMin - src.xMax;
        else if (dst.xMax < src.xMin)
            dx = dst.xMax - src.xMin;
        else if (dst.xMin < src.xMin && dst.xMax < src.xMax)
            dx = -enforce;
        else if (dst.xMin > src.xMin && dst.xMax > src.xMax)
            dx = enforce;
        float dy = 0;
        if (dst.yMin > src.yMax)
            dy = dst.yMin - src.yMax;
        else if (dst.yMax < src.yMin)
            dy = dst.yMax - src.yMin;
        else if (dst.yMin < src.yMin && dst.yMax < src.yMax)
            dy = -enforce;
        else if (dst.yMin > src.yMin && dst.yMax > src.yMax)
            dy = enforce;
        return new Vector2(dx, dy);
    }

    public static Rect RectFromCenter(Vector2 center, Vector2 wh) {
        return new Rect(center - wh / 2f, wh);
    }
}

public readonly struct Hurtbox {
    public readonly float x;
    public readonly float y;
    public readonly float radius;
    public readonly float radius2;
    public readonly float grazeRadius;
    public readonly float grazeRadius2;
    public readonly Vector2 location;

    public Hurtbox(Vector2 loc, float rad, float lrad) {
        location = loc;
        x = loc.x;
        y = loc.y;
        radius = rad;
        radius2 = rad * rad;
        grazeRadius = lrad;
        grazeRadius2 = lrad * lrad;
    }

    public Hurtbox(Vector2 loc, float rad) : this(loc, rad, rad) { }
}

public readonly struct CCircle {
    public readonly float x;
    public readonly float y;
    public readonly float r;

    public CCircle(float x, float y, float r) {
        this.x = x;
        this.y = y;
        this.r = r;
    }
    
    public static explicit operator CCircle(Vector3 x) => new CCircle(x.x, x.y, x.z);
    public static explicit operator Vector3(CCircle x) => new Vector3(x.x, x.y, x.r);
}

public readonly struct CRect {
    public readonly float x;
    public readonly float y;
    public readonly float halfW;
    public readonly float halfH;
    public readonly float cos_rot;
    public readonly float sin_rot;
    public readonly float angle;
    public Vector2 Offset => new Vector2(x, y);

    public CRect(float x, float y, float halfW, float halfH, float ang_deg) {
        this.x = x;
        this.y = y;
        this.halfW = halfW;
        this.halfH = halfH;
        this.angle = ang_deg;
        this.cos_rot = M.CosDeg(ang_deg);
        this.sin_rot = M.SinDeg(ang_deg);
    }

    public CRect(Transform tr, Bounds bounds) {
        var trloc = tr.position;
        var scale = tr.lossyScale;
        x = trloc.x + scale.x * bounds.center.x;
        y = trloc.y + scale.y * bounds.center.y;
        halfW = scale.x * bounds.extents.x;
        halfH = scale.y * bounds.extents.y;
        angle = tr.eulerAngles.z;
        this.cos_rot = M.CosDeg(angle);
        this.sin_rot = M.SinDeg(angle);
    }
    
    public static implicit operator CRect(V2RV2 rect) => new CRect(rect.nx, rect.ny, rect.rx, rect.ry, rect.angle);
}

/// <summary>
/// A position description composed of a nonrotational offset
/// and a rotational offset.
/// </summary>
public struct V2RV2 {
    /// <summary>
    /// X-component of nonrotational offset
    /// </summary>
    public float nx;
    /// <summary>
    /// Y-component of nonrotational offset
    /// </summary>
    public float ny;
    /// <summary>
    /// X-component of rotational offset
    /// </summary>
    public float rx;
    /// <summary>
    /// Y-component of rotational offset
    /// </summary>
    public float ry;
    /// <summary>
    /// Rotation (degrees) of rotational offset
    /// </summary>
    public float angle;
    public Vector2 NV => new Vector2(nx, ny);
    public Vector2 RV => new Vector2(rx, ry);
    
    public Vector2 TrueLocation => new Vector2(nx, ny) + M.RotateVectorDeg(rx, ry, angle);
    public static V2RV2 Zero => V2RV2.NRot(0, 0);

    public V2RV2(float nx, float ny, float rx, float ry, float angle_deg) {
        this.nx = nx;
        this.ny = ny;
        this.rx = rx;
        this.ry = ry;
        this.angle = angle_deg;
    }
    public V2RV2(Vector2 nxy, Vector2 rxy, float angle_deg) {
        this.nx = nxy.x;
        this.ny = nxy.y;
        this.rx = rxy.x;
        this.ry = rxy.y;
        this.angle = angle_deg;
    }

    public static V2RV2 NRot(float nx, float ny) => new V2RV2(nx, ny, 0, 0, 0);
    public static V2RV2 NRotAngled(float nx, float ny, float angle) => new V2RV2(nx, ny, 0, 0, angle);
    public static V2RV2 NRotAngled(Vector2 nv2, float angle) => new V2RV2(nv2.x, nv2.y, 0, 0, angle);
    public static V2RV2 Rot(float rx, float ry, float angle=0f) => new V2RV2(0,0,rx,ry,angle);
    public static V2RV2 Rot(Vector2 rot) => new V2RV2(0,0,rot.x,rot.y,0f);
    public static V2RV2 RX(float rx, float angle=0f) => new V2RV2(0,0,rx,0,angle);
    public static V2RV2 RY(float ry, float angle=0f) => new V2RV2(0,0,0,ry,angle);
    public static V2RV2 Angle(float angle) => new V2RV2(0, 0, 0, 0, angle);


    public V2RV2 Bank(float? new_angle_deg=null) {
        var tl = TrueLocation;
        return new V2RV2(tl.x, tl.y, 0, 0, new_angle_deg ?? angle);
    }

    public V2RV2 BankOffset(float angle_offset_deg) => Bank(angle + angle_offset_deg);

    public V2RV2 RotateAll(float by_deg) {
        var newnxy = M.RotateVectorDeg(nx, ny, by_deg);
        return new V2RV2(newnxy.x, newnxy.y, rx, ry, angle + by_deg);
    }
    
    public V2RV2 WithOffset(float onx, float ony) => new V2RV2(nx + onx, ny + ony, rx, ry, angle);
    public V2RV2 WithOffset(Vector2 nv2) => WithOffset(nv2.x, nv2.y);
    
    public V2RV2 ForceAngle(float new_ang) => new V2RV2(nx, ny, rx, ry, new_ang);
    
    public static V2RV2 operator +(V2RV2 a, V2RV2 b) {
        return new V2RV2(a.nx + b.nx, a.ny + b.ny, a.rx + b.rx, a.ry + b.ry, a.angle + b.angle);
    }
    public static V2RV2 operator -(V2RV2 a, V2RV2 b) {
        return new V2RV2(a.nx - b.nx, a.ny - b.ny, a.rx - b.rx, a.ry - b.ry, a.angle - b.angle);
    }
    public static V2RV2 operator *(float f, V2RV2 a) {
        return new V2RV2(f*a.nx, f*a.ny, f*a.rx, f*a.ry, f*a.angle);
    }
    public static V2RV2 operator /(V2RV2 a, float f) {
        return new V2RV2(a.nx/f, a.ny/f, a.rx/f, a.ry/f, a.angle/f);
    }
    public static V2RV2 operator +(V2RV2 a, float ang_deg) {
        return new V2RV2(a.nx, a.ny, a.rx, a.ry, a.angle + ang_deg);
    }
    public static V2RV2 operator +(V2RV2 a, Vector2 nv) {
        return new V2RV2(a.nx + nv.x, a.ny + nv.y, a.rx, a.ry, a.angle);
    }
    public static V2RV2 operator +(V2RV2 a, Vector3 rva) {
        return new V2RV2(a.nx, a.ny, a.rx + rva.x, a.ry + rva.y, a.angle + rva.z);
    }
    public override string ToString() {
        return $"<{(decimal) nx},{(decimal) ny}:{(decimal) rx},{(decimal) ry}:{(decimal) angle}>";
    }
}

public static class Parser {
    public const char SM_REF_KEY_C = '&';
    public const string SM_REF_KEY = "&";
    private const char decpt = '.';
    private const char zero = '0';

    public static float Float(string s) {
        if (TryFloat(s, out float f)) return f;
        throw new InvalidCastException($"Cannot convert \"{s}\" to float.");
    }
    public static bool TryFloat(string s, out float f) {
        return TryFloat(s, 0, s.Length, out f);
    }

    public static float? MaybeFloat(string s) => TryFloat(s, out var f) ? f : (float?)null;
    public static float Float(string s, int from, int to) {
        if (TryFloat(s, from, to, out float f)) return f;
        throw new InvalidCastException($"Cannot convert \"{s}\" to float.");
    }

    private const char CPI = 'π';
    private const char CPHI = 'p';
    private const char CINVPHI = 'h';
    private const char CFRAME = 'f';
    private const char CFPS = 's';
    private const char C360H = 'c';
    /// <summary>
    /// Supported shortcuts:
    /// <para>Up to two +- signs at the front</para>
    /// <para>Multiplier suffixes: p=phi, h=1/phi, f=1/120 (frame time), s=120 (fps)</para>
    /// <para>Effect suffixes: c = return 360h/x</para>
    /// </summary>
    /// <param name="s">String to parse</param>
    /// <param name="from">Starting index</param>
    /// <param name="to">Ending index (exclusive)</param>
    /// <param name="f">(out) Parsed float value</param>
    /// <returns></returns>
    public static bool TryFloat(string s, int from, int to, out float f) {
        f = 0f;
        if (to == from) return true;
        if (to == from + 1 && s[from] == '_') {
            f = M.IntFloatMax;
            return true;
        }
        float dec_mult = 0.1f;
        float multiplier = 1f;
        bool foundDecimal = false;
        int ii = from;
        char first = s[from];
        int slen = s.Length;
        bool c360inv = false;
        //Allow --, +-, -+, ++ at front; these are parsed as signs.
        if (first == '-') {
            ++ii;
            if (ii >= slen) return false;
            if (s[ii] == '-') { 
                ++ii;
            } else {
                if (s[ii] == '+') ++ii;
                multiplier *= -1;
            }
        } else if (first == '+') {
            ++ii;
            if (ii >= slen) return false;
            if (s[ii] == '+') {
                ++ii;
            } else if (s[ii] == '-') {
                ++ii;
                multiplier *= -1;
            }
        }
        for (; ii < to; ++ii) {
            char c = s[ii];
            if (c == decpt) {
                foundDecimal = true;
            } else {
                int val = c - zero;
                if (val < 0 || val > 9) {
                    if (c == CPHI) {
                        multiplier *= PHI;
                    } else if (c == CINVPHI) {
                        multiplier *= IPHI;
                    } else if (c == CFRAME) {
                        multiplier *= ETime.FRAME_TIME;
                    } else if (c == CFPS) {
                        multiplier *= ETime.ENGINEFPS_F;
                    } else if (c == CPI) {
                        multiplier *= PI;
                    } else if (c == C360H) {
                        c360inv = true;
                    } else return false;
                } else if (foundDecimal) {
                    f += dec_mult * val;
                    dec_mult *= 0.1f;
                } else {
                    f *= 10f;
                    f += val;
                }
            }
        }
        f *= multiplier;
        if (c360inv) f = 360f * IPHI / f;
        return true;
    }

    public static CCircle ParseCircle(string s) {
        //<x;y;r>
        string[] parts = s.Split(';');
        return new CCircle(
            Float(parts[0].Substring(1)),
            Float(parts[1]),
            Float(parts[2].Substring(0, parts[2].Length - 1))
        );
    }

    public static CRect ParseRect(string s) {
        //<x;y:hW;hH:ang>
        string[] parts = s.Split(':');
        int comma = parts[0].IndexOf(";");
        int comma2 = parts[1].IndexOf(";");
        return new CRect(
            Parser.Float(parts[0].Substring(1, comma - 1)),
            Parser.Float(parts[0].Substring(comma + 1, parts[0].Length - comma - 1)),
            Parser.Float(parts[1].Substring(0, comma2)),
            Parser.Float(parts[1].Substring(comma2 + 1, parts[1].Length - comma2 - 1)),
            Parser.Float(parts[2].Substring(0, parts[2].Length - 1))
        );
    }

    private static float NextFloat(string s, ref int from, ref int ii, char until) {
        while (++ii < s.Length) {
            if (s[ii] == until) {
                var f = Float(s, from, ii);
                from = ii + 1;
                return f;
            }
        }
        throw new Exception("Couldn't find enough float values in the string.");
    }
    public static V2RV2 ParseV2RV2(string s) {
        // Format: <float;float:float;float:float> (nx,ny,rx,ry,angle)
        // OR the RV2 format (rx,ry,angle).
        if (s == "<>") return V2RV2.Zero;
        if (s.CountOf(':') == 0) return V2RV2.Angle(Float(s, 1, s.Length - 1));
        if (s.CountOf(':') == 1) return ParseShortV2RV2(s);
        int ii = 0;
        int from = 1;
        var nx = NextFloat(s, ref from, ref ii, ';');
        var ny = NextFloat(s, ref from, ref ii, ':');
        var rx = NextFloat(s, ref from, ref ii, ';');
        var ry = NextFloat(s, ref from, ref ii, ':');
        return new V2RV2(nx, ny, rx, ry, Float(s, from, s.Length - 1));
    }

    private static V2RV2 ParseShortV2RV2(string s) {
        // Format: <float;float:float> ; args are rx,ry,angle resp.
        int ii = 0;
        int from = 1;
        float x = 0;
        while (++ii < s.Length) {
            if (s[ii] == ';') {
                x = Float(s, from, ii);
                from = ii + 1;
                break;
            }
        }
        while (++ii < s.Length) {
            if (s[ii] == ':') {
                float y = Float(s, from, ii);
                from = ii + 1;
                return V2RV2.Rot(x, y, Float(s, from, s.Length - 1));
            }
        }
        throw new FormatException("Bad V2RV2 formatting: " + s);
    }
    
}
}
