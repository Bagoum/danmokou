using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEngine;
using System.Linq.Expressions;
using Ex = System.Linq.Expressions.Expression;
using ExFXY = System.Func<TEx<float>, TEx<float>>;
using ExBPY = System.Func<DMath.TExPI, TEx<float>>;
using ExPred = System.Func<DMath.TExPI, TEx<bool>>;
using ExTP = System.Func<DMath.TExPI, TEx<UnityEngine.Vector2>>;
using ExBPRV2 = System.Func<DMath.TExPI, TEx<DMath.V2RV2>>;
using static DMath.ExMHelpers;
using static ExUtils;
using static DMath.ExM;

namespace DMath {

/// <summary>
/// Functions that take in parametric information and return a number.
/// </summary>
[SuppressMessage("ReSharper", "UnusedMember.Global")]
public static partial class BPYRepo {

    /// <summary>
    /// Random calls are dependent on the object's parametric ID.
    /// This function rehashes the ID for the child call, so
    /// any other random parametrics will no longer be dependent on the child call.
    /// </summary>
    /// <param name="b">Target parametric-number function</param>
    /// <returns></returns>
    public static ExBPY Rehash(ExBPY b) {
        return bpi => b(bpi.Rehash());
    }
    /// <summary>
    /// Return the parametric time.
    /// </summary>
    /// <returns></returns>
    public static ExBPY T() => bpi => bpi.t;
    /// <summary>
    /// Return the parametric firing index.
    /// </summary>
    /// <returns></returns>
    public static ExBPY P() => bpi => bpi.findex;
    /// <summary>
    /// See <see cref="ExM.P1"/>.
    /// </summary>
    /// <returns></returns>
    public static ExBPY P1() => bpi => ExM.P1(bpi.index);
    /// <summary>
    /// See <see cref="ExM.P1M"/>.
    /// </summary>
    /// <returns></returns>
    public static ExBPY P1M(int mod) => bpi => ExM.P1M(mod, bpi.index);
    public static ExBPY P1Ma(int[] children) => P1M(children.Aggregate(1, (x, y) => x * y));
    public static ExBPY P1Mf(FXY mod) => P1M((int)mod(0));
    public static ExBPY P1Maf(FXY[] children) => P1M(children.Aggregate(1, (x, y) => x * (int) y(0)));

    /// <summary>
    /// See <see cref="ExM.P2"/>.
    /// </summary>
    /// <returns></returns>
    public static ExBPY P2() => bpi => ExM.P2(bpi.index);
    /// <summary>
    /// See <see cref="ExM.P2M"/>.
    /// </summary>
    /// <returns></returns>
    public static ExBPY P2M(int mod) => bpi => ExM.P2M(mod, bpi.index);
    public static ExBPY P2Ma(int[] children) => P2M(children.Aggregate(1, (x, y) => x * y));
    public static ExBPY P2Mf(FXY mod) => P2M((int)mod(0));
    public static ExBPY P2Maf(FXY[] children) => P2M(children.Aggregate(1, (x, y) => x * (int) y(0)));
    /// <summary>
    /// See <see cref="ExM.PM"/>.
    /// </summary>
    /// <returns></returns>
    public static ExBPY PM(int self, int children) => bpi => ExM.PM(self, children, bpi.index);
    public static ExBPY PMa(int self, int[] children) => PM(self, children.Aggregate(1, (x, y) => x * y));
    public static ExBPY PMf(FXY self, FXY children) => PM((int)self(0), (int)children(0));
    public static ExBPY PMaf(FXY self, FXY[] children) => PM((int) self(0), children.Aggregate(1, (x, y) => x * (int) y(0)));
    
    /// <summary>
    /// Return the parametric x-position.
    /// </summary>
    /// <returns></returns>
    public static ExBPY X() => bpi => bpi.locx;
    /// <summary>
    /// Return the parametric y-position.
    /// </summary>
    /// <returns></returns>
    public static ExBPY Y() => bpi => bpi.locy;

    /// <summary>
    /// Returns the cosine of rotation of the entity's velocity struct.
    /// </summary>
    public static ExBPY AC() => Reference<float>("ac");
    /// <summary>
    /// Returns the sine of rotation of the entity's velocity struct.
    /// </summary>
    public static ExBPY AS() => Reference<float>("as");
    /// <summary>
    /// Returns the angle (degrees) of rotation of the entity's velocity struct.
    /// </summary>
    public static ExBPY A() => Reference<float>("a");
    
    /// <summary>
    /// Wrap a number-number function to take a function of parametric info as input.
    /// </summary>
    /// <param name="map">Control function of parametric info</param>
    /// <param name="f">Target number-number function</param>
    /// <returns></returns>
    public static ExBPY BfF(ExBPY map, ExFXY f) => bpi => f(map(bpi));
    
    private static readonly ExFunction SeedRandUint = Wrap(typeof(RNG), "GetSeededFloat", new[] {typeof(float), typeof(float), typeof(uint)});

    /// <summary>
    /// Return a random number as a deterministic function of the parametric ID.
    /// </summary>
    /// <param name="from">Minimum</param>
    /// <param name="to">Maximum</param>
    /// <returns></returns>
    public static ExBPY BRand(ExBPY from, ExBPY to) => bpi => SeedRandUint.Of(from(bpi), to(bpi), bpi.id);
    public static ExBPY BRand2(ExBPY from, ExBPY to) => bpi => SeedRandUint.Of(from(bpi), to(bpi), RNG.Rehash(bpi.id));

    /// <summary>
    /// Return either 0 or 1 based on the parametric ID.
    /// </summary>
    /// <returns></returns>
    public static ExBPY BRand01() => bpi => Ex.Condition(bpi.id.GT(ExC(uint.MaxValue / (uint)2)), E0, E1);

    /// <summary>
    /// Return either -1 or 1 based on the parametric ID.
    /// </summary>
    /// <returns></returns>
    public static ExBPY BRandpm1() => bpi => Ex.Condition(bpi.id.GT(ExC(uint.MaxValue / (uint)2)), EN1, E1);

    /// <summary>
    /// Return 1 if the predicate is true and 0 if it is false.
    /// </summary>
    /// <param name="pred"></param>
    /// <returns></returns>
    [Fallthrough(150)]
    public static ExBPY Pred10(ExPred pred) => bpi => Ex.Condition(pred(bpi), E1, E0);

    [Fallthrough(1)]
    public static ExBPY Const(float x) => bpi => Ex.Constant(x);

    /// <summary>
    /// Return one of two functions depending on the input,
    /// adjusting the switch variable by the reference switch amount if returning the latter function.
    /// </summary>
    /// <param name="switchVar">The variable upon which pivoting is performed. Should be either "p" (firing index) or "t" (time).</param>
    /// <param name="at">Reference</param>
    /// <param name="f1">Function when <c>t \leq at</c></param>
    /// <param name="f2">Function when <c>t \gt at</c></param>
    /// <returns></returns>
    public static ExBPY SwitchH(ExBPY switchVar, ExBPY at, ExBPY f1, ExBPY f2) => bpi => {
        var pivot = VFloat();
        var cold = new TExPI();
        return Ex.Block(new[] { pivot }, 
            pivot.Is(at(bpi)),
            Ex.Condition(Ex.GreaterThan(switchVar(bpi), pivot), 
                Ex.Block(new ParameterExpression[] { cold },
                    Ex.Assign(cold, bpi),
                    SubAssign(switchVar(cold), pivot),
                    f2(cold)
                ), f1(bpi))
        );
    };

    public static ExBPY SwitchHT(ExBPY at, ExBPY f1, ExBPY f2) => SwitchH(T(), at, f1, f2);
    
    /// <summary>
    /// See <see cref="DMath.Parametrics.Pivot"/>.
    /// </summary>
    public static ExBPY Pivot(ExBPY pivotVar, ExBPY pivot, ExBPY f1, ExBPY f2) => ExMHelpers.Pivot(pivot, f1, f2, pivotVar);
    
    /*
    public static ExBPY Softmax(ExBPY sharpness, ExBPY[] against) => bpi => GenericMath.Softmax(sharpness(bpi), against.Select(x => x(bpi)).ToArray());
    
    public static ExBPY Logsum(ExBPY sharpness, ExBPY[] against) => ExM.LogSum(sharpness, against);
*/
    /// <summary>
    /// Smoothly pivot from one function to another.
    /// Functionality is similar to Pivot, but you must specify the direction, and because this relies on softmax,
    /// the value f2(t) - f2(pivot) + f1(pivot) must always be greater than f1(pivot) after the pivot point
    /// and less than before the pivot point (when sharpness is positive).
    /// </summary>
    /// <param name="pivotVar">The variable upon which pivoting is performed. Should be either "p" (firing index) or "t" (time) or a reference variable.</param>
    /// <param name="sharpness">The higher the absolute value of this, the more quickly the result will converge.
    /// Set negative for softmin.</param>
    /// <param name="pivot">The value of the variable at which pivoting is performed</param>
    /// <param name="f1">Starting equation</param>
    /// <param name="f2">Equation after pivot</param>
    /// <returns></returns>
    public static ExBPY SoftmaxShift(string pivotVar, ExBPY sharpness, ExBPY pivot, ExBPY f1, ExBPY f2) =>
        ExMHelpers.SoftmaxShift(sharpness, pivot, f1, f2, pivotVar);
    [Alias("smsht")]
    public static ExBPY SoftmaxShiftT(ExBPY sharpness, ExBPY pivot, ExBPY f1, ExBPY f2) => SoftmaxShift("t", sharpness, pivot, f1, f2);
    [Alias("smsht3")]
    public static ExBPY SoftmaxShiftT3(ExBPY sharp, ExBPY pivot, ExBPY sharp2, ExBPY pivot2, ExBPY f1, ExBPY f2, ExBPY f3) => 
        SoftmaxShiftT(sharp, pivot, f1, SoftmaxShiftT(sharp2, pivot2, f2, f3));

    
    /// <summary>
    /// See <see cref="DMath.BPYRepo.SoftmaxShift"/>.
    /// </summary>
    public static ExBPY LogsumShift(string pivotVar, ExBPY sharpness, ExBPY pivot, ExBPY f1, ExBPY f2) =>
        ExMHelpers.LogSumShift(sharpness, pivot, f1, f2, pivotVar);
    
    /// <summary>
    /// Logsumshift using t as a pivot variable
    /// </summary>
    [Alias("lssht")]
    public static ExBPY LogsumShiftT(ExBPY sharpness, ExBPY pivot, ExBPY f1, ExBPY f2) => LogsumShift("t", sharpness, pivot, f1, f2);
    /// <summary>
    /// Logsumshift twice, using t as a pivot variable, first from f1 to f2 and then from f2 to f3
    /// </summary>
    [Alias("lssht3")]
    public static ExBPY LogsumShiftT3(ExBPY sharp, ExBPY pivot, ExBPY sharp2, ExBPY pivot2, ExBPY f1, ExBPY f2, ExBPY f3) => 
        LogsumShiftT(sharp, pivot, f1, LogsumShiftT(sharp2, pivot2, f2, f3));
    /// <summary>
    /// Logsumshift using &amp;t as a pivot variable. Make sure this is defined in a higher-level let statement.
    /// </summary>
    [Alias("lsshat")]
    public static ExBPY LogsumShiftAT(ExBPY sharpness, ExBPY pivot, ExBPY f1, ExBPY f2) => LogsumShift("&t", sharpness, pivot, f1, f2);

    /// <summary>
    /// Apply a ease function on top of a target that uses time as a controller.
    /// </summary>
    /// <param name="name">Easing function name</param>
    /// <param name="maxTime">Time over which to perform easing</param>
    /// <param name="f">Target function</param>
    /// <returns></returns>
    public static ExBPY Ease(string name, float maxTime, ExBPY f) => ExMHelpers.Ease(name, maxTime, f);


    /// <summary>
    /// Apply a ease function on top of a target derivative function that uses time as a controller.
    /// </summary>
    /// <param name="name">Easing function name</param>
    /// <param name="maxTime">Time over which to perform easing</param>
    /// <param name="fd">Target function</param>
    /// <returns></returns>
    public static ExBPY EaseD(string name, float maxTime, ExBPY fd) => ExMHelpers.EaseD(name, maxTime, fd);
    
    /// <summary>
    /// See <see cref="ExM.Lerp{T}"/>
    /// </summary>
    public static ExBPY LerpT(ExBPY zeroBound, ExBPY oneBound, ExBPY f1, ExBPY f2) => bpi =>
    ExM.Lerp(zeroBound(bpi), oneBound(bpi), T()(bpi), f1(bpi), f2(bpi));

    public static ExBPY LerpT3(ExBPY zeroBound, ExBPY oneBound, ExBPY twoBound, ExBPY threeBound, ExBPY f1, ExBPY f2, ExBPY f3) =>
        bpi => ExM.Lerp3(zeroBound(bpi), oneBound(bpi), twoBound(bpi), threeBound(bpi), T()(bpi), f1(bpi), f2(bpi), f3(bpi));

    /// <summary>
    /// See <see cref="ExM.LerpBack{T}"/>
    /// </summary>
    public static ExBPY LerpBackT(ExBPY zeroBound, ExBPY oneBound, ExBPY oneBound2, ExBPY zeroBound2, ExBPY f1,
        ExBPY f2) => bpi =>
        ExM.LerpBack(zeroBound(bpi), oneBound(bpi), oneBound2(bpi), zeroBound2(bpi), T()(bpi), f1(bpi), f2(bpi));

    /// <summary>
    /// Lerp into a function, using time as the lerp controller.
    /// </summary>
    /// <param name="zeroBound">Lower bound for lerp controller</param>
    /// <param name="oneBound">Upper bound for lerp controller</param>
    /// <param name="f">Target function</param>
    /// <returns></returns>
    public static ExBPY LerpTIn(ExBPY zeroBound, ExBPY oneBound, ExBPY f) => LerpT(zeroBound, oneBound, _ => 0f, f);

    /// <summary>
    /// Default randomized star rotation (slow) in one direction.
    /// </summary>
    public static ExBPY StarRot1() => bpi => Mul(T()(bpi), BRand(_ => 60f, _ => 90f)(bpi));
    /// <summary>
    /// Default randomized star rotation (slow) in two directions.
    /// </summary>
    public static ExBPY StarRotB1() => bpi => Mul(BRandpm1()(bpi), StarRot1()(bpi));
    /// <summary>
    /// Default randomized star rotation (medium) in one direction.
    /// </summary>
    public static ExBPY StarRot2() => bpi => Mul(T()(bpi), BRand(_ => 100f, _ => 140f)(bpi));
    /// <summary>
    /// Default randomized star rotation (medium) in two directions.
    /// </summary>
    public static ExBPY StarRotB2() => bpi => Mul(BRandpm1()(bpi), StarRot2()(bpi));
    /// <summary>
    /// Default randomized star rotation (fast) in one direction.
    /// </summary>
    public static ExBPY StarRot3() => bpi => Mul(T()(bpi), BRand(_ => 160f, _ => 200f)(bpi));
    /// <summary>
    /// Default randomized star rotation (fast) in two directions.
    /// </summary>
    public static ExBPY StarRotB3() => bpi => Mul(BRandpm1()(bpi), StarRot3()(bpi));
    /// <summary>
    /// Default randomized star rotation (very fast) in one direction.
    /// </summary>
    public static ExBPY StarRot4() => bpi => Mul(T()(bpi), BRand(_ => 240f, _ => 300f)(bpi));
    /// <summary>
    /// Default randomized star rotation (very fast) in two directions.
    /// </summary>
    public static ExBPY StarRotB4() => bpi => Mul(BRandpm1()(bpi), StarRot4()(bpi));

    /// <summary>
    /// Get distance (square root) from current location to a point.
    /// </summary>
    /// <param name="loc"></param>
    /// <returns></returns>
    public static ExBPY DistTo(ExTP loc) => bpi => Dist(loc(bpi), bpi.loc);
    
    /// <summary>
    /// Returns T / DN.
    /// </summary>
    public static ExBPY tDN() => bpi => bpi.t.Div(DN());

    /// <summary>
    /// Returns T / DH.
    /// </summary>
    public static ExBPY tDH() => bpi => bpi.t.Div(DH());
    
    /// <summary>
    /// Returns T / DL.
    /// </summary>
    public static ExBPY tDL() => bpi => bpi.t.Div(DL());
    /// <summary>
    /// Returns P / DH.
    /// </summary>
    public static ExBPY pDH() => bpi => bpi.findex.Div(DH());
    
    /// <summary>
    /// Returns P / DL.
    /// </summary>
    public static ExBPY pDL() => bpi => bpi.findex.Div(DL());

    public static ExBPY OptionAngle() => b => FireOption.optionAngle.Of(b.index);
}
}
