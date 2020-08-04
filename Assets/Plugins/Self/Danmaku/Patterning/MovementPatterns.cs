using System;
using System.Linq;
using System.Linq.Expressions;
using Ex = System.Linq.Expressions.Expression;
using ExFXY = System.Func<TEx<float>, TEx<float>>;
using ExBPY = System.Func<DMath.TExPI, TEx<float>>;
using ExTP = System.Func<DMath.TExPI, TEx<UnityEngine.Vector2>>;
using ExBPRV2 = System.Func<DMath.TExPI, TEx<DMath.V2RV2>>;
using static DMath.BPYRepo;
using GCP = Danmaku.GenCtxProperty;
using ExSBF = System.Func<Danmaku.TExSB, TEx<float>>;
using ExSBV2 = System.Func<Danmaku.TExSB, TEx<UnityEngine.Vector2>>;
using ExVTP = System.Func<Danmaku.ITExVelocity, TEx<float>, DMath.TExPI, DMath.RTExV2, TEx<UnityEngine.Vector2>>;
using static Danmaku.Enums;
using DMath;
using JetBrains.Annotations;
using UnityEngine;
using SP = Danmaku.SyncPatterns;
using TPr = DMath.Parametrics;
using RV2r = DMath.BPRV2Repo;
using R = Reflector;
using static Reflector;
using static Compilers;
using static DMath.ExM;
using static DMath.ExMHelpers;
using static Danmaku.AtomicPatterns;
using static Danmaku.PatternUtils;
using static Danmaku.GenCtxProperty;
using static Danmaku.VTPRepo;
using static DMath.Parametrics;
using Danmaku;

namespace DMath {

public static partial class Parametrics {
    public static ExTP goX(ExBPY x) => PXY(x, Y());
    public static ExTP goY(ExBPY y) => PXY(X(), y);
    public static ExTP goUp() => goY(_ => ExC(6f));
    public static ExTP goDown() => goY(_ => ExC(-6f));
    public static ExTP goLeft() => goX(_ => ExC(-7f));
    public static ExTP goRight() => goX(_ => ExC(7f));
    public static ExTP goSide(ExBPY y) => PXY(b => Mul(ExC(7f), Sign(X()(b))), y);

}
public static class MovementPatterns {
    private static ExBPY f(float f) => _ => ExC(f);
    public static RootedVTP Null(GCXF<Vector2> root) => new RootedVTP(root, VTPRepo.Null());
    public static RootedVTP RVTP(GCXF<Vector2> root, GCXU<VTP> path) => new RootedVTP(root, path);
    
    public static RootedVTP LR(GCXF<float> lr, RootedVTP gt0, RootedVTP lt0) {
        return new RootedVTP(gcx => lr(gcx) > 0 ? gt0.root(gcx) : lt0.root(gcx), new GCXU<VTP>(
            (GenCtx gcx, ref uint id) =>
                lr(gcx) > 0 ? gt0.path.New(gcx, ref id) : lt0.path.New(gcx, ref id),
            (gcx, id) => lr(gcx) > 0 ? gt0.path.Add(gcx, id) : lt0.path.Add(gcx, id)
        ));
    }

    private static ExBPY ModTime(ExBPY letValue, string withLet) =>
        LetFloats(new[] {
            ("t", letValue)
        }, withLet.Into<ExBPY>());

    public static ExBPY th1(float pivot, ExBPY target) => ModTime(target,
        $"lsshat 2 {pivot} * 0.5 &t &t");
    public static ExBPY t21(float pivot, ExBPY target) => ModTime(target,
        $"lsshat -3 {pivot} * 2 &t &t");

    private static ExVTP SetupTime(ExBPY time, ExVTP inner) =>
        LetFloats(new[] {
            ("t", time)
        }, inner);

    private static ExVTP SetupTime(ExBPY time, string inner) => SetupTime(time, inner.Into<ExVTP>());
    
    private static RootedVTP Setup(float x, float y, ExBPY time, string path) => 
        new RootedVTP(x, y, SetupTime(time, path));
    
    //Standard model for stage enemy paths: Fixed root, fixed path,
    // time along path (as parametric input) is controllable as a separate parameter.
    //Note: use ^^ instead of ^ in order to avoid NaN errors when logsumshift pushes t negative (this can happen quite commonly).
    public static RootedVTP DipUpL(ExBPY time) => Setup(-6, 2, time,
        "nroffset pxy &t - (^^ &t 1.25) (* 1.7 &t)");
    public static RootedVTP DipUpR(ExBPY time) => Setup(6, 2, time,
        "nroffset pxy neg &t - (^^ &t 1.25) (* 1.7 &t)");
    public static RootedVTP DipUp2L(ExBPY time) => Setup(-6, 0.5f, time,
        "nroffset pxy &t * (if > &t 5 0.2 0.02) (sqr - &t 5)");
    public static RootedVTP DipUp2R(ExBPY time) => Setup(6, 0.5f, time,
        "nroffset pxy neg &t * (if > &t 5 0.2 0.02) (sqr - &t 5)");

    private const string CrossUpSY = "* 0.11 ^^ &t 1.2";
    private const string Inv = "neg";
    public static RootedVTP CrossSL(float x, float y, string xn, string yn, ExBPY time) => 
        Setup(x, y, time, $"nroffset pxy {xn} &t {yn} {CrossUpSY}");

    public static RootedVTP CrossUpL(ExBPY time) => CrossSL(-6, 1, "", "", time);
    public static RootedVTP CrossUpR(ExBPY time) => CrossSL(6, 1, Inv, "", time);
    public static RootedVTP CrossDownL(ExBPY time) => CrossSL(-6, 3, "", Inv, time);
    public static RootedVTP CrossDownR(ExBPY time) => CrossSL(6, 3, Inv, Inv, time);
    public static RootedVTP CrossUp2L(ExBPY time) => Setup(-7, -5, time,
        $"nroffset pxy * 1.4 &t &t");
    public static RootedVTP CrossUp2R(ExBPY time) => Setup(7, -5, time,
        $"nroffset pxy * -1.4 &t &t");
    public static RootedVTP CrossDown2L(ExBPY time) => Setup(-7, 5, time,
        $"nroffset pxy * 1.4 &t neg &t");
    public static RootedVTP CrossDown2R(ExBPY time) => Setup(7, 5, time,
        $"nroffset pxy * -1.4 &t neg &t");
    
    
    public static RootedVTP CircDownL(ExBPY time) => Setup(-0.2f, 3.8f, time,
        "nroffset pxy (* -2.8 sin + 1.4 &t) (if > &t 5) * 2 t (* 2.4 cos + 1.2 &t)");
    public static RootedVTP CircDownR(ExBPY time) => Setup(0.2f, 3.8f, time,
        "nroffset pxy (* 2.8 sin + 1.4 &t) (if > &t 5) * 2 t (* 2.4 cos + 1.2 &t)");
    
    public static RootedVTP CircDown2L(ExBPY time) => Setup(-1f, 3f, time,
        "nroffset pxy (* -5 sin + 1.7 damp pi 0.7 &t) (if > &t 5) * 2 t (* 3 cos + 1.2 * 0.96 &t)");
    public static RootedVTP CircDown2R(ExBPY time) => Setup(1f, 3f, time,
        "nroffset pxy (* 5 sin + 1.7 damp pi 0.7 &t) (if > &t 5) * 2 t (* 3 cos + 1.2 * 0.96 &t)");

    public static RootedVTP BendDownHL(ExBPY time) => Setup(-4.5f, 5, time,
        $"nroffset pxy (logsumshift &t 5 2 (* 0.5 &t) &t) " +
        $"(logsumshift &t 5 2 * -1.2 &t (* -0.13 ^^ &t 0.6))");
    public static RootedVTP BendDownHR(ExBPY time) => Setup(4.5f, 5, time,
        $"nroffset pxy (neg logsumshift &t 5 2 (* 0.5 &t) &t) " +
        $"(logsumshift &t 5 2 * -1.2 &t (* -0.13 ^^ &t 0.6))");
    public static RootedVTP BendUpL(ExBPY time) => Setup(-5.5f, 3f, time,
        "nroffset pxy exp * 0.12 &t * -0.8 exp - 2.4 * 0.2 &t");
    public static RootedVTP BendUpR(ExBPY time) => Setup(5.5f, 3f, time,
        "nroffset pxy neg exp * 0.12 &t * -0.8 exp - 2.4 * 0.2 &t");

    public static RootedVTP CornerL(ExBPY time) => Setup(-6f, 0, time,
        $"nroffset pxy (lsshat -2 1 * 3 &t * 0.5 &t) " +
        $"(lsshat 2 1 * 0.5 &t * 3 &t)");
    public static RootedVTP CornerR(ExBPY time) => Setup(6f, 0, time,
        $"nroffset pxy neg (lsshat -2 1 * 3 &t * 0.5 &t) " +
        $"(lsshat 2 1 * 0.5 &t * 3 &t)");
    
    
    public static RootedVTP Up(ExBPY time) => Setup(0, -5, time, "nroffset py &t");
    public static RootedVTP Down(ExBPY time) => Setup(0, 5, time, "nroffset py neg &t");
    
    public static RootedVTP AcrossL(ExBPY time) => Setup(-6, 0, time, "nroffset px &t");
    public static RootedVTP AcrossR(ExBPY time) => Setup(6, 0, time, "nroffset px neg &t");
}

}