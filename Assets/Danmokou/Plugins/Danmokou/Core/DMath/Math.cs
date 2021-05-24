using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using Danmokou.Core;
using Ex = System.Linq.Expressions.Expression;

namespace Danmokou.DMath {
public static class M {
    public const float HPI = Mathf.PI * 0.5f;
    public const float PI = Mathf.PI;
    public const float NPI =- Mathf.PI;
    public const float TAU = 2f * PI;
    public const float TWAU = 4f * PI;
    public const float PHI = 1.6180339887498948482045868343656381f;
    public const float IPHI = PHI - 1f;
    public const float degRad = PI / 180f;
    public const float radDeg = 180f / PI;
    public const float MAG_ERR = 1e-10f;
    public const int IntFloatMax = int.MaxValue / 2;

    public static float Mod(float by, float x) => x - by * Mathf.Floor(x / by);

    public static int Mod(int by, int x) {
        x %= by;
        return (x < 0) ? x + by : x;
    }

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Sin(float rad) => (float)Math.Sin(rad);

    public static float Sine(float period, float amp, float t) => amp * (float) Math.Sin(t * M.TAU / period);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Cos(float rad) => (float)Math.Cos(rad);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 CosSin(float rad) => new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float SinDeg(float deg) => (float)Math.Sin(deg * degRad);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float CosDeg(float deg) => (float)Math.Cos(deg * degRad);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 CosSinDeg(float deg) => CosSin(deg * degRad);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 PolarToXY(float deg) {
        return new Vector2(CosDeg(deg), SinDeg(deg));
    }
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
    public static float Atan(Vector2 v2) => Mathf.Atan2(v2.y, v2.x);

    /// <summary>
    /// Get a point on the unit sphere.
    /// </summary>
    /// <param name="theta">Angle in the XY plane (0 = X-axis)</param>
    /// <param name="phi">Divergence from the Z axis (pi/2 = XY-plane)</param>
    /// <returns></returns>
    public static Vector3 Spherical(float theta, float phi) {
        float sp = Mathf.Sin(phi);
        return new Vector3(Mathf.Cos(theta)*sp, Mathf.Sin(theta)*sp, Mathf.Cos(phi));
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 RotateVector(Vector2 init, float cos_rot, float sin_rot) {
        return new Vector2(cos_rot * init.x - sin_rot * init.y, sin_rot * init.x + cos_rot * init.y);
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
        float cos_rot = Mathf.Cos(ang);
        float sin_rot = Mathf.Sin(ang);
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
        float cos_rot = Mathf.Cos(ang);
        float sin_rot = Mathf.Sin(ang);
        return new Vector2(cos_rot * x - sin_rot * y, sin_rot * x + cos_rot * y);
    }
    public static Vector2 ConvertBasis(Vector2 source, Vector2 basis1) => RotateVector(source, basis1.x, -basis1.y);
    public static Vector2 DeconvertBasis(Vector2 source, Vector2 basis1) => RotateVector(source, basis1.x, basis1.y);

    public static Vector2 ProjectionUnit(Vector2 of, Vector2 onto1) {
        float dot = of.x * onto1.x + of.y * onto1.y;
        return dot * onto1;
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
        return Mathf.Atan2(diff.y, diff.x);
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
    /// <param name="source_x"></param>
    /// <param name="source_y"></param>
    /// <param name="p1_x"></param>
    /// <param name="p1_y"></param>
    /// <param name="p2_x"></param>
    /// <param name="p2_y"></param>
    /// <returns></returns>
    public static bool IsCounterClockwise(float source_x, float source_y, float p1_x, float p1_y, float p2_x,
        float p2_y) {
        p1_x -= source_x;
        p1_y -= source_y;
        p2_x -= source_x;
        p2_y -= source_y;
        return p1_x * p2_y - p1_y * p2_x > 0;
    }

    public static Vector3 CylinderWrap(float R, float a0, float aMax, float axis, Vector2 loc) {
        var cs = CosSin(axis);
        var xyd = ConvertBasis(loc, cs);
        float a = xyd.x / R;
        float aRem = 0;
        if (Mathf.Abs(a) > aMax) {
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
    public static double Lerp(double a, double b, double t) => LerpU(a, b, Clamp(0, 1, t));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Lerp(double low, double high, double controller, double a, double b) =>
        Lerp(a, b, (controller - low) / (high - low));
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float LerpU(float a, float b, float t) => a * (1 - t) + b * t;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Lerp(float a, float b, float t) => LerpU(a, b, Clamp(0, 1, t));
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Lerp(float low, float high, float controller, float a, float b) =>
        Lerp(a, b, (controller - low) / (high - low));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 Lerp(float low, float high, float controller, Vector3 a, Vector3 b) =>
        Vector3.Lerp(a, b, (controller - low) / (high - low));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Lerp3(float lowest, float low, float high, float highest, float controller, float a, float b,
        float c) =>
        controller < high ? Lerp(lowest, low, controller, a, b) : Lerp(high, highest, controller, b, c);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 Lerp3(float lowest, float low, float high, float highest, float controller, Vector3 a, Vector3 b,
        Vector3 c) =>
        controller < high ? Lerp(lowest, low, controller, a, b) : Lerp(high, highest, controller, b, c);
            

    /// <summary>
    /// Returns (x - a) / (b - a); ie. t such that LerpUnclamped(a, b, t) = x.
    /// </summary>
    public static double Ratio(double a, double b, double x) => (x - a) / (b - a);
    public static float Ratio(float a, float b, float x) => (x - a) / (b - a);
    /// <summary>
    /// Returns (x - a) / (b - a) clamped to (0, 1).
    /// </summary>
    
    public static float RatioC(float a, float b, float x) => Mathf.Clamp01((x - a) / (b - a));

    public static float EInSine(float x) => 1f - (float) Math.Cos(HPI * x);
    public static float EOutSine(float x) => (float) Math.Sin(HPI * x);
    public static float EIOSine(float x) => 0.5f - 0.5f * (float) Math.Cos(PI * x);
    public static float DEOutSine(float x) => HPI * (float) Math.Cos(HPI * x);
    public static float EOutPow(float x, float pow) => 1f - Mathf.Pow(1f - x, pow);
    public static float EOutQuad(float x) => 1f - Mathf.Pow(1f - x, 4f);

    public static float Identity(float x) => x;

    public static float EOutBack(float a, float x) {
        return 1 + (a+1) * Mathf.Pow(x - 1, 3) + a * (x - 1) * (x - 1);
    }
    
    public static Vector3 MulBy(this Vector3 x, Vector3 m) => new Vector3(x.x * m.x, x.y * m.y, x.z * m.z);
    public static Bounds MulBy(this Bounds b, Vector3 m) {
        return new Bounds(b.center.MulBy(m), b.size.MulBy(m));
    }

    public static double BlockRound(double block, double value) => Math.Round(value / block) * block;
}

public readonly struct Hitbox {
    public readonly float x;
    public readonly float y;
    public readonly float radius;
    public readonly float radius2;
    public readonly float largeRadius;
    public readonly float largeRadius2;

    public Hitbox(Vector2 loc, float rad, float lrad) {
        x = loc.x;
        y = loc.y;
        radius = rad;
        radius2 = rad * rad;
        largeRadius = lrad;
        largeRadius2 = lrad * lrad;
    }
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
public readonly struct V2RV2 {
    /// <summary>
    /// X-component of nonrotational offset
    /// </summary>
    public readonly float nx;
    /// <summary>
    /// Y-component of nonrotational offset
    /// </summary>
    public readonly float ny;
    /// <summary>
    /// X-component of rotational offset
    /// </summary>
    public readonly float rx;
    /// <summary>
    /// Y-component of rotational offset
    /// </summary>
    public readonly float ry;
    /// <summary>
    /// Rotation (degrees) of rotational offset
    /// </summary>
    public readonly float angle;
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

/// <summary>
/// For cleanliness, V2RV2 is immutable. If you need V2RV2s in the inspector,
/// use this struct instead, as the inspector cannot work with immutability.
/// </summary>
[Serializable]
public struct MutV2RV2 {
    public float nx;
    public float ny;
    public float rx;
    public float ry;
    public float angle;
    public MutV2RV2(float nx, float ny, float rx, float ry, float angle_deg) {
        this.nx = nx;
        this.ny = ny;
        this.rx = rx;
        this.ry = ry;
        this.angle = angle_deg;
    }
    public static implicit operator MutV2RV2(V2RV2 rv) => new MutV2RV2(rv.nx, rv.ny, rv.rx, rv.ry, rv.angle);
    public static implicit operator V2RV2(MutV2RV2 rv) => new V2RV2(rv.nx, rv.ny, rv.rx, rv.ry, rv.angle);
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
                        multiplier *= M.PHI;
                    } else if (c == CINVPHI) {
                        multiplier *= M.IPHI;
                    } else if (c == CFRAME) {
                        multiplier *= ETime.FRAME_TIME;
                    } else if (c == CFPS) {
                        multiplier *= ETime.ENGINEFPS_F;
                    } else if (c == CPI) {
                        multiplier *= M.PI;
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
        if (c360inv) f = 360f * M.IPHI / f;
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
