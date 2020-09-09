using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using Core;
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