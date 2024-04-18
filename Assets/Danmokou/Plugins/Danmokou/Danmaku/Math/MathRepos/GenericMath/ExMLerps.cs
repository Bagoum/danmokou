using System;
using System.Linq;
using System.Linq.Expressions;
using BagoumLib.Expressions;
using BagoumLib.Unification;
using Danmokou.Core;
using Danmokou.Expressions;
using Danmokou.Reflection;
using Danmokou.Reflection2;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;
using static Danmokou.Expressions.ExUtils;
using static Danmokou.Expressions.ExMHelpers;
using tfloat = Danmokou.Expressions.TEx<float>;
using tbool = Danmokou.Expressions.TEx<bool>;
using tv2 = Danmokou.Expressions.TEx<UnityEngine.Vector2>;
using tv3 = Danmokou.Expressions.TEx<UnityEngine.Vector3>;
using trv2 = Danmokou.Expressions.TEx<Danmokou.DMath.V2RV2>;
using static Danmokou.DMath.Functions.ExM;
using static Danmokou.DMath.Functions.ExMConditionals;
using static Danmokou.DMath.Functions.ExMConversions;
using static Danmokou.DMath.Functions.ExMMod;
using ExBPY = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<float>>;
using static Danmokou.Reflection.Compilers;

namespace Danmokou.DMath.Functions {
/// <summary>
/// See <see cref="Danmokou.DMath.Functions.ExM"/>. This class contains functions related to lerping and smoothing.
/// </summary>
[Reflect]
public static class ExMLerps {
    /// <summary>
    /// Get the value t such that LerpUnclamped(a, b, t) = x.
    /// Do not use if a = b.
    /// </summary>
    /// <param name="a">Lower lerp bound</param>
    /// <param name="b">Upper lerp bound</param>
    /// <param name="x">Resulting lerp value</param>
    /// <returns></returns>
    public static tfloat Ratio(tfloat a, tfloat b, tfloat x) => TEx.Resolve(a, _a => x.Sub(a).Div(b.Sub(a)));
    
    /// <summary>
    /// Lerp between two functions.
    /// <br/>Note: Unless marked otherwise, all lerp functions clamp the controller.
    /// </summary>
    /// <param name="zeroBound">Lower bound for lerp controller</param>
    /// <param name="oneBound">Upper bound for lerp controller</param>
    /// <param name="controller">Lerp controller</param>
    /// <param name="f1">First function (when controller leq zeroBound, return this)</param>
    /// <param name="f2">Second function (when controller geq oneBound, return this)</param>
    /// <returns></returns>
    public static TEx<T> Lerp<T>(tfloat zeroBound, tfloat oneBound, tfloat controller, TEx<T> f1, TEx<T> f2) => 
        TEx.Resolve(zeroBound, oneBound, controller, (z, o, c) => {
            var rc = VFloat("ratio");
            return Ex.Block(new[] {rc},
                rc.Is(Clamp(z, o, c).Sub(z).Div(o.Sub(z))),
                rc.Mul(f2).Add(rc.Complement().Mul(f1))
            );
        });

    /// <summary>
    /// Lerp between a value for easy difficulty and lunatic difficulty.
    /// </summary>
    public static TEx<T> LerpD<T>(TEx<T> f1, TEx<T> f2) => Lerp(FixedDifficulty.Easy.Counter(),
        FixedDifficulty.Lunatic.Counter(), ExMDifficulty.Dc(), f1, f2);
    /// <summary>
    /// Lerp between a value for minimum rank and maximum rank.
    /// </summary>
    public static TEx<T> LerpR<T>(TEx<T> f1, TEx<T> f2) => Lerp(ExMDifficulty.MinRank(),
        ExMDifficulty.MaxRank(), ExMDifficulty.Rank(), f1, f2);

    /// <summary>
    /// Lerp between two functions with 0-1 as the bounds for the controller.
    /// </summary>
    public static TEx<T> Lerp01<T>(tfloat controller, TEx<T> f1, TEx<T> f2) =>
        TEx.Resolve<float>(Clamp01(controller), c => c.Mul(f2).Add(((Ex)c).Complement().Mul(f1)));
    
    /// <summary>
    /// Lerp between two functions with smoothing applied to the controller.
    /// </summary>
    public static TEx<T> LerpSmooth<T>([LookupMethod] TEx<Func<float, float>> smoother, 
        tfloat zeroBound, tfloat oneBound, tfloat controller, TEx<T> f1, TEx<T> f2) 
        => TEx.Resolve(zeroBound, oneBound, controller, (z, o, c) => {
            var rc = VFloat("ratio");
            return Ex.Block(new[] {rc},
                rc.Is(PartialFn.Execute(smoother, Clamp(z, o, c).Sub(z).Div(o.Sub(z)))),
                rc.Mul(f2).Add(rc.Complement().Mul(f1))
            );
        });
    
    /// <summary>
    /// Lerp between two functions. The controller is not clamped.
    /// </summary>
    /// <param name="zeroBound">Lower bound for lerp controller</param>
    /// <param name="oneBound">Upper bound for lerp controller</param>
    /// <param name="controller">Lerp controller</param>
    /// <param name="f1">First function</param>
    /// <param name="f2">Second function</param>
    /// <returns></returns>
    public static TEx<T> LerpU<T>(tfloat zeroBound, tfloat oneBound, tfloat controller, TEx<T> f1, TEx<T> f2) => 
        TEx.Resolve(zeroBound, oneBound, controller, (z, o, c) => {
            var rc = VFloat("uratio");
            return Ex.Block(new[] {rc},
                rc.Is(c.Sub(z).Div(o.Sub(z))),
                rc.Mul(f2).Add(rc.Complement().Mul(f1))
            );
        });
    
    /// <summary>
    /// Lerp between two functions with 0-1 as the bounds for the controller. The controller is not clamped.
    /// </summary>
    public static TEx<T> Lerp01U<T>(tfloat controller, TEx<T> f1, TEx<T> f2) =>
        TEx.Resolve(controller, c => c.Mul(f2).Add(((Ex)c).Complement().Mul(f1)));
    
    /// <summary>
    /// Lerp between three functions.
    /// Between zeroBound and oneBound, lerp from the first to the second.
    /// Between zeroBound2 and oneBound2, lerp from the second to the third.
    /// </summary>
    public static TEx<T> Lerp3<T>(tfloat zeroBound, tfloat oneBound,
        tfloat zeroBound2, tfloat oneBound2, tfloat controller, TEx<T> f1, TEx<T> f2, TEx<T> f3) => 
        TEx.Resolve(zeroBound, oneBound, zeroBound2, oneBound2, controller, (z1, o1, z2, o2, c) => 
            Ex.Condition(c.LT(z2), Lerp(z1, o1, c, f1, f2), Lerp(z2, o2, c, f2, f3)));
    
    /// <summary>
    /// Lerp between four functions.
    /// Between zeroBound and oneBound, lerp from the first to the second.
    /// Between zeroBound2 and oneBound2, lerp from the second to the third.
    /// Between zeroBound3 and oneBound3, lerp from the third to the fourth.
    /// </summary>
    public static TEx<T> Lerp4<T>(tfloat zeroBound, tfloat oneBound,
        tfloat zeroBound2, tfloat oneBound2, tfloat zeroBound3, tfloat oneBound3, tfloat controller, 
        TEx<T> f1, TEx<T> f2, TEx<T> f3, TEx<T> f4) => 
        TEx.Resolve(zeroBound, oneBound, zeroBound2, oneBound2, zeroBound3, oneBound3, controller, (z1, o1, z2, o2, z3, o3, c) => 
            Ex.Condition(c.LT(z3),
                Ex.Condition(c.LT(z2), Lerp(z1, o1, c, f1, f2), Lerp(z2, o2, c, f2, f3)),
                Lerp(z3, o3, c, f3, f4)));

    /// <summary>
    /// Lerp between three functions.
    /// Between zeroBound and oneBound, lerp from the first to the second.
    /// Between oneBound and twoBound, lerp from the second to the third.
    /// </summary>
    public static TEx<T> Lerp3c<T>(tfloat zeroBound, tfloat oneBound, tfloat twoBound,
        tfloat controller, TEx<T> f1, TEx<T> f2, TEx<T> f3) => TEx.Resolve(oneBound,
        ob => Lerp3(zeroBound, ob, ob, twoBound, controller, f1, f2, f3));

    /// <summary>
    /// Lerp between two functions.
    /// Between zeroBound and oneBound, lerp from the first to the second.
    /// Between oneBound2 and zeroBound2, lerp from the second back to the first.
    /// </summary>
    /// <param name="zeroBound">Lower bound for lerp controller</param>
    /// <param name="oneBound">Upper bound for lerp controller</param>
    /// <param name="oneBound2">Upper bound for lerp controller</param>
    /// <param name="zeroBound2">Lower bound for lerp controller</param>
    /// <param name="controller">Lerp controller</param>
    /// <param name="f1">First function (when controller leq zeroBound, return this)</param>
    /// <param name="f2">Second function (when controller geq oneBound, return this)</param>
    /// <returns></returns>
    public static TEx<T> LerpBack<T>(tfloat zeroBound, tfloat oneBound, tfloat oneBound2,
        tfloat zeroBound2, tfloat controller, TEx<T> f1, TEx<T> f2) =>
        Lerp3(zeroBound, oneBound, oneBound2, zeroBound2, controller, f1, f2, f1);

    /// <summary>
    /// Lerp between many functions.
    /// </summary>
    public static Func<TExArgCtx, TEx<T>> LerpMany<T>((UncompiledCode<float> bd, UncompiledCode<T> val)[] points, 
        Func<TExArgCtx, TEx<float>> controller) => tac => 
        TEx.Resolve(controller(tac), x => {
            Ex ifLt = points[0].val.Code(tac);
            for (int ii = 0; ii < points.Length - 1; ++ii) {
                ifLt = Ex.Condition(x.LT(points[ii].bd.Code(tac)), ifLt,
                    LerpU(points[ii].bd.Code(tac), points[ii + 1].bd.Code(tac), x, points[ii].val.Code(tac), points[ii + 1].val.Code(tac)));
            }
            return Ex.Condition(x.LT(points[^1].bd.Code(tac)), ifLt, points[^1].val.Code(tac));
        });

    /// <summary>
    /// Select one of an array of values. If OOB, selects the last element.
    /// Note: this expands to (if i = 0) arr[0] (if i = 1) arr[1] ....
    /// This may sound stupid, but since each value is a function, there's no way to actually store it in an array.
    /// </summary>
    public static Func<TExArgCtx, TEx<T>> Select<T>(Func<TExArgCtx, TEx<float>> index, UncompiledCode<T>[] points) => 
        tac => TEx.Resolve((TEx<int>) ((Ex) index(tac)).Cast<int>(), i => {
            Ex ifNeq = points[^1].Code(tac);
            for (int ii = points.Length - 2; ii >= 0; --ii) {
                ifNeq = Ex.Condition(Ex.Equal(i, ExC(ii)), points[ii].Code(tac), ifNeq);
            }
            return ifNeq;
        });

    /// <summary>
    /// Select a value according to the current difficulty counter.
    /// </summary>
    public static TEx<T> SelectDC<T>(TEx<T> easy, TEx<T> normal, TEx<T> hard, TEx<T> lunatic) =>
        Ex.Condition(ExMDifficulty.Dc().Leq(ExC(FixedDifficulty.Normal.Counter())),
            Ex.Condition(ExMDifficulty.Dc().Leq(ExC(FixedDifficulty.Easy.Counter())), easy, normal),
            Ex.Condition(ExMDifficulty.Dc().Leq(ExC(FixedDifficulty.Hard.Counter())), hard, lunatic)
        );

    /// <summary>
    /// Return 0 if the controller is leq the lower bound, 1 if the controller is geq the lower bound, and
    /// a linear interpolation in between.
    /// </summary>
    public static tfloat SStep(tfloat zeroBound, tfloat oneBound, tfloat controller) => TEx.Resolve(zeroBound, controller, (z, c) => Clamp01(c.Sub(z).Div(oneBound.Sub(z))));
    
    /// <summary>
    /// Provide a soft ceiling for the value, multiplying any excess by the value RATIO.
    /// </summary>
    public static tfloat Damp(tfloat ceiling, tfloat ratio, tfloat value) => TEx.Resolve(ceiling, value, (c, x) =>
        If(x.GT(c), c.Add(ratio.Mul(x.Sub(c))), x));
    
    /// <summary>
    /// Lerp between `f1` and `f2` using time (`t`) as a controller.
    /// </summary>
    /// <param name="zeroBound">Lower bound for time. When t=zeroBound, return f1.</param>
    /// <param name="oneBound">Upper bound for time. When t=oneBound, return f2.</param>
    /// <param name="f1">First lerp value</param>
    /// <param name="f2">Second lerp value</param>
    public static Func<TExArgCtx, TEx<T>> LerpT<T>(ExBPY zeroBound, ExBPY oneBound, 
        Func<TExArgCtx, TEx<T>> f1, Func<TExArgCtx, TEx<T>> f2) => b => 
        Lerp(zeroBound(b), oneBound(b), AtomicBPYRepo.T()(b), f1(b), f2(b));
    
    
    public static Func<TExArgCtx, TEx<T>> LerpT3<T>(ExBPY zeroBound, ExBPY oneBound, ExBPY twoBound, ExBPY threeBound, 
        Func<TExArgCtx, TEx<T>> f1, Func<TExArgCtx, TEx<T>> f2, Func<TExArgCtx, TEx<T>> f3) => b => 
        Lerp3(zeroBound(b), oneBound(b), twoBound(b), threeBound(b), AtomicBPYRepo.T()(b), f1(b), f2(b), f3(b));
    
    
    /// <summary>
    /// Return one of two functions depending on the input,
    /// adjusting the switch variable by the reference switch amount if returning the latter function.
    /// </summary>
    /// <param name="switchVar">The variable upon which pivoting is performed. Should be either "p" (firing index) or "t" (time).</param>
    /// <param name="at">Reference</param>
    /// <param name="f1">Function when <c>t \leq at</c></param>
    /// <param name="f2">Function when <c>t \gt at</c></param>
    /// <returns></returns>
    public static Func<TExArgCtx, TEx<T>> SwitchH<T>(ExBPY switchVar, ExBPY at, Func<TExArgCtx, TEx<T>> f1, Func<TExArgCtx, TEx<T>> f2) => bpi => {
        var pivot = VFloat();
        var pivotT = bpi.MakeCopyForExType<TExPI>(out var currEx, out var pivotEx);
        return Ex.Block(new[] { pivot }, 
            pivot.Is(at(bpi)),
            Ex.Condition(Ex.GreaterThan(switchVar(bpi), pivot), 
                Ex.Block(new ParameterExpression[] { pivotEx },
                    Ex.Assign(pivotEx, currEx),
                    SubAssign(switchVar(pivotT), pivot),
                    f2(pivotT)
                ), f1(bpi))
        );
    };
    
    /// <summary>
    /// Apply a ease function on top of a target derivative function that uses time as a controller.
    /// Primarily used for velocity parametrics.
    /// </summary>
    /// <param name="smoother">Name of a float->float smoothing function (<see cref="ExMEasers"/>)</param>
    /// <param name="maxTime">Time over which to perform easing</param>
    /// <param name="f">Target parametric (describing velocity)</param>
    /// <returns></returns>
    public static Func<TExArgCtx, TEx<T>> EaseD<T>(string smoother, float maxTime, 
        Func<TExArgCtx, TEx<T>> f) =>
        ExMHelpers.EaseD(smoother, maxTime, f, bpi => bpi.t, (bpi, t) => bpi.CopyWithT(t));

    /// <summary>
    /// Apply a ease function on top of a target function that uses time as a controller.
    /// </summary>
    /// <param name="smoother">Smoothing function (<see cref="ExMEasers"/>)</param>
    /// <param name="maxTime">Time over which to perform easing</param>
    /// <param name="f">Target parametric (describing offset)</param>
    /// <returns></returns>
    public static Func<TExArgCtx, TEx<T>> Ease<T>([LookupMethod] Func<TExArgCtx, TEx<Func<float, float>>> smoother, float maxTime, 
        Func<TExArgCtx, TEx<T>> f) 
        => ExMHelpers.Ease(smoother, maxTime, f, bpi => bpi.t, (bpi, t) => bpi.CopyWithT(t));


    public static tv2 RotateLerp(tfloat zeroBound, tfloat oneBound, tfloat controller, tv2 source, tv2 target) =>
        TEx.Resolve(zeroBound, oneBound, controller, source, target, (z, o, c, f1, f2) => RotateRad(
            Clamp(z, o, c).Sub(z).Div(o.Sub(z)).Mul(RadDiff(f2, f1)),
            f1
        ));
    public static tv2 RotateLerpCCW(tfloat zeroBound, tfloat oneBound, tfloat controller, tv2 source, tv2 target) =>
        TEx.Resolve(zeroBound, oneBound, controller, source, target, (z, o, c, f1, f2) => RotateRad(
            Clamp(z, o, c).Sub(z).Div(o.Sub(z)).Mul(RadDiffCCW(f2, f1)),
            f1
        ));
    public static tv2 RotateLerpCW(tfloat zeroBound, tfloat oneBound, tfloat controller, tv2 source, tv2 target) =>
        TEx.Resolve(zeroBound, oneBound, controller, source, target, (z, o, c, f1, f2) => RotateRad(
            Clamp(z, o, c).Sub(z).Div(o.Sub(z)).Mul(RadDiffCW(f2, f1)),
            f1
        ));
    
    #region Easing

    /// <summary>
    /// Apply a contortion to a 0-1 range.
    /// This returns an approximately linear function.
    /// </summary>
    /// <param name="smoother">Smoothing function (<see cref="ExMEasers"/>)</param>
    /// <param name="controller">0-1 value</param>
    /// <returns></returns>
    [Obsolete("Instead of running 'smooth(eiosine, t)', you may simply run 'eiosine(t)'.")]
    public static tfloat Smooth([LookupMethod] TEx<Func<float, float>> smoother, tfloat controller) => 
        PartialFn.Execute(smoother, controller);

    /// <summary>
    /// Apply a contortion to a clamped 0-1 range.
    /// This returns an approximately linear function.
    /// </summary>
    /// <param name="smoother">Smoothing function (<see cref="ExMEasers"/>)</param>
    /// <param name="controller">0-1 value (clamped if outside)</param>
    /// <returns></returns>
    public static tfloat SmoothC([LookupMethod] TEx<Func<float, float>> smoother, tfloat controller) 
        => PartialFn.Execute(smoother, Clamp01(controller));

    /// <summary>
    /// Apply a contortion to a 0-x range, returning:
    /// <br/> 0-1 in the range [0,s1]
    /// <br/> 1 in the range [s1,x-s2]
    /// <br/> 1-0 in the range [x-s2,x]
    /// </summary>
    /// <param name="smoother1">First Smoothing function (<see cref="ExMEasers"/>)</param>
    /// <param name="smoother2">Second Smoothing function (<see cref="ExMEasers"/>)</param>
    /// <param name="total">Total time</param>
    /// <param name="smth1">Smooth-in time</param>
    /// <param name="smth2">Smooth-out time</param>
    /// <param name="controller">0-x value</param>
    /// <returns></returns>
    public static tfloat SmoothIO(
        [LookupMethod] TEx<Func<float, float>> smoother1, 
        [LookupMethod] TEx<Func<float, float>> smoother2, 
        tfloat total, tfloat smth1, tfloat smth2, tfloat controller) 
        => TEx.Resolve(total, smth1, smth2, controller,
            (T, s1, s2, t) => Ex.Condition(t.LT(T.Sub(smth2)), 
                    SmoothC(smoother1, t.Div(s1)),
                    E1.Sub(SmoothC(smoother2, t.Sub(T.Sub(s2)).Div(s2)))
                ));

    /// <summary>
    /// Apply SmoothIO where name=name1=name2 and smth=smth1=smth2.
    /// </summary>
    public static tfloat SmoothIOe([LookupMethod] TEx<Func<float, float>> smoother,
        tfloat total, tfloat smth, tfloat controller) 
        => TEx.Resolve(smth, s => SmoothIO(smoother, smoother, total, s, s, controller));

    /// <summary>
    /// Get the value of an easer at a given point between 0 and 1.
    /// The return value is periodized, so if the input is 5.4, then the output is 5 + ease(0.4).
    /// </summary>
    /// <param name="smoother">Smoothing function (<see cref="ExMEasers"/>)</param>
    /// <param name="controller"></param>
    /// <returns></returns>
    public static tfloat SmoothLoop([LookupMethod] TEx<Func<float, float>> smoother, tfloat controller) 
        => TEx.Resolve(controller, x => {
            var per = VFloat();
            return Ex.Block(new[] {per},
                per.Is(Floor(x)),
                per.Add(PartialFn.Execute(smoother, x.Sub(per)))
            );
        });

    /// <summary>
    /// Apply a contortion to a 0-R range, returning R * Smooth(name, controller/R).
    /// This returns an approximately linear function.
    /// </summary>
    /// <param name="smoother">Smoothing function (<see cref="ExMEasers"/>)</param>
    /// <param name="range">Range</param>
    /// <param name="controller">0-R value</param>
    /// <returns></returns>
    public static tfloat SmoothR([LookupMethod] TEx<Func<float, float>> smoother, tfloat range, tfloat controller) =>
        TEx.Resolve(range, r => r.Mul(PartialFn.Execute(smoother, controller.Div(r))));
    
    /// <summary>
    /// Returns R * SmoothLoop(name, controller/R).
    /// </summary>
    public static tfloat SmoothLoopR([LookupMethod] TEx<Func<float, float>> smoother, tfloat range, tfloat controller) =>
        TEx.Resolve(range, r => r.Mul(SmoothLoop(smoother, controller.Div(r))));
    
    
    /// <summary>
    /// Quadratic function that joins an ease-out and an ease-in, ie. two joined parabolas.
    /// </summary>
    /// <param name="midp"></param>
    /// <param name="period"></param>
    /// <param name="controller"></param>
    /// <returns></returns>
    public static tfloat EQuad0m10(tfloat midp, tfloat period, tfloat controller) => TEx.Resolve(midp, m => {
        var t = VFloat();
        return Ex.Block(new[] {t},
            t.Is(controller.Sub(m)),
            Sqr(t).Div(Ex.Condition(t.LT0(), Sqr(m), Sqr(period.Sub(m)))).Complement()
        );
    });
    
    #endregion

    /// <summary>
    /// Perform a quadratic bezier interpolation.
    /// </summary>
    /// <param name="start">Starting point</param>
    /// <param name="ctrl">Control point</param>
    /// <param name="end">Ending point</param>
    /// <param name="time">0-1 lerp controller (automatically clamped)</param>
    public static TEx<T> Bezier<T>(TEx<T> start, TEx<T> ctrl, TEx<T> end, TEx<float> time) => 
        TEx.Resolve<float>(Clamp01(time), t => {
            var c = E1.Sub(t);
            return c.Mul(c).Mul(start)
                .Add(E2.Mul(c).Mul(t).Mul(ctrl))
                .Add(t.Mul(t).Mul(end));
    });

    /// <summary>
    /// Perform a cubic bezier interpolation between points in N-dimensional space.
    /// <br/>This is the same as CalcBezier in BagoumLib when start=0 and end=1.
    /// <br/>This is not the same as the cubic bezier interpolation used in CSS and
    ///  most animation engines. For that functionality, use CubicBezier.
    /// </summary>
    /// <param name="start">Starting point</param>
    /// <param name="ctrl1">First control point</param>
    /// <param name="ctrl2">Second control point</param>
    /// <param name="end">Ending point</param>
    /// <param name="time">0-1 lerp controller (automatically clamped)</param>
    public static TEx<T> Bezier3<T>(TEx<T> start, TEx<T> ctrl1, TEx<T> ctrl2, TEx<T> end, TEx<float> time)
        => TEx.Resolve<float>(Clamp01(time), t => {
            var c = E1.Sub(t);
            return c.Mul(c).Mul(c).Mul(start)
                .Add(ExC(3f).Mul(c).Mul(c).Mul(t).Mul(ctrl1))
                .Add(ExC(3f).Mul(c).Mul(t).Mul(t).Mul(ctrl2))
                .Add(t.Mul(t).Mul(t).Mul(end));
        });

    /// <summary>
    /// Perform a cubic bezier easing interpolation using the same logic as cubic-bezier in CSS.
    /// <br/>This is significantly more computationally expensive than other bezier methods since it requires
    ///  calculating the roots of the bezier function.
    /// <br/>For optimization purposes, it is required that both control coordinates reduce to constants.
    /// </summary>
    /// <param name="time1">Time of first control point</param>
    /// <param name="prog1">Progression of first control point</param>
    /// <param name="time2">Time of first control point</param>
    /// <param name="prog2">Progression of first control point</param>
    /// <param name="time">0-1 lerp controller (automatically clamped)</param>
    public static ExBPY CubicBezier(ExBPY time1, ExBPY prog1, ExBPY time2, ExBPY prog2, ExBPY time) => tac => {
        var f = new FlattenVisitor(false, true);
        if (!f.Visit(time1(tac)).TryAsConst(out float t1))
            throw new Exception("CubicBezier argument time1 is not a constant.");
        if (!f.Visit(prog1(tac)).TryAsConst(out float p1))
            throw new Exception("CubicBezier argument prog1 is not a constant.");
        if (!f.Visit(time2(tac)).TryAsConst(out float t2))
            throw new Exception("CubicBezier argument time2 is not a constant.");
        if (!f.Visit(prog2(tac)).TryAsConst(out float p2))
            throw new Exception("CubicBezier argument prog2 is not a constant.");
        var easer = BagoumLib.Mathematics.Bezier.CBezier(t1, p1, t2, p2);
        tac.Proxy(easer);
        return new ExFunction(easer.GetType().GetMethod("Invoke")!).InstanceOf(Ex.Constant(easer), Clamp01(time(tac)));
    };

    public static Func<TExArgCtx, TEx<T>> CubicBezierLerp<T>(ExBPY time1, ExBPY prog1, ExBPY time2, ExBPY prog2,
        ExBPY time, Func<TExArgCtx, TEx<T>> f1, Func<TExArgCtx, TEx<T>> f2) => b =>
        Lerp01U(CubicBezier(time1, prog1, time2, prog2, time)(b), f1(b), f2(b));

}
}