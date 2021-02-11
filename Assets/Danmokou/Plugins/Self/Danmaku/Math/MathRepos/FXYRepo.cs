using System;
using System.Diagnostics.CodeAnalysis;
using DMK.Core;
using DMK.Expressions;
using Ex = System.Linq.Expressions.Expression;
using static DMK.Expressions.ExUtils;
using ExBPY = System.Func<DMK.Expressions.TExArgCtx, DMK.Expressions.TEx<float>>;

namespace DMK.DMath.Functions {

/// <summary>
/// Number>number functions. 
/// </summary>
[SuppressMessage("ReSharper", "UnusedMember.Global")]
[Reflect]
public static class FXYRepo {

    /// <summary>
    /// Apply a ease function on top of a target function that uses time as a controller.
    /// </summary>
    /// <param name="smoother">Smoothing function (<see cref="ExMEasers"/>)</param>
    /// <param name="maxTime">Time over which to perform easing</param>
    /// <param name="f">Target function</param>
    /// <returns></returns>
    public static ExBPY EaseF([LookupMethod] Func<TEx<float>, TEx<float>> smoother, float maxTime, ExBPY f) 
    => ExMHelpers.Ease(smoother, maxTime, f, x => x.FloatVal, (x, y) => x.MakeCopyForType<TEx<float>>(y));


    /// <summary>
    /// Apply a ease function on top of a target derivative function that uses time as a controller.
    /// </summary>
    /// <param name="smoother">Smoothing function (<see cref="ExMEasers"/>)</param>
    /// <param name="maxTime">Time over which to perform easing</param>
    /// <param name="fd">Target function</param>
    /// <returns></returns>
    public static ExBPY EaseFD([LookupMethod] Func<TEx<float>, TEx<float>> smoother, float maxTime, ExBPY fd) 
        => ExMHelpers.EaseD(smoother, maxTime, fd, x => x.FloatVal, (x, y) => x.MakeCopyForType<TEx<float>>(y));

    /// <summary>
    /// See <see cref="BPYRepo.SoftmaxShift"/>.
    /// </summary>
    public static ExBPY SoftmaxShift(ExBPY sharpness, ExBPY pivot, ExBPY f1, ExBPY f2) =>
        ExMHelpers.SoftmaxShift<TEx<float>>(sharpness, pivot, f1, f2, "x");

}

}
