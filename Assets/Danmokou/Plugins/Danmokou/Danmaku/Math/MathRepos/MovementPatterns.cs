using System;
using System.Linq;
using System.Linq.Expressions;
using BagoumLib.Expressions;
using Danmokou.Core;
using Ex = System.Linq.Expressions.Expression;
using static Danmokou.DMath.Functions.BPYRepo;
using GCP = Danmokou.Danmaku.Options.GenCtxProperty;
using UnityEngine;
using RV2r = Danmokou.DMath.Functions.BPRV2Repo;
using R = Danmokou.Reflection.Reflector;
using static Danmokou.DMath.Functions.ExM;
using static Danmokou.Expressions.ExMHelpers;
using static Danmokou.DMath.Functions.VTPRepo;
using static Danmokou.DMath.Functions.Parametrics;
using static Danmokou.DMath.Functions.ExMPred;
using static Danmokou.DMath.LocationHelpers;
using static Danmokou.DMath.Functions.ExMConditionals;
using static Danmokou.DMath.Functions.ExMLerps;
using static Danmokou.DMath.Functions.ExMConversions;
using static Danmokou.Reflection.Compilers;
using Danmokou.Expressions;
using Danmokou.Reflection;
using ExVTP = System.Func<Danmokou.Expressions.ITexMovement, Danmokou.Expressions.TEx<float>, Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TExV2, Danmokou.Expressions.TEx>;
using ExBPY = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<float>>;
using ExPred = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<bool>>;
using ExTP = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<UnityEngine.Vector2>>;
using ExTP3 = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<UnityEngine.Vector3>>;

namespace Danmokou.DMath.Functions {

public static partial class Parametrics {
    public static ExTP goX(ExBPY x) => PXY(x, Y());
    public static ExTP goY(ExBPY y) => PXY(X(), y);
    public static ExTP goUp() => goY(_ => ExC(6f));
    public static ExTP goDown() => goY(_ => ExC(-6f));
    public static ExTP goLeft() => goX(_ => ExC(-5f));
    public static ExTP goRight() => goX(_ => ExC(5f));
    public static ExTP goSide(ExBPY y) => PXY(b => Mul(ExC(5f), Sign(X()(b))), y);
    public static ExTP goOtherSide(ExBPY y) => PXY(b => Mul(ExC(-5f), Sign(X()(b))), y);

}
[Reflect]
public static class MovementPatterns {
    private static ExBPY f(float f) => _ => ExC(f);
    public static RootedVTP Null(GCXF<Vector2> root) => new RootedVTP(root, VTPRepo.Null());
    public static RootedVTP RVTP(GCXF<Vector2> root, GCXU<VTP> path) => new RootedVTP(root, path);

    private static ExBPY ModTime(ExBPY letValue, string withLet) =>
        LetFloats(new[] {
            ("t", letValue)
        }, withLet.Into<ExBPY>());

    public static ExBPY th1(float pivot, ExBPY target) => ModTime(target,
        $"lsshat(2, {pivot}, 0.5 * &t, &t)");
    public static ExBPY t21(float pivot, ExBPY target) => ModTime(target,
        $"lsshat(-3, {pivot}, 2 * &t, &t)");

    private static ExVTP SetupTime(ExBPY time, ExVTP inner, bool reqDeriv = false) =>
        LetFloats(reqDeriv ? new[] {
            ("t", time),
            ("dt", b => ((Ex)time(b)).Derivate(b.t, E1))
        } : new[] {
            ("t", time)
        }, inner);

    private static ExVTP SetupTime(ExBPY time, string inner, bool reqDeriv) => SetupTime(time, inner.Into<ExVTP>(), reqDeriv);
    
    private static RootedVTP Setup(float x, float y, ExBPY time, string path, bool reqDeriv=false) => 
        new RootedVTP(x, y, SetupTime(time, path, reqDeriv));
    
    private static RootedVTP Setup(ExBPY x, ExBPY y, ExBPY time, ExVTP path, bool reqDeriv=false) =>
        new RootedVTP(PXY(x, y), SetupTime(time, path, reqDeriv));
    private static RootedVTP Setup(ExBPY x, float y, ExBPY time, ExVTP path, bool reqDeriv=false) =>
        new RootedVTP(PXY(x, _ => y), SetupTime(time, path, reqDeriv));

    //Standard model for stage enemy paths: Fixed root, fixed path,
    // time along path (as parametric input) is controllable as a separate parameter.
    //Note: use ^^ instead of ^ in order to avoid NaN errors when logsumshift pushes t negative (this can happen quite commonly).
    public static RootedVTP DipUp1(ExBPY xmul, ExBPY time) => new RootedVTP(b => xmul(b).Mul(LeftMinus1), b => 1.5f, 
        SetupTime(time, NROffset(PXY(
                b => xmul(b).Mul(t(b)),
                b => Pow(t(b), 1.4f).Sub(t(b).Mul(1.9f))
            ))));
    
    public static RootedVTP DipUp2(ExBPY xmul, ExBPY time) => new RootedVTP(b => xmul(b).Mul(LeftMinus1), b => 0.5f, 
        SetupTime(time, NROffset(PXY(
            b => xmul(b).Mul(t(b)),
            b => If<float>(Gt(t(b), 4), 0.4f, 0.02f).Mul(Pow(t(b).Add(-4f), 2f))
        ))));
    
    public static RootedVTP DipUp3(ExBPY xmul, ExBPY time) => new RootedVTP(b => xmul(b).Mul(LeftMinus1), b => 0.5f, 
        SetupTime(time, NROffset(PXY(
            b_ => xmul(b_).Mul(LogSumShift<TExPI>(_ => -1, _ => 2f, b => t(b).Mul(3f), b => t(b).Mul(0.2f), "&t")(b_)),
            b_ => LogSumShift<TExPI>(_ => 2, _ => 1.9f, _ => 0, b => t(b).Mul(2.7f), "&t")(b_)
        ))));

    public static RootedVTP Cross1(GCXF<float> x, GCXF<float> y, ExBPY xmul, ExBPY ymul, ExBPY time) =>
        new RootedVTP(gcx => new Vector2(x(gcx), y(gcx)), SetupTime(time, NROffset(PXY(
            b => xmul(b).Mul(t(b)),
            b => ymul(b).Mul(t(b))
        )), false));

    private static readonly ExBPY b1 = _ => Ex.Constant(1f);
    private static readonly ExBPY bn1 = _ => Ex.Constant(-1f);

    public static RootedVTP CrossUp(ExBPY xmul, ExBPY time) => 
        Cross1(GCXF<float>(b => xmul(b).Mul(LeftMinus1)), _ => 1f, xmul, _ => ExC(0.12f), time);
    public static RootedVTP CrossDown(ExBPY xmul, ExBPY time) => 
        Cross1(GCXF<float>(b => xmul(b).Mul(LeftMinus1)), _ => 3f, xmul, _ => ExC(-0.12f), time);
    public static RootedVTP CrossUp2(ExBPY xmul, ExBPY time) =>
        Cross1(GCXF<float>(b => xmul(b).Mul(LeftMinus1)), _ => -4.5f, xmul, b1, time);
    public static RootedVTP CrossDown2(ExBPY xmul, ExBPY time) =>
        Cross1(GCXF<float>(b => xmul(b).Mul(LeftMinus1)), _ => 5f, xmul, bn1, time);

    //WARNING/TODO: velocity-based RootedVTP needs to multiply by the derivative of the time function.
    // This means you are restricted to only using fairly rudimentary time functions.
    public static RootedVTP CrossDownLoopBack(ExBPY xmul, ExBPY time) => Setup(b => xmul(b).Mul(-4.6f), 4f, time,
        NRVelocity(MultiplyX(xmul,
                b => RotateLerpCCW(2f, 3.5f, t(b), CXY(2.7f, -1.4f)(b), CXY(-2f, 0.3f)(b))
            .Mul(Lerp<float>(1.5f, 3.5f, t(b), 1f, 0.7f)).Mul(dt(b)))), true);

    private static ExBPY t => Reference<float>("t");
    private static ExBPY dt => Reference<float>("dt");

    public static RootedVTP CircDown(ExBPY xmul, ExBPY time) => Setup(b => xmul(b).Mul(-0.2f), 3.8f, time,
        NROffset(PXY(b => xmul(b).Mul(-2.8f).Mul(Sin(t(b).Add(1.4f))),
            b => If<float>(Gt(t(b), 5), t(b).Mul(2), Cos(t(b).Add(1.2f)).Mul(2.4f)))));
    //"nroffset pxy(-2.8 * sin(1.4 + &t), if(> &t 5, 2 * t, 2.4 * cos(1.2 + &t)))"

    public static RootedVTP CornerLoop(ExBPY xmul, ExBPY time) => Setup(b => xmul(b).Mul(-4.6f), 5f, time,
        NRVelocity(MultiplyX(xmul, b => PolarToXY(
            dt(b).Mul(3f),
            Lerp<float>(1.4f, 5f, t(b), -35f, -670f)
        ))), true
    );
    
    public static RootedVTP CircDown2L(ExBPY time) => Setup(-1f, 3f, time,
        "nroffset pxy (-5 * sin(1.7 + damp(pi, 0.7, &t)), if(> &t 5, 2 * t, 3 * cos(1.2 + 0.96 * &t)))");
    public static RootedVTP CircDown2R(ExBPY time) => Setup(1f, 3f, time,
        "nroffset pxy (5 * sin(1.7 + damp(pi, 0.7, &t)), if(> &t 5, 2 * t, 3 * cos(1.2 + 0.96 * &t)))");

    public static RootedVTP BendDownHL(ExBPY time) => Setup(-3, 5, time,
        $"nroffset pxy(logsumshift(&t, 5, 2, 0.5 * &t, &t),  " +
        $"logsumshift(&t, 5, 2, -1.2 * &t, -0.13 * ^^ &t 0.6))");
    public static RootedVTP BendDownHR(ExBPY time) => Setup(3, 5, time,
        $"nroffset pxy(neg logsumshift(&t, 5, 2, 0.5 * &t, &t),  " +
        $"logsumshift(&t, 5, 2, -1.2 * &t, -0.13 * ^^ &t 0.6))");
    public static RootedVTP BendUpL(ExBPY time) => Setup(LeftMinus1, 3f, time,
        "nroffset pxy(exp(0.12 * &t), -0.8 * exp(2.4 - 0.2 * &t))");
    public static RootedVTP BendUpR(ExBPY time) => Setup(RightPlus1, 3f, time,
        "nroffset pxy(neg exp(0.12 * &t), -0.8 * exp(2.4 - 0.2 * &t))");

    public static RootedVTP CornerL(ExBPY time) => Setup(LeftMinus1, 0, time,
        $"nroffset pxy(lsshat(-2, 1, 3 * &t, 0.5 * &t), " +
        $"lsshat(2, 1, 0.5 * &t, 3 * &t))");
    public static RootedVTP CornerR(ExBPY time) => Setup(RightPlus1, 0, time,
        $"nroffset pxy(neg lsshat(-2, 1, 3 * &t, 0.5 * &t), " +
        $"lsshat(2, 1, 0.5 * &t, 3 * &t))");
    
    
    public static RootedVTP Up(ExBPY time) => Setup(0, -5, time, "nroffset(py(&t))");
    public static RootedVTP Down(ExBPY time) => Setup(0, 5, time, "nroffset(py(neg(&t)))");
    
    public static RootedVTP AcrossL(ExBPY time) => Setup(LeftMinus1, 0, time, "nroffset(px(&t))");
    public static RootedVTP AcrossR(ExBPY time) => Setup(RightPlus1, 0, time, "nroffset(px(neg(&t)))");
}

}