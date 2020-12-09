using System;
using System.Diagnostics.CodeAnalysis;
using DMK.Core;
using DMK.Expressions;
using Ex = System.Linq.Expressions.Expression;
using ExFXY = System.Func<DMK.Expressions.TEx<float>, DMK.Expressions.TEx<float>>;
using ExBPY = System.Func<DMK.Expressions.TExPI, DMK.Expressions.TEx<float>>;
using ExPred = System.Func<DMK.Expressions.TExPI, DMK.Expressions.TEx<bool>>;
using ExTP = System.Func<DMK.Expressions.TExPI, DMK.Expressions.TEx<UnityEngine.Vector2>>;
using ExBPRV2 = System.Func<DMK.Expressions.TExPI, DMK.Expressions.TEx<DMK.DMath.V2RV2>>;
using static DMK.Expressions.ExUtils;

namespace DMK.DMath.Functions {

/// <summary>
/// Number>number functions. 
/// </summary>
[SuppressMessage("ReSharper", "UnusedMember.Global")]
public static class FXYRepo {
    /// <summary>
    /// Identity function.
    /// </summary>
    public static ExFXY X() {
        return t => (t.type == tfloat) ? t : Ex.Convert(t, tfloat);
    }

    /// <summary>
    /// Identity function.
    /// </summary>
    public static ExFXY T() => X();

    [Fallthrough(1)]
    public static ExFXY Const(float x) => bpi => Ex.Constant(x);


    /// <summary>
    /// Apply a ease function on top of a target function that uses time as a controller.
    /// </summary>
    /// <param name="smoother">Smoothing function</param>
    /// <param name="maxTime">Time over which to perform easing</param>
    /// <param name="f">Target function</param>
    /// <returns></returns>
    public static ExFXY Ease([LookupMethod] Func<TEx<float>, TEx<float>> smoother, float maxTime, ExFXY f) 
    => ExMHelpers.Ease(smoother, maxTime, f, x => x, (x, y) => y);


    /// <summary>
    /// Apply a ease function on top of a target derivative function that uses time as a controller.
    /// </summary>
    /// <param name="smoother">Smoothing function</param>
    /// <param name="maxTime">Time over which to perform easing</param>
    /// <param name="fd">Target function</param>
    /// <returns></returns>
    public static ExFXY EaseD([LookupMethod] Func<TEx<float>, TEx<float>> smoother, float maxTime, ExFXY fd) 
        => ExMHelpers.EaseD(smoother, maxTime, fd, x => x, (x, y) => y);

    /// <summary>
    /// See <see cref="BPYRepo.SoftmaxShift"/>.
    /// </summary>
    public static ExFXY SoftmaxShift(ExFXY sharpness, ExFXY pivot, ExFXY f1, ExFXY f2) =>
        ExMHelpers.SoftmaxShift(sharpness, pivot, f1, f2, "x");

}

}
