using DMK.Core;
using Ex = System.Linq.Expressions.Expression;
using ExBPRV2 = System.Func<DMK.Expressions.TExPI, DMK.Expressions.TEx<DMK.DMath.V2RV2>>;
using static DMK.Expressions.ExMHelpers;

namespace DMK.DMath.Functions {
/// <summary>
/// Functions that take in parametric information and return a V2RV2.
/// </summary>
public class BPRV2Repo {
    /// <summary>
    /// Return a constant V2RV2.
    /// </summary>
    /// <param name="rv2"></param>
    /// <returns></returns>
    [Fallthrough(1)]
    public static ExBPRV2 RV2(V2RV2 rv2) => bpi => ExC(rv2);
}

}