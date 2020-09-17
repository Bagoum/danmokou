using System;
using Danmaku;
using Core;
using UnityEngine;
using static DMath.NoExprMath_1;
using static DMath.NoExprMath_2;
using static Danmaku.VTPConstructors;
using static Danmaku.VTPControllers;

namespace DMath {
/// <summary>
/// A repo containing a few key math functions for use in IL2CPP demos while expressions are unusable.
/// </summary>
public static class NoExprMath_1 {
    [Fallthrough]
    public static GCXF<float> GCXF(BPY f) => gcx => f(gcx.AsBPI);
    [Fallthrough]
    public static GCXF<V2RV2> GCXF(BPRV2 f) => gcx => f(gcx.AsBPI);
    //lmao what a shitty limitation
    private static GCXU<T> _GCXU<T>(T f) => new GCXU<T>((GenCtx gcx, ref uint id) => f, (gcx, id) => f);
    [Fallthrough]
    public static GCXU<VTP> GCXU(VTP x) => _GCXU(x);
    [Alias("tprot")]
    public static VTP RVelocity(TP rv) => VTPControllers.Velocity(CartesianRot(rv));
    public static VTP NROffset(TP nrp) => VTPControllers.Offset(CartesianNRot(nrp));

    public static readonly BPY b0 = _ => 0f;
    [Fallthrough]
    public static BPY Const(float f) => _ => f;
    [Fallthrough]
    public static FXY Constf(float f) => _ => f;
    [Fallthrough]
    public static BPRV2 Const(V2RV2 rv2) => _ => rv2;

    public static TP Loc() => b => b.loc;
    public static FXY X() => x => x;
    public static BPY T() => b => b.t;
    public static BPY P() => b => b.index;
    [Alias("+")]
    public static BPY Add(BPY x, BPY y) => b => x(b) + y(b);
    [Alias("+")]
    public static FXY Add(FXY x, FXY y) => b => x(b) + y(b);
    [Alias("-")]
    public static BPY Sub(BPY x, BPY y) => b => x(b) - y(b);
    [Alias("-")]
    public static TP Sub(TP x, TP y) => b => x(b) - y(b);
    [Alias("*")]
    public static BPY Mul(BPY x, BPY y) => b => x(b) * y(b);
    [Alias("*")]
    public static FXY Mul(FXY x, FXY y) => b => x(b) * y(b);
    [Alias("*")]
    public static TP Mul(BPY x, TP y) => b => x(b) * y(b);
    [Alias("/")]
    public static BPY Div(BPY x, BPY y) => b => x(b) / y(b);
    [Alias("/")]
    public static FXY Div(FXY x, FXY y) => b => x(b) / y(b);
    [Alias("//")]
    public static BPY FDiv(BPY x, BPY y) => b => Mathf.Floor(x(b) / y(b));
    [Alias("^")]
    public static BPY Pow(BPY x, BPY y) => b => Mathf.Pow(x(b), y(b));
    [Alias("^")]
    public static FXY Pow(FXY x, FXY y) => b => Mathf.Pow(x(b), y(b));
    public static BPY Sqrt(BPY x) => b => Mathf.Sqrt(x(b));
    public static FXY Sqrt(FXY x) => b => Mathf.Sqrt(x(b));
    public static BPY Atan2(BPY y, BPY x) => b => M.Atan2D(y(b), x(b));
    public static FXY Atan2(FXY y, FXY x) => b => M.Atan2D(y(b), x(b));
    [Alias("-m")]
    public static BPY SubMax0(BPY x, BPY y) => b => Math.Max(0, x(b) - y(b));
    [Alias("-m")]
    public static FXY SubMax0(FXY x, FXY y) => b => Math.Max(0, x(b) - y(b));
    public static BPY Min(BPY x, BPY y) => b => Math.Min(x(b), y(b));
    public static FXY Min(FXY x, FXY y) => b => Math.Min(x(b), y(b));
    public static BPY Max(BPY x, BPY y) => b => Math.Max(x(b), y(b));
    public static FXY Max(FXY x, FXY y) => b => Math.Max(x(b), y(b));

    public static BPY Mod(BPY m, BPY x) => b => M.Mod(m(b), x(b));
    public static BPY Softmod(BPY m, BPY x) => b => {
        var by = m(b);
        var xm2 = M.Mod(by * 2, x(b));
        return xm2 < by ? xm2 : 2 * by - xm2;
    };
    
    public static TP CXY(float x, float y) => b => new Vector2(x, y);
    public static TP CX(float x) => CXY(x, 0);
    public static TP CY(float y) => CXY(0, y);
    public static TP PXY(BPY x, BPY y) => b => new Vector2(x(b), y(b));
    public static TP PX(BPY x) => PXY(x, b0);
    public static TP PY(BPY y) => PXY(b0, y);
    public static TP RX(BPY x, BPY rot) => b => x(b) * M.CosSinDeg(rot(b));

    [Fallthrough(100, true)]
    public static TP TP3XY(TP3 x) => b => x(b);

    private static float Lerp(float z, float o, float c, float f1, float f2) {
        var r = Mathf.Clamp01((c - z) / (o - z));
        return r * f2 + (1 - r) * f1;
    }

    //note: generics won't work since BPY cannot be treated as a generic of Func<ParametricInfo, T>
    //also T doesn't have math operators...
    public static BPY Lerp(BPY z, BPY o, BPY c, BPY f1, BPY f2) => b => Lerp(z(b), o(b), c(b), f1(b), f2(b));
    public static BPY Lerp3(BPY z, BPY o, BPY z2, BPY o2, BPY c, BPY f1, BPY f2, BPY f3) => b => {
        float _z2 = z2(b);
        float _c = c(b);
        return _c < _z2 ? 
            Lerp(z(b), o(b), _c, f1(b), f2(b)) : 
            Lerp(_z2, o2(b), _c, f2(b), f3(b));
    };

    public static BPY LerpBack(BPY z, BPY o, BPY o2, BPY z2, BPY c, BPY f1, BPY f2) =>
        Lerp3(z, o, o2, z2, c, f1, f2, f1);

    
    public static TP SS0(TP x) {
        var data = DataHoisting.GetClearableDictV2_();
        return b => {
            if (!data.TryGetValue(b.id, out var v)) {
                data[b.id] = v = x(b);
            }
            return v;
        };
    }

    public static BPY Smooth(string name, BPY controller) => controller; //Can't do much here
    
    public static TP EaseToTarget(string ease, BPY time, TP location) => 
        Mul(Smooth(ease, Div(T(), time)), SS0(Sub(location, Loc())));
}

public static class NoExprMath_2 {

    public static TP3 PXYZ(BPY x, BPY y, BPY z) => b => new Vector3(x(b), y(b), z(b));
    public static TP3 PX(BPY x) => PXYZ(x, b0, b0);
    public static TP3 PY(BPY y) => PXYZ(b0, y, b0);
    public static TP3 PZ(BPY z) => PXYZ(b0, b0, z);

    public static TP3 QRotate(TP3 rot, TP3 x) => b => Quaternion.Euler(rot(b)) * x(b);

    [Fallthrough]
    public static TP3 TP(TP x) => b => x(b);
}
}