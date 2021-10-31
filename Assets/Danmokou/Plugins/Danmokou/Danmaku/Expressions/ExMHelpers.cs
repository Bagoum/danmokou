using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Linq.Expressions;
using BagoumLib.Expressions;
using Danmokou.Core;
using Danmokou.DataHoist;
using Danmokou.DMath;
using Danmokou.DMath.Functions;
using Danmokou.Reflection;
using Ex = System.Linq.Expressions.Expression;
using tfloat = Danmokou.Expressions.TEx<float>;
using static Danmokou.Expressions.ExUtils;
using static Danmokou.DMath.Functions.ExM;
using static Danmokou.DMath.Functions.ExMConversions;

namespace Danmokou.Expressions {
/// <summary>
/// Expression helpers for mathematical operations.
/// </summary>
public static class ExMHelpers {
    private const int LookupCt = 1 << 21;
    private const int LookupMask = (1 << 21) - 1;
    private const double dTAU = Math.PI * 2;
    private const double radRatio = LookupCt / dTAU;
    private const double degRatio = LookupCt / 360.0;
    public static readonly Vector2[] LookupTable = new Vector2[LookupCt];
    public static readonly Expression exLookupTable = Expression.Field(null, typeof(ExMHelpers), "LookupTable");
    static ExMHelpers() {
        const double piIncr = dTAU / LookupCt;
        for (int ii = 0; ii < LookupCt; ++ii) {
            LookupTable[ii] = new Vector2((float) Math.Cos(ii * piIncr), (float) Math.Sin(ii * piIncr));
        }
    }
    //This is exactly what Mathf does, avoiding the function call is faster.
    public static Ex OfDFD(ExFunction f, Ex arg) => f.Of(arg.As<double>()).As<float>();
    public static Ex OfDFD(ExFunction f, params Ex[] args) => f.Of(args.Select(x => x.As<double>()).ToArray()).As<float>();

    private static Ex dGetRadIndex(TEx<double> angleRad) => Ex.And(angleRad.Mul(ExC(radRatio)).As<int>(), ExC(LookupMask));
    private static Ex dGetDegIndex(TEx<double> angleDeg) => Ex.And(angleDeg.Mul(ExC(degRatio)).As<int>(), ExC(LookupMask));
    private static Ex dLookupByIndex(Ex index) => ExC(LookupTable).Index(index);
    public static Ex dLookupCosSinRad(TEx<double> angleRad) => dLookupByIndex(dGetRadIndex(angleRad));
    public static Ex dLookupCosRad(TEx<double> angleRad) => Ex.Field(dLookupCosSinRad(angleRad), "x");
    public static Ex dLookupSinRad(TEx<double> angleRad) => Ex.Field(dLookupCosSinRad(angleRad), "y");
    public static Ex dLookupCosSinDeg(TEx<double> angleDeg) => dLookupByIndex(dGetDegIndex(angleDeg));
    public static Ex dLookupCosDeg(TEx<double> angleDeg) => Ex.Field(dLookupCosSinDeg(angleDeg), "x");
    public static Ex dLookupSinDeg(TEx<double> angleDeg) => Ex.Field(dLookupCosSinDeg(angleDeg), "y");
    
    public static readonly Ex hpi = Ex.Constant(M.HPI);
    public static readonly Ex pi = Ex.Constant(M.PI);
    public static readonly Ex npi = Ex.Constant(M.NPI);
    public static readonly Ex tau = Ex.Constant(M.TAU);
    public static readonly Ex twau = ExC(M.TWAU);
    public static readonly Ex degRad = ExC(M.degRad);
    public static readonly Ex radDeg = ExC(M.radDeg);
    public static readonly Ex phi = Ex.Constant(M.PHI);
    public static readonly Ex iphi = Ex.Constant(M.IPHI);
    public static readonly Ex iphi360 = Ex.Constant(360f * M.PHI);

    public static readonly Ex E0 = Ex.Constant(0.0f);
    public static readonly Ex E05 = Ex.Constant(0.5f);
    public static readonly Ex E025 = Ex.Constant(0.25f);
    public static readonly Ex E1 = Ex.Constant(1.0f);
    public static readonly Ex E2 = Ex.Constant(2.0f);
    public static readonly Ex EN1 = Ex.Constant(-1f);
    public static readonly Ex EN2 = Ex.Constant(-2f);
    public static readonly Ex EN05 = Ex.Constant(-0.5f);
    public static readonly Ex v20 = Ex.Constant(Vector2.zero);
    public static Ex QRotate(Ex quat, Ex v3) => Ex.Multiply(quat, v3);
    public static Expression ExC(object x) => Ex.Constant(x);

    public static BlockExpression RotateLerp(Ex target, Ex source, TExArgCtx bpi, bool isRate, bool isTrue, Ex rate) {
        if (isRate) rate = rate.Mul(M.degRad);
        if (isTrue) rate = rate.Mul(ETime.FRAME_TIME);
        TExV2 v = TExV2.Variable();
        TEx<float> ang = ExUtils.VFloat();
        Expression[] exprs = new Expression[3];
        exprs[1] = ang.Is(RadDiff(target, v));
        if (isTrue) {
            var key = bpi.Ctx.NameWithSuffix("_RotateLerpKey");
            exprs[0] = v.Is(
                Ex.Condition(FiringCtx.Contains<Vector2>(bpi, key),
                    FiringCtx.GetValue<Vector2>(bpi, key),
                    FiringCtx.SetValue<Vector2>(bpi, key, source)
                ));
            exprs[2] = 
                FiringCtx.SetValue<Vector2>(bpi, key, RotateRad(isRate ? (Ex)Limit(rate, ang) : ang.Mul(rate), v));
        } else {
            exprs[0] = v.Is(source);
            exprs[2] = RotateRad(isRate ? 
                    (Ex)Limit(bpi.t.Mul(rate), ang) :
                    ang.Mul(Min(bpi.t.Mul(rate), E1)), v);
        }
        return Ex.Block(new ParameterExpression[] {v, ang}, exprs);
    }
    public static BlockExpression LaserRotateLerp(Ex target, Ex source, TExArgCtx bpi, Ex rate) {
        var r1 = rate.Mul(ExC(ETime.FRAME_TIME));
        TExV2 v = TExV2.Variable();
        TEx<float> ang = ExUtils.VFloat();
        var dirKey = bpi.Ctx.NameWithSuffix("_LaserRotateLerpDirKey");
        var sideKey = bpi.Ctx.NameWithSuffix("_LaserRotateLerpSideKey");
        var inter_ang = HighPass(ExC(0.01f), RadDiff(target, v));
        return Ex.Block(new ParameterExpression[] {v, ang},
            Ex.Condition(
                bpi.FCtxHas<Vector2>(dirKey).And(bpi.t.GT0()),
                Ex.Block(
                    v.Is(bpi.FCtxGet<Vector2>(dirKey)),
                    ang.Is(Ex.Condition(bpi.FCtxGet<float>(sideKey).LT0(),
                        RadToNeg(inter_ang),
                        RadToPos(inter_ang)
                    )),
                    bpi.FCtxSet<Vector2>(dirKey, RotateRad(Limit(r1, ang), v))
                ),
                Ex.Block(
                    v.Is(source),
                    ang.Is(RadDiff(target, v)),
                    bpi.FCtxSet<float>(sideKey, Sign(ang)),
                    bpi.FCtxSet<Vector2>(dirKey, RotateRad(Limit(r1, ang), v))
                )
            )
        );
    }

    //See Design/Engine Math Tips for details on these two functions. They are not raw easing.
    public static Func<T, TEx<R>> Ease<T, R>(Func<tfloat, tfloat> easer, float maxTime, 
            Func<T, TEx<R>> f, Func<T, Ex> t, Func<T, Ex, T> withT)
        // x = f(g(t)), where g(t) = T e(t/T)
        => bpi => Ex.Condition(Ex.GreaterThan(t(bpi), ExC(maxTime)), f(bpi),
            f(withT(bpi, ExC(maxTime).Mul(
                    easer(t(bpi).Mul(1f/maxTime))
                ))
            ));

    public static Func<T, TEx<R>> EaseD<T, R>(Func<tfloat, tfloat> easer, float maxTime, 
        Func<T, TEx<R>> fd, Func<T, Ex> t, Func<T, Ex, T> withT) {
        var ratTime = ExUtils.VFloat();
        // x'(t) = f'(g(t)) g'(t). Where g(t) = T e(t/T): g'(t) = e'(t/T)
        return bpi => Ex.Block(new[] {ratTime},
                Ex.Assign(ratTime, Clamp01(ExC(1f / maxTime).Mul(t(bpi)))),
                Ex.Multiply(
                    DerivativeVisitor.Derivate(ratTime, E1, easer(ratTime)),
                    fd(withT(bpi, ExC(maxTime).Mul(easer(ratTime)))))
            );
    }

    public static Func<TExArgCtx, TEx<float>> SoftmaxShift<S>(Func<TExArgCtx,TEx<float>> sharpness, Func<TExArgCtx,TEx<float>> pivot, Func<TExArgCtx,TEx<float>> f1, Func<TExArgCtx,TEx<float>> f2, string pivotVar) where S: TEx, new()  =>
        PivotShift<S>(ExM.Softmax, sharpness, pivot, f1, f2, pivotVar);
    
    public static Func<TExArgCtx, TEx<float>> PivotShift<S>(Func<EEx<float>, TEx<float>[], TEx<float>> shifter, 
        Func<TExArgCtx,TEx<float>> sharpness, Func<TExArgCtx,TEx<float>> pivot, 
        Func<TExArgCtx,TEx<float>> f1, Func<TExArgCtx,TEx<float>> f2, string pivotVar) where S: TEx, new() {
        if (pivotVar == "t" || pivotVar == "p" || pivotVar == "x") {
            return t => {
                var pivotT = t.MakeCopyForType<S>(out var currEx, out var pivotEx);
                return Ex.Block(new ParameterExpression[] {pivotEx},
                    Ex.Assign(pivotEx, currEx),
                    Ex.Assign(pivotVar.Into<Func<TExArgCtx, TEx<float>>>()(pivotT), pivot(t)),
                    shifter(sharpness(t), new TEx<float>[] {f1(t), f1(pivotT).Add(f2(t).Sub(f2(pivotT)))})
                );
            };
        } else if (pivotVar[0] == Parser.SM_REF_KEY_C) {
            var let = pivotVar.Substring(1);
            return t => shifter(sharpness(t), new TEx<float>[] {
                f1(t), f2(t).Add(
                    ReflectEx.Let<float, float>(let, pivot, () => f1(t).Sub(f2(t)), t)
                )
            });
        } else throw new Exception($"{pivotVar} is not a valid pivoting target.");
    }
    public static Func<TExArgCtx, TEx<float>> LogSumShift<S>(Func<TExArgCtx, TEx<float>> sharpness, 
        Func<TExArgCtx, TEx<float>> pivot, Func<TExArgCtx, TEx<float>> f1, Func<TExArgCtx, TEx<float>> f2, 
        string pivotVar) where S : TEx, new() =>
        PivotShift<S>(ExM.Logsum, sharpness, pivot, f1, f2, pivotVar);
    
    public static Func<TExArgCtx,TEx<T>> Pivot<S, T>(Func<TExArgCtx,TEx<float>> pivot, Func<TExArgCtx,TEx<T>> f1, Func<TExArgCtx,TEx<T>> f2, Func<TExArgCtx, TEx> pivotVar) 
        where S: TEx, new() => t => {
        var pv = VFloat();
        var pivotT = t.MakeCopyForType<S>(out var currEx, out var pivotEx);
        return Ex.Block(new[] {pv},
            pv.Is(pivot(t)),
            Ex.Condition(pv.LT(pivotVar(t)), 
                Ex.Block(new ParameterExpression[] {pivotEx},
                    Ex.Assign(pivotEx, currEx),
                    Ex.Assign(pivotVar(pivotT), pv),
                    Ex.Add(f1(pivotT), Ex.Subtract(f2(t), f2(pivotT)))
                ),
                f1(t)
            )
        );
    };
}

public static class MoreExExtensions {
    public static bool TryAsConst<T>(this TEx<T> tex, out T val) => ((Ex)tex).TryAsConst(out val);
    public static Ex Is<T>(this TEx<T> tex, Ex other) => ((Ex) tex).Is(other);
    public static Ex Add<T>(this TEx<T> tex, Ex other) => ((Ex) tex).Add(other);
    public static Ex Mul<T>(this TEx<T> tex, Ex other) => ((Ex) tex).Mul(other);
    public static Ex Add<T>(this TEx<T> tex, float other) => ((Ex) tex).Add(other);
    public static Ex Sub<T>(this TEx<T> tex, float other) => ((Ex) tex).Sub(other);
    public static Ex Mul<T>(this TEx<T> tex, float other) => ((Ex) tex).Mul(other);
    public static Ex Sub<T>(this TEx<T> tex, Ex other) => ((Ex) tex).Sub(other);
    public static Ex Div<T>(this TEx<T> tex, Ex other) => ((Ex) tex).Div(other);
    public static Ex LT<T>(this TEx<T> tex, Ex than) => ((Ex) tex).LT(than);
    public static Ex LT0<T>(this TEx<T> tex) => ((Ex) tex).LT0();
    public static Ex GT<T>(this TEx<T> tex, Ex than) => ((Ex) tex).GT(than);
    public static Ex GT0<T>(this TEx<T> tex) => ((Ex) tex).GT0();
    public static Ex Field<T>(this TEx<T> tex, string field) => Ex.PropertyOrField(tex, field);
    
    public static Ex Flatten(this Ex ex, bool reduceMethod=true) => FlattenVisitor.Flatten(ex, reduceMethod);
    public static Ex Flatten(this TEx ex) => FlattenVisitor.Flatten(ex);
    public static Ex Derivate(this Ex ex, Ex x, Ex dx) => DerivativeVisitor.Derivate(x, dx, ex);
    public static string Debug(this Ex ex) => new DebugVisitor().Export(ex);
    public static string FlatDebug(this Ex ex) => ex.Flatten().Debug();
    public static Ex Linearize(this Ex ex) => new LinearizeVisitor().Visit(ex);
}

}
