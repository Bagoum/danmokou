using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Linq.Expressions;
using Danmaku;
using JetBrains.Annotations;
using Ex = System.Linq.Expressions.Expression;
using static ExUtils;
using F = DMath.FXYRepo;
using static DMath.ExM;

namespace DMath {
/// <summary>
/// Expression helpers for mathematical operations.
/// </summary>
public static class ExMHelpers {
    private const int LookupCt = 1 << 21;
    private const int LookupMask = (1 << 21) - 1;
    private const double dTAU = Math.PI * 2;
    private const double radRatio = LookupCt / dTAU;
    private const double degRatio = LookupCt / 360.0;
    private static readonly Vector2[] LookupTable = new Vector2[LookupCt];
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
    public static readonly Ex EN05 = Ex.Constant(-0.5f);
    public static readonly Ex v20 = Ex.Constant(Vector2.zero);

    private static readonly ExFunction qRotate = ExUtils.Wrap<Quaternion>("op_Multiply", 
        new[] { typeof(Quaternion), typeof(Vector3)});
    public static Ex QRotate(Ex quat, Ex v3) => qRotate.Of(quat, v3);
    public static Expression ExC(object x) => Ex.Constant(x);

    public static BlockExpression RotateLerp(Ex target, Ex source, TExPI bpi, bool isRate, bool isTrue, float rate) {
        Ex exrate = ExC(rate * (isRate ? M.degRad : 1f) * (isTrue ? ETime.FRAME_TIME : 1f));
        TExV2 v = TExV2.Variable();
        TEx<float> ang = ExUtils.VFloat();
        Expression[] exprs = new Expression[3];
        exprs[1] = ang.Is(RadDiff(target, v));
        if (isTrue) {
            Ex data = DataHoisting.GetClearableDictV2();
            exprs[0] = v.Is(ExUtils.DictIfExistsGetElseSet<uint, Vector2>(data, bpi.id, source));
            exprs[2] = data.DictSet(bpi.id, RotateRad(isRate ? (Ex)Limit(exrate, ang) : ang.Mul(exrate), v));
        } else {
            exprs[0] = v.Is(source);
            exprs[2] = RotateRad(isRate ? 
                    (Ex)Limit(bpi.t.Mul(exrate), ang) :
                    ang.Mul(Min(bpi.t.Mul(exrate), E1)), v);
        }
        return Ex.Block(new ParameterExpression[] {v, ang}, exprs);
    }

    //See Design/Engine Math Tips for details on these two functions. They are not raw easing.
    public static Func<TExPI, TEx<R>> Ease<R>(string name, float maxTime, Func<TExPI, TEx<R>> f) =>
        Ease<TExPI,R>(name, maxTime, f, bpi => bpi.t, (bpi, t) => bpi.CopyWithT(t));
    public static Func<T, TEx<R>> Ease<T, R>(string name, float maxTime, Func<T, TEx<R>> f, Func<T, Ex> t, Func<T, Ex, T> withT) {
        // x = f(g(t)), where g(t) = T e(t/T)
        return bpi => Ex.Condition(Ex.GreaterThan(t(bpi), ExC(maxTime)), f(bpi),
            f(withT(bpi, ExC(maxTime).Mul(
                    EaseHelpers.GetFunc(name)(t(bpi).Mul(1f/maxTime))
                ))
            ));
    }

    public static Func<TExPI, TEx<R>> EaseD<R>(string name, float maxTime, Func<TExPI, TEx<R>> fd) =>
        EaseD<TExPI,R>(name, maxTime, fd, bpi => bpi.t, (bpi, t) => bpi.CopyWithT(t));
    public static Func<T, TEx<R>> EaseD<T, R>(string name, float maxTime, Func<T, TEx<R>> fd, Func<T, Ex> t, Func<T, Ex, T> withT) {
        var ratTime = ExUtils.VFloat();
        // x'(t) = f'(g(t)) g'(t). Where g(t) = T e(t/T): g'(t) = e'(t/T)
        return bpi => Ex.Block(new[] {ratTime},
                Ex.Assign(ratTime, Clamp01(ExC(1f / maxTime).Mul(t(bpi)))),
                Ex.Multiply(
                    EaseHelpers.GetDeriv(name)(ratTime),
                    fd(withT(bpi, ExC(maxTime).Mul(EaseHelpers.GetFunc(name)(ratTime)))))
            );
    }

    public static Func<S, TEx<float>> SoftmaxShift<S>(Func<S,TEx<float>> sharpness, Func<S,TEx<float>> pivot, Func<S,TEx<float>> f1, Func<S,TEx<float>> f2, string pivotVar) where S: TEx, new()  =>
        PivotShift(ExM.Softmax, sharpness, pivot, f1, f2, pivotVar);
    
    public static Func<S, TEx<float>> PivotShift<S>(Func<EEx<float>, TEx<float>[], TEx<float>> shifter, 
        Func<S,TEx<float>> sharpness, Func<S,TEx<float>> pivot, 
        Func<S,TEx<float>> f1, Func<S,TEx<float>> f2, string pivotVar) where S: TEx, new() {
        if (pivotVar == "t" || pivotVar == "p" || pivotVar == "x") {
            var tpv = new S();
            return t => Ex.Block(new ParameterExpression[] {tpv},
                Ex.Assign(tpv, t),
                Ex.Assign(pivotVar.Into<Func<S, TEx<float>>>()(tpv), pivot(t)),
                shifter(sharpness(t), new TEx<float>[] { f1(t), f1(tpv).Add(f2(t).Sub(f2(tpv))) })
            );
        } else if (pivotVar[0] == Parser.SM_REF_KEY_C) {
            var let = pivotVar.Substring(1);
            return t => shifter(sharpness(t), new TEx<float>[] {
                f1(t), f2(t).Add(
                    ReflectEx.Let<S, float, float>(let, pivot, () => f1(t).Sub(f2(t)), t)
                )
            });
        } else throw new Exception($"{pivotVar} is not a valid pivoting target.");
    }
    public static Func<S, TEx<float>> LogSumShift<S>(Func<S, TEx<float>> sharpness, Func<S, TEx<float>> pivot,
        Func<S, TEx<float>> f1, Func<S, TEx<float>> f2, string pivotVar) where S : TEx, new() =>
        PivotShift(ExM.Logsum, sharpness, pivot, f1, f2, pivotVar);
    
    public static Func<S,TEx<T>> Pivot<S,T>(Func<S,TEx<float>> pivot, Func<S,TEx<T>> f1, Func<S,TEx<T>> f2, Func<S, TEx> pivotVar) 
        where S: TEx, new() => t => {
        var pv = VFloat();
        var cold = new S();
        return Ex.Block(new[] {pv},
            pv.Is(pivot(t)),
            Ex.Condition(pv.LT(pivotVar(t)), 
                Ex.Block(new ParameterExpression[] {cold},
                    Ex.Assign(cold, t),
                    Ex.Assign(pivotVar(cold), pv),
                    Ex.Add(f1(cold), Ex.Subtract(f2(t), f2(cold)))
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
    public static Ex Sub<T>(this TEx<T> tex, Ex other) => ((Ex) tex).Sub(other);
    public static Ex Div<T>(this TEx<T> tex, Ex other) => ((Ex) tex).Div(other);
    public static Ex LT<T>(this TEx<T> tex, Ex than) => ((Ex) tex).LT(than);
    public static Ex LT0<T>(this TEx<T> tex) => ((Ex) tex).LT0();
    public static Ex GT<T>(this TEx<T> tex, Ex than) => ((Ex) tex).GT(than);
    public static Ex GT0<T>(this TEx<T> tex) => ((Ex) tex).GT0();
    
    public static Ex Flatten(this Ex ex) => FlattenVisitor.Flatten(ex);
    public static Ex Flatten(this TEx ex) => FlattenVisitor.Flatten(ex);
    public static string Debug(this Ex ex) => new DebugVisitor().Export(ex);
    public static string FlatDebug(this Ex ex) => ex.Flatten().Debug();
}

public class EEx {
    protected readonly Ex ex;
    protected readonly bool requiresCopy;
    public EEx(Ex ex, bool requiresCopy) {
        this.ex = ex;
        this.requiresCopy = requiresCopy;
    }
    //Remove this in favor of subtype
    public static implicit operator EEx(Ex ex) => new EEx(ex, 
        ex.NodeType != ExpressionType.Parameter && 
        ex.NodeType != ExpressionType.Constant &&
        ex.NodeType != ExpressionType.MemberAccess);
    public static implicit operator Ex(EEx ex) => ex.ex;
    public static implicit operator (Ex, bool)(EEx exx) => (exx.ex, exx.requiresCopy);
    private static Ex ResolveCopy(Func<Ex[], Ex> func, params (Ex, bool)[] requiresCopy) {
        var newvars = ListCache<ParameterExpression>.Get();
        var setters = ListCache<Expression>.Get();
        var usevars = new Expression[requiresCopy.Length];
        for (int ii = 0; ii < requiresCopy.Length; ++ii) {
            var (ex, reqCopy) = requiresCopy[ii];
            if (reqCopy) {
                var copy = V(ex.Type);
                usevars[ii] = copy;
                newvars.Add(copy);
                setters.Add(copy.Is(ex));
            } else {
                usevars[ii] = ex;
            }
        }
        setters.Add(func(usevars));
        var block = Ex.Block(newvars, setters);
        ListCache<ParameterExpression>.Consign(newvars);
        ListCache<Expression>.Consign(setters);
        return setters.Count > 1 ? func(usevars) : block;
    }
    public static Ex Resolve<T1>(EEx<T1> t1, Func<TEx<T1>, Ex> resolver) =>
        ResolveCopy(x => resolver(x[0]), t1);
    public static Ex ResolveV2(EEx<Vector2> t1, Func<TExV2, Ex> resolver) =>
        ResolveCopy(x => resolver(new TExV2(x[0])), t1);
    public static Ex ResolveV3(EEx<Vector3> t1, Func<TExV3, Ex> resolver) =>
        ResolveCopy(x => resolver(new TExV3(x[0])), t1);
    public static Ex Resolve<T1,T2>(EEx<T1> t1, EEx<T2> t2, Func<TEx<T1>, TEx<T2>, Ex> resolver) =>
        ResolveCopy(x => resolver(x[0], x[1]), t1, t2);
    public static Ex ResolveV2(EEx<Vector2> t1, EEx<Vector2> t2, 
        Func<TExV2, TExV2, Ex> resolver) =>
        ResolveCopy(x => resolver(new TExV2(x[0]), new TExV2(x[1])), t1, t2);
    public static Ex ResolveV3(EEx<Vector3> t1, EEx<Vector3> t2, 
        Func<TExV3, TExV3, Ex> resolver) =>
        ResolveCopy(x => resolver(new TExV3(x[0]), new TExV3(x[1])), t1, t2);
    public static Ex Resolve<T1,T2,T3>(EEx<T1> t1, EEx<T2> t2, EEx<T3> t3, 
        Func<TEx<T1>, TEx<T2>, TEx<T3>, Ex> resolver) =>
        ResolveCopy(x => resolver(x[0], x[1], x[2]), t1, t2, t3);
    public static Ex ResolveV2(EEx<Vector2> t1, EEx<Vector2> t2, EEx<Vector2> t3, 
        Func<TExV2, TExV2, TExV2, Ex> resolver) =>
        ResolveCopy(x => resolver(new TExV2(x[0]), new TExV2(x[1]), new TExV2(x[2])), t1, t2, t3);
    public static Ex Resolve<T1,T2,T3,T4>(EEx<T1> t1, EEx<T2> t2, EEx<T3> t3, EEx<T4> t4, 
        Func<TEx<T1>, TEx<T2>, TEx<T3>, TEx<T4>, Ex> resolver) =>
        ResolveCopy(x => resolver(x[0], x[1], x[2], x[3]), t1, t2, t3, t4);
    public static Ex Resolve<T1,T2,T3,T4,T5>(EEx<T1> t1, EEx<T2> t2, EEx<T3> t3, EEx<T4> t4, EEx<T5> t5, 
        Func<TEx<T1>, TEx<T2>, TEx<T3>, TEx<T4>, TEx<T5>, Ex> resolver) =>
        ResolveCopy(x => resolver(x[0], x[1], x[2], x[3], x[4]), t1, t2, t3, t4, t5);
}

public class EEx<T> : EEx {
    public EEx(Ex ex, bool requiresCopy) : base(ex, requiresCopy) { }

    public static implicit operator EEx<T>(TEx<T> ex) => (Ex) ex;
    public static implicit operator EEx<T>(Ex ex) => new EEx<T>(ex, 
        ex.NodeType != ExpressionType.Parameter && 
        ex.NodeType != ExpressionType.Constant &&
        ex.NodeType != ExpressionType.MemberAccess);
    public static implicit operator Ex(EEx<T> ex) => ex.ex;
    public static implicit operator (Ex, bool)(EEx<T> exx) => (exx.ex, exx.requiresCopy);
}

}
