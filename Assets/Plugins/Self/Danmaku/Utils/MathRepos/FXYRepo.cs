using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEngine;
using Core;
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
/// Functions that take in a number and return a number (FXY).
/// Best used for adjusting parametrics when a "function of time"
/// or "function of firing index" is necessary.
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
    /// <param name="name">Easing function name</param>
    /// <param name="maxTime">Time over which to perform easing</param>
    /// <param name="f">Target function</param>
    /// <returns></returns>
    public static ExFXY Ease(string name, float maxTime, ExFXY f) => ExMHelpers.Ease(name, maxTime, f, x => x, (x, y) => y);


    /// <summary>
    /// Apply a ease function on top of a target derivative function that uses time as a controller.
    /// </summary>
    /// <param name="name">Easing function name</param>
    /// <param name="maxTime">Time over which to perform easing</param>
    /// <param name="fd">Target function</param>
    /// <returns></returns>
    public static ExFXY EaseD(string name, float maxTime, ExFXY fd) => ExMHelpers.EaseD(name, maxTime, fd, x => x, (x, y) => y);

    /// <summary>
    /// See <see cref="DMath.BPYRepo.SoftmaxShift"/>.
    /// </summary>
    public static ExFXY SoftmaxShift(ExFXY sharpness, ExFXY pivot, ExFXY f1, ExFXY f2) =>
        ExMHelpers.SoftmaxShift(sharpness, pivot, f1, f2, "x");

}

}
