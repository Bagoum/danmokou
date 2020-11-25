using System;
using Core;
using Danmaku;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;
using static ExUtils;
using static DMath.ExMHelpers;
using tfloat = TEx<float>;
using tbool = TEx<bool>;
using tv2 = TEx<UnityEngine.Vector2>;
using tv3 = TEx<UnityEngine.Vector3>;
using trv2 = TEx<DMath.V2RV2>;
using efloat = DMath.EEx<float>;
using ev2 = DMath.EEx<UnityEngine.Vector2>;
using ev3 = DMath.EEx<UnityEngine.Vector3>;
using erv2 = DMath.EEx<DMath.V2RV2>;
using ExBPY = System.Func<DMath.TExPI, TEx<float>>;
using ExPred = System.Func<DMath.TExPI, TEx<bool>>;
using static DMath.ExM;

namespace DMath {
/// <summary>
/// See <see cref="DMath.ExM"/>. This class contains functions related to subsamplers.
/// </summary>
public static class ExMSamplers {
    /// <summary>
    /// If the input time is less than the reference time, evaluate the invokee. Otherwise, return the last returned evaluation.
    /// <para>You can call this with zero sampling time, and it will sample the invokee once. However, in this case SS0 is preferred.</para>
    /// </summary>
    /// <param name="time">Time at which to stop sampling</param>
    /// <param name="p">Target function</param>
    /// <returns></returns>
    [Alias("ss")]
    public static Func<TExPI, TEx<T>> StopSampling<T>(ExBPY time, Func<TExPI, TEx<T>> p) {
        Ex data = DataHoisting.GetClearableDict<T>();
        return bpi => ExUtils.DictIfCondSetElseGet(data, Ex.OrElse(Ex.LessThan(bpi.t, time(bpi)),
            Ex.Not(ExUtils.DictContains<uint, T>(data, bpi.id))), bpi.id, p(bpi));
    }
    
    /// <summary>
    /// If the condition is true, evaluate the invokee. Otherwise, return the last returned evaluation.
    /// <para>You can call this with zero sampling time, and it will sample the invokee once. However, in this case SS0 is preferred.</para>
    /// </summary>
    public static Func<TExPI, TEx<T>> SampleIf<T>(ExPred cond, Func<TExPI, TEx<T>> p) {
        Ex data = DataHoisting.GetClearableDict<T>();
        return bpi => ExUtils.DictIfCondSetElseGet(data, Ex.OrElse(cond(bpi),
            Ex.Not(ExUtils.DictContains<uint, T>(data, bpi.id))), bpi.id, p(bpi));
    }
    
    
    /// <summary>
    /// Samples an invokee exactly once.
    /// </summary>
    /// <param name="p">Target function</param>
    /// <returns></returns>
    public static Func<TExPI, TEx<T>> SS0<T>(Func<TExPI, TEx<T>> p) {
        Ex data = DataHoisting.GetClearableDict<T>();
        return bpi => ExUtils.DictIfCondSetElseGet(data, Ex.Not(ExUtils.DictContains<uint, T>(data, bpi.id)), bpi.id, p(bpi));
    }

}
}