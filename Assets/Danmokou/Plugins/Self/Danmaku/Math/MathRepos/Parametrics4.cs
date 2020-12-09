using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using DMK.Core;
using Ex = System.Linq.Expressions.Expression;
using ExTP = System.Func<DMK.Expressions.TExPI, DMK.Expressions.TEx<UnityEngine.Vector2>>;
using ExTP3 = System.Func<DMK.Expressions.TExPI, DMK.Expressions.TEx<UnityEngine.Vector3>>;
using ExTP4 = System.Func<DMK.Expressions.TExPI, DMK.Expressions.TEx<UnityEngine.Vector4>>;
using ExFXY = System.Func<DMK.Expressions.TEx<float>, DMK.Expressions.TEx<float>>;
using ExBPY = System.Func<DMK.Expressions.TExPI, DMK.Expressions.TEx<float>>;
using ExPred = System.Func<DMK.Expressions.TExPI, DMK.Expressions.TEx<bool>>;
using static DMK.Expressions.ExMHelpers;

namespace DMK.DMath.Functions {
/// <summary>
/// Functions that return Vector4.
/// <br/>Note that these are also used for colors, in the format R G B A (0-1 floats).
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
public static partial class Parametrics4 {
    /// <summary>
    /// Derive a color from a vector3 containing R, G, B components. Alpha is set to 1.
    /// </summary>
    [Fallthrough(1)]
    public static ExTP4 TP3(ExTP3 tp) => bpi => ExMV4.TP3A(E1, tp(bpi));
}

}