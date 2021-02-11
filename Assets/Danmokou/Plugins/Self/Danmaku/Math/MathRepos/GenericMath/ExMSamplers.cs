using System;
using System.Diagnostics.CodeAnalysis;
using DMK.Core;
using DMK.DataHoist;
using DMK.Expressions;
using Ex = System.Linq.Expressions.Expression;
using tfloat = DMK.Expressions.TEx<float>;
using tbool = DMK.Expressions.TEx<bool>;
using tv2 = DMK.Expressions.TEx<UnityEngine.Vector2>;
using tv3 = DMK.Expressions.TEx<UnityEngine.Vector3>;
using trv2 = DMK.Expressions.TEx<DMK.DMath.V2RV2>;
using efloat = DMK.Expressions.EEx<float>;
using ev2 = DMK.Expressions.EEx<UnityEngine.Vector2>;
using ev3 = DMK.Expressions.EEx<UnityEngine.Vector3>;
using erv2 = DMK.Expressions.EEx<DMK.DMath.V2RV2>;
using ExBPY = System.Func<DMK.Expressions.TExArgCtx, DMK.Expressions.TEx<float>>;
using ExPred = System.Func<DMK.Expressions.TExArgCtx, DMK.Expressions.TEx<bool>>;

namespace DMK.DMath.Functions {
/// <summary>
/// See <see cref="ExM"/>. This class contains functions related to subsamplers.
/// </summary>
[Reflect]
[SuppressMessage("ReSharper", "UnusedMethodReturnValue.Global")]
public static class ExMSamplers {
    /// <summary>
    /// If the input time is less than the reference time, evaluate the invokee. Otherwise, return the last returned evaluation.
    /// <para>You can call this with zero sampling time, and it will sample the invokee once. However, in this case SS0 is preferred.</para>
    /// </summary>
    /// <param name="time">Time at which to stop sampling</param>
    /// <param name="p">Target function</param>
    /// <returns></returns>
    [Alias("ss")]
    public static Func<TExArgCtx, TEx<T>> StopSampling<T>(ExBPY time, Func<TExArgCtx, TEx<T>> p) =>
        SampleIf(tac => Ex.LessThan(tac.t, time(tac)), p);

    /// <summary>
    /// If the condition is true, evaluate the invokee. Otherwise, return the last returned evaluation.
    /// <para>You can call this with zero sampling time, and it will sample the invokee once. However, in this case SS0 is preferred.</para>
    /// </summary>
    public static Func<TExArgCtx, TEx<T>> SampleIf<T>(ExPred cond, Func<TExArgCtx, TEx<T>> p) =>
        bpi => {
            var key = bpi.Ctx.NameWithSuffix("_SampleIfKey");
            return Ex.Condition(Ex.OrElse(cond(bpi), Ex.Not(bpi.FCtxHas<T>(key))),
                bpi.FCtxSet<T>(key, p(bpi)),
                bpi.FCtxGet<T>(key)
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
            return Ex.Condition(Ex.Not(bpi.FCtxHas<T>(key)),
                bpi.FCtxSet<T>(key, p(bpi)),
                bpi.FCtxGet<T>(key)
            );
        };
}
}