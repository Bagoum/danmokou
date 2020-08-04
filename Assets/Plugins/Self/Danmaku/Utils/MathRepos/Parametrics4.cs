using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using System.Linq.Expressions;
using Ex = System.Linq.Expressions.Expression;
using ExTP = System.Func<DMath.TExPI, TEx<UnityEngine.Vector2>>;
using ExTP3 = System.Func<DMath.TExPI, TEx<UnityEngine.Vector3>>;
using ExTP4 = System.Func<DMath.TExPI, TEx<UnityEngine.Vector4>>;
using ExFXY = System.Func<TEx<float>, TEx<float>>;
using ExBPY = System.Func<DMath.TExPI, TEx<float>>;
using ExPred = System.Func<DMath.TExPI, TEx<bool>>;
using static DMath.ExMHelpers;
using static ExUtils;
using FR = DMath.FXYRepo;
using static DMath.ExM;

namespace DMath {
/// <summary>
/// Functions that return V4.
/// <br/>Note that these are also used for colors, in the format R G B A (0-1 floats).
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
public static partial class Parametrics4 {
    /// <summary>
    /// Assign local variables that can be repeatedly used without reexecution. Shortcut: ::
    /// </summary>
    /// <param name="aliases">List of each variable and its assigned float value</param>
    /// <param name="inner">Code to execute within the scope of the variables</param>
    public static ExTP4 LetFloats((string, ExBPY)[] aliases, ExTP4 inner) => bpi => ReflectEx.Let(aliases, () => inner(bpi), bpi);
    /// <summary>
    /// Assign local variables that can be repeatedly used without reexecution. Shortcut: ::v2
    /// </summary>
    /// <param name="aliases">List of each variable and its assigned vector value</param>
    /// <param name="inner">Code to execute within the scope of the variables</param>
    public static ExTP4 LetV2s((string, ExTP)[] aliases, ExTP4 inner) => bpi => ReflectEx.Let(aliases, () => inner(bpi), bpi);

    /// <summary>
    /// Derive a color from a vector3 containing R, G, B components. Alpha is set to 1.
    /// </summary>
    [Fallthrough(1)]
    public static ExTP4 TP3(ExTP3 tp) => bpi => ExMV4.TP3A(E1, tp(bpi));
}

}