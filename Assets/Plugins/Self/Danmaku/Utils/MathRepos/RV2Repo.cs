using Core;
using Ex = System.Linq.Expressions.Expression;
using ExBPRV2 = System.Func<DMath.TExPI, TEx<DMath.V2RV2>>;
using static DMath.ExMHelpers;
using FR = DMath.FXYRepo;

namespace DMath {
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