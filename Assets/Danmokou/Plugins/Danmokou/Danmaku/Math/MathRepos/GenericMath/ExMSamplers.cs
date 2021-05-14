using System;
using System.Diagnostics.CodeAnalysis;
using Danmokou.Core;
using Danmokou.DataHoist;
using Danmokou.Expressions;
using Ex = System.Linq.Expressions.Expression;
using tfloat = Danmokou.Expressions.TEx<float>;
using tbool = Danmokou.Expressions.TEx<bool>;
using tv2 = Danmokou.Expressions.TEx<UnityEngine.Vector2>;
using tv3 = Danmokou.Expressions.TEx<UnityEngine.Vector3>;
using trv2 = Danmokou.Expressions.TEx<Danmokou.DMath.V2RV2>;
using efloat = Danmokou.Expressions.EEx<float>;
using ev2 = Danmokou.Expressions.EEx<UnityEngine.Vector2>;
using ev3 = Danmokou.Expressions.EEx<UnityEngine.Vector3>;
using erv2 = Danmokou.Expressions.EEx<Danmokou.DMath.V2RV2>;
using ExBPY = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<float>>;
using ExPred = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<bool>>;

namespace Danmokou.DMath.Functions {
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