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
/// See <see cref="DMath.ExM"/>. This class contains functions related to conditionals.
/// </summary>
public static class ExMConditionals {

    /// <summary>
    /// Convert a boolean into a 1/0 value.
    /// </summary>
    public static tfloat Pred10(tbool pred) => Ex.Condition(pred, E1, E0);
    /// <summary>
    /// If the predicate is true, return the true branch, otherwise the false branch.
    /// </summary>
    public static TEx<T> If<T>(tbool pred, TEx<T> iftrue, TEx<T> iffalse) => Ex.Condition(pred, iftrue, iffalse);

    /// <summary>
    /// If the switcher is nonzero, return the result, otherwise the default value.
    /// </summary>
    public static TEx<T> IfN0<T>(tfloat switcher, TEx<T> result) =>
        Ex.Condition(Ex.Equal(switcher, E0), Ex.Default(typeof(T)), result);
    /// <summary>
    /// If the switcher is zero, return the result, otherwise the default value.
    /// </summary>
    public static TEx<T> If0<T>(tfloat switcher, TEx<T> result) =>
        Ex.Condition(Ex.Equal(switcher, E0), result, Ex.Default(typeof(T)));
    /// <summary>
    /// If the switcher is not 1, return the result, otherwise the default value.
    /// </summary>
    public static TEx<T> IfN1<T>(tfloat switcher, TEx<T> result) =>
        Ex.Condition(Ex.Equal(switcher, E1), Ex.Default(typeof(T)), result);
    /// <summary>
    /// If the switcher is 1, return the result, otherwise the default value.
    /// </summary>
    public static TEx<T> If1<T>(tfloat switcher, TEx<T> result) =>
        Ex.Condition(Ex.Equal(switcher, E1), result, Ex.Default(typeof(T)));


}
}