using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using BagoumLib.Expressions;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Expressions;
using Ex = System.Linq.Expressions.Expression;
using static Danmokou.Expressions.ExMHelpers;
using static Danmokou.Expressions.ExUtils;
using static Danmokou.DMath.Functions.ExM;
using static Danmokou.DMath.Functions.ExMDifficulty;
using ExBPY = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<float>>;
using ExTP = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<UnityEngine.Vector2>>;

namespace Danmokou.DMath.Functions {
/// <summary>
/// Functions that take in parametric information and return a number.
/// </summary>
[SuppressMessage("ReSharper", "UnusedMember.Global")]
[Reflect]
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
    /// Return the parametric time, or if this is a float function, the input value.
    /// </summary>
    /// <returns></returns>
    public static ExBPY T() => bpi => bpi.MaybeBPI?.t ?? bpi.FloatVal;
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
    public static ExBPY P1M(int mod) => 
        bpi => ExM.P1M(mod, bpi.index);
    public static ExBPY P1Ma(int[] children) => 
        P1M(children.Aggregate(1, (x, y) => x * y));
    public static ExBPY P1Mf(ExBPY mod) => 
        bpi => ExM.exP1M(mod(bpi), bpi.index);
    public static ExBPY P1Maf(ExBPY[] children) => 
        bpi => ExM.exP1M(children.Aggregate(E1, (x, y) => x.Mul(y(bpi))), bpi.index);

    /// <summary>
    /// See <see cref="ExM.P2"/>.
    /// </summary>
    /// <returns></returns>
    public static ExBPY P2() => 
        bpi => ExM.P2(bpi.index);
    /// <summary>
    /// See <see cref="ExM.P2M"/>.
    /// </summary>
    /// <returns></returns>
    public static ExBPY P2M(int mod) => 
        bpi => ExM.P2M(mod, bpi.index);
    public static ExBPY P2Ma(int[] children) => 
        P2M(children.Aggregate(1, (x, y) => x * y));
    public static ExBPY P2Mf(ExBPY mod) => 
        bpi => ExM.exP2M(mod(bpi), bpi.index);
    public static ExBPY P2Maf(ExBPY[] children) => 
        bpi => ExM.exP2M(children.Aggregate(E1, (x, y) => x.Mul(y(bpi))), bpi.index);
    /// <summary>
    /// See <see cref="ExM.PM"/>.
    /// </summary>
    /// <returns></returns>
    public static ExBPY PM(int self, int children) => 
        bpi => ExM.PM(self, children, bpi.index);
    public static ExBPY PMa(int self, int[] children) => 
        PM(self, children.Aggregate(1, (x, y) => x * y));
    public static ExBPY PMf(ExBPY self, ExBPY children) => 
        bpi => ExM.exPM(self(bpi), children(bpi), bpi.index);
    public static ExBPY PMaf(ExBPY self, ExBPY[] children) => bpi =>
        ExM.exPM(self(bpi), children.Aggregate(E1, (x, y) => x.Mul(y(bpi))), bpi.index);
    
    /// <summary>
    /// Return the parametric x-position, or, if this is a float function, the input value.
    /// </summary>
    /// <returns></returns>
    public static ExBPY X() => bpi => bpi.MaybeBPI?.locx ?? bpi.FloatVal;
    /// <summary>
    /// Return the parametric y-position.
    /// </summary>
    /// <returns></returns>
    public static ExBPY Y() => bpi => bpi.locy;


    private static readonly ExFunction SeedRandUint = ExFunction.Wrap(typeof(RNG), "GetSeededFloat", new[] {typeof(float), typeof(float), typeof(uint)});

    /// <summary>
    /// Return a random number as a deterministic function of the parametric ID.
    /// </summary>
    /// <param name="from">Minimum</param>
    /// <param name="to">Maximum</param>
    /// <returns></returns>
    public static ExBPY BRand(ExBPY from, ExBPY to) => bpi => SeedRandUint.Of(from(bpi), to(bpi), bpi.id);
    /// <summary>
    /// Return a random number as a deterministic function of the parametric ID.
    /// <br/>This function returns a different value from `<see cref="BRand"/>`.
    /// </summary>
    /// <param name="from">Minimum</param>
    /// <param name="to">Maximum</param>
    /// <returns></returns>
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

    [Fallthrough(1)]
    public static ExBPY Const(float x) => bpi => Ex.Constant(x);
    
    
    
    

    /// <summary>
    /// See <see cref="Parametrics.Pivot"/>.
    /// </summary>
    public static ExBPY Pivot(ExBPY pivotVar, ExBPY pivot, ExBPY f1, ExBPY f2) => 
        ExMHelpers.Pivot<TExPI, float>(pivot, f1, f2, pivotVar);
    
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
        ExMHelpers.SoftmaxShift<TExPI>(sharpness, pivot, f1, f2, pivotVar);
    [Alias("smsht")]
    public static ExBPY SoftmaxShiftT(ExBPY sharpness, ExBPY pivot, ExBPY f1, ExBPY f2) => SoftmaxShift("t", sharpness, pivot, f1, f2);
    [Alias("smsht3")]
    public static ExBPY SoftmaxShiftT3(ExBPY sharp, ExBPY pivot, ExBPY sharp2, ExBPY pivot2, ExBPY f1, ExBPY f2, ExBPY f3) => 
        SoftmaxShiftT(sharp, pivot, f1, SoftmaxShiftT(sharp2, pivot2, f2, f3));

    
    /// <summary>
    /// See <see cref="BPYRepo.SoftmaxShift"/>.
    /// </summary>
    public static ExBPY LogsumShift(string pivotVar, ExBPY sharpness, ExBPY pivot, ExBPY f1, ExBPY f2) =>
        ExMHelpers.LogSumShift<TExPI>(sharpness, pivot, f1, f2, pivotVar);
    
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
}
}
