using System;
using DMK.Core;
using DMK.Danmaku;
using DMK.DataHoist;
using DMK.DMath.Functions;
using DMK.Expressions;
using DMK.Reflection;
using UnityEngine;
using static DMK.DMath.Functions.NoExprMath_1;
using static DMK.DMath.Functions.NoExprMath_2;
using static DMK.DMath.Functions.VTPConstructors;
using static DMK.DMath.Functions.VTPControllers;

namespace DMK.DMath.Functions {
/// <summary>
/// A repo containing a few key math functions for use in IL2CPP demos while expressions are unusable.
/// NOTE: DO __NOT__ USE THESE FOR STANDARD HANDLING.
/// </summary>
public static class NoExprMath_1 {
    /// <summary>
    /// Yes, this is INSANELY stupid and completely unacceptable architecture.
    /// However, it's for demo purposes only (keyword NO_EXPR) and is not used in any standard pathway.
    /// </summary>
    public static GenCtx boundGCX;
    //Likewise...
    public static float LaserTime;

    private static GCXF<T> GCXF<T>(Func<ParametricInfo, T> f) => gcx => {
        boundGCX = gcx;
        var result = f(gcx.AsBPI);
        boundGCX = null;
        return result;
    };

    [Fallthrough]
    public static GCXF<float> GCXF(BPY f) => GCXF<float>(x => f(x));
    [Fallthrough]
    public static GCXF<Vector2> GCXF(TP f) => GCXF<Vector2>(x => f(x));
    [Fallthrough]
    public static GCXF<V2RV2> GCXF(BPRV2 f) => GCXF<V2RV2>(x => f(x));

    private static GCXU<T> _GCXU<T>(T f) {
        var bound = new (Reflector.ExType, string)[0];
        return new GCXU<T>((GenCtx gcx, ref uint id) => {
            PrivateDataHoisting.UploadNew(bound, gcx, ref id);
            PrivateDataHoisting.UploadAllFloats(gcx, id);
            return f;
        }, (gcx, id) => {
            PrivateDataHoisting.UploadAllFloats(gcx, id);
            return f;
        });
    }

    [Fallthrough]
    public static GCXU<VTP> GCXU(VTP x) => _GCXU(x);
    [Fallthrough]
    public static GCXU<LVTP> GCXU(LVTP x) => _GCXU(x);
    [Fallthrough]
    public static GCXU<BPY> GCXU(BPY x) => _GCXU(x);
    [Fallthrough]
    public static GCXU<SBV2> GCXU(SBV2 x) => _GCXU(x);
    public static VTP RVelocity(TP rv) => VTPControllers.Velocity(CartesianRot(rv));
    public static VTP NRVelocity(TP rv) => VTPControllers.Velocity(CartesianNRot(rv));
    public static VTP ROffset(TP nrp) => VTPControllers.Offset(CartesianRot(nrp));
    public static VTP NROffset(TP nrp) => VTPControllers.Offset(CartesianNRot(nrp));
    public static VTP Offset(TP rp, TP nrp) => VTPControllers.Offset(Cartesian(rp, nrp));
    public static VTP Polar(BPY r, BPY theta) => VTPControllers.Offset(VTPConstructors.Polar(r, theta));
    public static VTP Null() => VTPRepo.NoVTP;
    [Fallthrough]
    public static LVTP LVTP(VTP vtp) {
        var dummyVel = Movement.None;
        return (in LaserMovement vel, in float dT, in float lT, ParametricInfo bpi, out Vector2 nrv) => {
            LaserTime = lT;
            dummyVel.rootPos = vel.rootPos;
            dummyVel.cos_rot = vel.cos_rot;
            dummyVel.sin_rot = vel.sin_rot;
            vtp(in dummyVel, in dT, bpi, out nrv);
        };
    }

    public static readonly BPY b0 = _ => 0f;
    [Fallthrough]
    public static BPY Const(float f) => _ => f;
    [Fallthrough]
    public static FXY Constf(float f) => _ => f;
    [Fallthrough]
    public static BPRV2 Const(V2RV2 rv2) => _ => rv2;

    [Fallthrough]
    public static SBV2 TP(TP tp) => (ref BulletManager.SimpleBullet sb) => tp(sb.bpi);

    [Alias("persist")]
    [Alias("_")]
    public static Pred True() => _ => true;

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
    public static BPY AtanR(TP xy) => b => M.Atan(xy(b));
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

    public static BPY Limit(BPY by, BPY x) => b => {
        var _x = x(b);
        return _x > 0 ? Math.Min(_x, @by(b)) : Math.Max(_x, -@by(b));
    };

    public static BPY Mod(BPY m, BPY x) => b => M.Mod(m(b), x(b));
    public static BPY Softmod(BPY m, BPY x) => b => {
        var by = m(b);
        var xm2 = M.Mod(by * 2, x(b));
        return xm2 < by ? xm2 : 2 * by - xm2;
    };

    public static BPY Sine(BPY period, BPY amplitude, BPY time) => b =>
        amplitude(b) * (float) Math.Sin(time(b) * M.TAU / period(b));
    public static BPY Cosine(BPY period, BPY amplitude, BPY time) => b =>
        amplitude(b) * (float) Math.Cos(time(b) * M.TAU / period(b));

    public static TP RotateV(TP cs, TP vec) => b => {
        var _cs = cs(b);
        var x = vec(b);
        return new Vector2(_cs.x * x.x - _cs.y * x.y, _cs.y * x.x + _cs.x * x.y);
    };
    public static TP CosSin(BPY rad) => b => {
        var r = rad(b);
        return new Vector2((float) Math.Cos(r), (float)Math.Sin(r));
    };
    public static TP CosSinDeg(BPY rad) => b => {
        var r = rad(b);
        return new Vector2((float) Math.Cos(r * M.degRad), (float)Math.Sin(r * M.degRad));
    };

    public static TP Circle(BPY period, BPY amplitude, BPY time) =>
        Mul(amplitude, CosSin(b => time(b) * M.TAU / period(b)));
    
    public static TP CXY(float x, float y) => b => new Vector2(x, y);
    public static TP CX(float x) => CXY(x, 0);
    public static TP CY(float y) => CXY(0, y);
    public static TP PXY(BPY x, BPY y) => b => new Vector2(x(b), y(b));
    public static TP PX(BPY x) => PXY(x, b0);
    public static TP PY(BPY y) => PXY(b0, y);
    public static TP RX(BPY x, BPY rot) => b => x(b) * M.CosSinDeg(rot(b));
    
    public static BPRV2 RXY(BPY rx, BPY ry) => b => new V2RV2(0, 0, rx(b), ry(b), 0);

    [Fallthrough(100, true)]
    public static TP TP3XY(TP3 x) => b => x(b);

    private static float Lerp(float z, float o, float c, float f1, float f2) {
        var r = Mathf.Clamp01((c - z) / (o - z));
        return r * f2 + (1 - r) * f1;
    }
    private static Vector2 Lerp(float z, float o, float c, Vector2 f1, Vector2 f2) {
        var r = Mathf.Clamp01((c - z) / (o - z));
        return r * f2 + (1 - r) * f1;
    }

    //note: generics won't work since BPY cannot be treated as a generic of Func<ParametricInfo, T>
    //also T doesn't have math operators...
    public static BPY Lerp(BPY z, BPY o, BPY c, BPY f1, BPY f2) => b => Lerp(z(b), o(b), c(b), f1(b), f2(b));
    public static TP Lerp(BPY z, BPY o, BPY c, TP f1, TP f2) => b => Lerp(z(b), o(b), c(b), f1(b), f2(b));
    public static BPY Lerp3(BPY z, BPY o, BPY z2, BPY o2, BPY c, BPY f1, BPY f2, BPY f3) => b => {
        float _z2 = z2(b);
        float _c = c(b);
        return _c < _z2 ? 
            Lerp(z(b), o(b), _c, f1(b), f2(b)) : 
            Lerp(_z2, o2(b), _c, f2(b), f3(b));
    };

    public static BPY Smooth(string smoother, BPY x) {
        var func = EasingFunctionRemote.GetRemoteEaser(smoother);
        return b => func(x(b));
    }
    public static BPY LerpSmooth(string smoother, BPY z, BPY o, BPY c, BPY f1, BPY f2) {
        var sf = EasingFunctionRemote.GetRemoteEaser(smoother);
        return b => {
            var _z = z(b);
            var ratio = Mathf.Clamp01((c(b) - _z) / (o(b) - _z));
            ratio = sf(ratio);
            return ratio * f2(b) + (1 - ratio) * f1(b);
        };
    }

    public static BPY LerpBack(BPY z, BPY o, BPY o2, BPY z2, BPY c, BPY f1, BPY f2) =>
        Lerp3(z, o, o2, z2, c, f1, f2, f1);

    public static BPY If(Pred cond, BPY ifTrue, BPY ifFalse) => b => cond(b) ? ifTrue(b) : ifFalse(b);

    public static Pred Even(BPY num) => b => num(b) % 2 == 0;
    
    public static TP SS0(TP x) {
        var data = DataHoisting.GetClearableDictV2_();
        return b => {
            if (!data.TryGetValue(b.id, out var v)) {
                data[b.id] = v = x(b);
            }
            return v;
        };
    }
    public static TP StopSampling(float cutoff, TP x) {
        var data = DataHoisting.GetClearableDictV2_();
        return b => {
            if (b.t < cutoff || !data.TryGetValue(b.id, out var v)) {
                data[b.id] = v = x(b);
            }
            return v;
        };
    }

    public static TP VHome(BPY speed, TP target) => b => {
        var delta = target(b) - b.loc;
        return delta * (speed(b) / (delta.magnitude + M.MAG_ERR));
    };

    private static BPY RadIntoRange(BPY angle) => b => {
        var a = angle(b);
        if (a > M.PI) return a - M.TAU;
        else if (a < -M.PI) return a + M.TAU;
        else return a;
    };

    private static BPY RadDiff(TP target, TP source) => 
        RadIntoRange(Sub(AtanR(target), AtanR(source)));

    public static TP TrueRotateLerpRate(BPY rate, TP from, TP to) {
        var data = DataHoisting.GetClearableDictV2_();
        Vector2 src = new Vector2();
        float r = 0;
        var getDelta = RadDiff(to, _ => src);
        var getNext = RotateV(CosSin(Limit(_ => r, getDelta)), _ => src);
        return b => {
            if (!data.TryGetValue(b.id, out src)) data[b.id] = src = from(b);
            r = rate(b) * M.degRad * ETime.FRAME_TIME;
            return data[b.id] = getNext(b);
        };

    }
    
    public static TP EaseToTarget(string ease, BPY time, TP location) => 
        Mul(Smooth(ease, Div(T(), time)), SS0(Sub(location, Loc())));

    public static TP LPlayer() => _ => LocationHelpers.GetEnemyVisiblePlayer();

    [Alias(">")]
    public static Pred GT(BPY x, BPY y) => b => x(b) > y(b);
}

public static class NoExprMath_2 {

    /// <summary>
    /// No lets! Can't make that work without expressions
    /// </summary>
    public static BPY ReferenceFloat(string alias) {
        if (alias[0] == Parser.SM_REF_KEY_C) alias = alias.Substring(1); 
        bool isExplicit = alias.StartsWith(".");
        if (isExplicit) alias = alias.Substring(1);
        if (alias == "lt") return b => LaserTime;
        var key = PrivateDataHoisting.GetKey(alias);
        return b => {
            if (boundGCX != null && boundGCX.TryGetFloat(alias, out var f)) return f;
            return PrivateDataHoisting.GetFloat(b.id, key);
        };
    }
    [Alias("@")]
    public static TP RetrieveHoisted(ReflectEx.Hoist<Vector2> hoist, BPY indexer) => b => 
        hoist.Retrieve((int)indexer(b));
    [Alias("@")]
    public static BPY RetrieveHoisted(ReflectEx.Hoist<float> hoist, BPY indexer) => b => 
        hoist.Retrieve((int)indexer(b));

    public static TP3 PXYZ(BPY x, BPY y, BPY z) => b => new Vector3(x(b), y(b), z(b));
    public static TP3 PX(BPY x) => PXYZ(x, b0, b0);
    public static TP3 PY(BPY y) => PXYZ(b0, y, b0);
    public static TP3 PZ(BPY z) => PXYZ(b0, b0, z);

    public static TP3 QRotate(TP3 rot, TP3 x) => b => Quaternion.Euler(rot(b)) * x(b);

    [Fallthrough]
    public static TP3 TP(TP x) => b => x(b);

    public static SBV2 Loc() => (ref BulletManager.SimpleBullet sb) => sb.bpi.loc;
    public static SBV2 Dir() => (ref BulletManager.SimpleBullet sb) => sb.direction;
}
}