﻿using System;
using System.Diagnostics.CodeAnalysis;
using BagoumLib.Expressions;
using Danmokou.Core;
using Danmokou.DataHoist;
using Danmokou.Expressions;
using Scriptor;
using Scriptor.Expressions;
using Ex = System.Linq.Expressions.Expression;
using ExBPY = System.Func<Scriptor.Expressions.TExArgCtx, Scriptor.Expressions.TEx<float>>;
using ExPred = System.Func<Scriptor.Expressions.TExArgCtx, Scriptor.Expressions.TEx<bool>>;

namespace Danmokou.DMath.Functions {
/// <summary>
/// See <see cref="ExM"/>. This class contains functions related to subsamplers.
/// </summary>
[Reflect]
[SuppressMessage("ReSharper", "UnusedMethodReturnValue.Global")]
public static class ExMSamplers {
    /// <summary>
    /// If the input time is less than the reference time, evaluate the invokee. Otherwise, return the last returned evaluation.
    /// <br/>You can call this with zero sampling time, and it will sample the invokee once. However, in this case SS0 is preferred.
    /// </summary>
    /// <param name="time">Time at which to stop sampling</param>
    /// <param name="p">Target function</param>
    /// <returns></returns>
    [Alias("ss")]
    public static Func<TExArgCtx, TEx<T>> StopSampling<T>(ExBPY time, Func<TExArgCtx, TEx<T>> p) =>
        SampleIf(tac => Ex.LessThan(tac.t(), time(tac)), p);

    /// <summary>
    /// If the condition is true, evaluate the invokee. Otherwise, return the last returned evaluation.
    /// <br/>You can call this with a cond of false, and it will sample the invokee once. However, in this case SS0 is preferred.
    /// </summary>
    public static Func<TExArgCtx, TEx<T>> SampleIf<T>(ExPred cond, Func<TExArgCtx, TEx<T>> p) =>
        bpi => {
            var key = bpi.Ctx.NameWithSuffix("_SampleIfKey");
            return Ex.Condition(Ex.OrElse(cond(bpi), Ex.Not(bpi.DynamicHas<T>(key))),
                bpi.DynamicSet<T>(key, p(bpi)),
                bpi.DynamicGet<T>(key)
            );
        };


    /// <summary>
    /// Samples an invokee exactly once.
    /// </summary>
    /// <param name="p">Target function</param>
    /// <returns></returns>
    public static Func<TExArgCtx, TEx<T>> SS0<T>(Func<TExArgCtx, TEx<T>> p) =>
        bpi => {
            var key = bpi.Ctx.NameWithSuffix("_SampleIfKey");
            return Ex.Condition(Ex.Not(bpi.DynamicHas<T>(key)),
                bpi.DynamicSet<T>(key, p(bpi)),
                bpi.DynamicGet<T>(key)
            );
        };
}
}