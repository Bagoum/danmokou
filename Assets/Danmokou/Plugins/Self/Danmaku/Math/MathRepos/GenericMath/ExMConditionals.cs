using DMK.Expressions;
using Ex = System.Linq.Expressions.Expression;
using static DMK.Expressions.ExMHelpers;
using tfloat = DMK.Expressions.TEx<float>;
using tbool = DMK.Expressions.TEx<bool>;
using tv2 = DMK.Expressions.TEx<UnityEngine.Vector2>;
using tv3 = DMK.Expressions.TEx<UnityEngine.Vector3>;
using trv2 = DMK.Expressions.TEx<DMK.DMath.V2RV2>;
using efloat = DMK.Expressions.EEx<float>;
using ev2 = DMK.Expressions.EEx<UnityEngine.Vector2>;
using ev3 = DMK.Expressions.EEx<UnityEngine.Vector3>;
using erv2 = DMK.Expressions.EEx<DMK.DMath.V2RV2>;
using ExBPY = System.Func<DMK.Expressions.TExPI, DMK.Expressions.TEx<float>>;
using ExPred = System.Func<DMK.Expressions.TExPI, DMK.Expressions.TEx<bool>>;

namespace DMK.DMath.Functions {
/// <summary>
/// See <see cref="Functions.ExM"/>. This class contains functions related to conditionals.
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