using BagoumLib.Expressions;
using Danmokou.Core;
using Danmokou.Expressions;
using Scriptor;
using Scriptor.Expressions;
using Ex = System.Linq.Expressions.Expression;
using static Scriptor.Expressions.ExMHelpers;
using tfloat = Scriptor.Expressions.TEx<float>;
using tbool = Scriptor.Expressions.TEx<bool>;
using static Danmokou.DMath.Functions.ExMMod;

namespace Danmokou.DMath.Functions {
/// <summary>
/// Functions that return boolean values.
/// </summary>
[Reflect]
public static partial class ExMPred {
    /// <summary>
    /// Return true.
    /// </summary>
    /// <returns></returns>
    [Alias("persist")] [Alias("_")] [Atomic]
    public static tbool True() => Ex.Constant(true);
    /// <summary>
    /// Returns false.
    /// </summary>
    /// <returns></returns>
    [Alias("once")] [Atomic]
    public static tbool False()  => Ex.Constant(false);

    /// <summary>
    /// Return true iff the first argument is strictly between the second and third.
    /// </summary>
    /// <param name="b">First BPY function</param>
    /// <param name="br1">Lower bound BPY function</param>
    /// <param name="br2">Upper bound BPY function</param>
    /// <returns></returns>
    [Operator]
    public static tbool In(tfloat b, tfloat br1, tfloat br2) {
        var f = ExUtils.VFloat();
        return Ex.Block(new[] {f},
            Ex.Assign(f, b),
            Ex.AndAlso(
                Ex.GreaterThan(f, br1),
                Ex.LessThan(f, br2)
            )
        );
    }

    public static tbool DivBy(tfloat by, tfloat x) => E0.Eq(Mod(by, x));
    /// <summary>
    /// Returns true if the number is even.
    /// </summary>
    public static tbool Even(tfloat b) => E0.Eq(z1Mod(b));
    /// <summary>
    /// Returns true if the number is odd.
    /// </summary>
    public static tbool Odd(tfloat b) => E1.Eq(z1Mod(b));
}
}