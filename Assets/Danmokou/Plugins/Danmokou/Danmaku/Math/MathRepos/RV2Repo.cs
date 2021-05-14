using System;
using Danmokou.Core;
using Danmokou.Expressions;
using Ex = System.Linq.Expressions.Expression;
using static Danmokou.Expressions.ExMHelpers;

namespace Danmokou.DMath.Functions {
/// <summary>
/// Functions that take in parametric information and return a V2RV2.
/// </summary>
[Reflect]
public class BPRV2Repo {
    /// <summary>
    /// Return a constant V2RV2.
    /// </summary>
    /// <param name="rv2"></param>
    /// <returns></returns>
    [Fallthrough(1)]
    public static Func<TExArgCtx, TEx<V2RV2>> RV2(V2RV2 rv2) => bpi => ExC(rv2);
}

}