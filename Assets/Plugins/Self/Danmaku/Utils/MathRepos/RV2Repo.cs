using System.Linq.Expressions;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;
using ExTP = System.Func<DMath.TExPI, TEx<UnityEngine.Vector2>>;
using ExTP3 = System.Func<DMath.TExPI, TEx<UnityEngine.Vector3>>;
using ExFXY = System.Func<TEx<float>, TEx<float>>;
using ExBPY = System.Func<DMath.TExPI, TEx<float>>;
using ExBPRV2 = System.Func<DMath.TExPI, TEx<DMath.V2RV2>>;
using ExPred = System.Func<DMath.TExPI, TEx<bool>>;
using static DMath.ExMHelpers;
using static ExUtils;
using FR = DMath.FXYRepo;

namespace DMath {
/// <summary>
/// Functions that take in parametric information and return a V2RV2.
/// </summary>
public class BPRV2Repo {
    /// <summary>
    /// Assign local variables that can be repeatedly used without reexecution. Shortcut: ::
    /// </summary>
    /// <param name="aliases">List of each variable and its assigned float value</param>
    /// <param name="inner">Code to execute within the scope of the variables</param>
    public static ExBPRV2 LetFloats((string, ExBPY)[] aliases, ExBPRV2 inner) => bpi => ReflectEx.Let(aliases, () => inner(bpi), bpi);
    /// <summary>
    /// Reference a value defined in a let function. Shortcut: &amp;
    /// </summary>
    /// <returns></returns>
    public static ExBPRV2 Reference(string alias) => ReflectEx.ReferenceLetBPI<V2RV2>(alias);
    /// <summary>
    /// Return a constant V2RV2.
    /// </summary>
    /// <param name="rv2"></param>
    /// <returns></returns>
    [Fallthrough(1)]
    public static ExBPRV2 RV2(V2RV2 rv2) => bpi => ExC(rv2);
}

}