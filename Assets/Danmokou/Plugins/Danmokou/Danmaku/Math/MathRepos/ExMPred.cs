using BagoumLib.Expressions;
using Danmokou.Core;
using Danmokou.Expressions;
using Ex = System.Linq.Expressions.Expression;
using static Danmokou.Expressions.ExMHelpers;
using tfloat = Danmokou.Expressions.TEx<float>;
using tbool = Danmokou.Expressions.TEx<bool>;
using tv2 = Danmokou.Expressions.TEx<UnityEngine.Vector2>;
using tv3 = Danmokou.Expressions.TEx<UnityEngine.Vector3>;
using trv2 = Danmokou.Expressions.TEx<Danmokou.DMath.V2RV2>;
using efloat = Danmokou.Expressions.EEx<float>;
using ev2 = Danmokou.Expressions.EEx<UnityEngine.Vector2>;
using ev3 = Danmokou.Expressions.EEx<UnityEngine.Vector3>;
using erv2 = Danmokou.Expressions.EEx<Danmokou.DMath.V2RV2>;
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
    [Alias("persist")]
    [Alias("_")]
    public static tbool True() => Ex.Constant(true);
    /// <summary>
    /// Returns false.
    /// </summary>
    /// <returns></returns>
    [Alias("once")]
    public static tbool False()  => Ex.Constant(false);

    /// <summary>
    /// Return true iff the argument is false.
    /// </summary>
    public static tbool Not(tbool pred) => Ex.Not(pred);
    /// <summary>
    /// Return true iff both arguments are true.
    /// </summary>
    /// <param name="pr1">First predicate</param>
    /// <param name="pr2">Second predicate</param>
    /// <returns></returns>
    [Alias("&")] [WarnOnStrict]
    public static tbool And(tbool pr1, tbool pr2) {
        return Ex.AndAlso(pr1, pr2);
    }
    /// <summary>
    /// Return true iff one or more arguments are true.
    /// </summary>
    /// <param name="pr1">First predicate</param>
    /// <param name="pr2">Second predicate</param>
    /// <returns></returns>
    [Alias("|")] [WarnOnStrict]
    public static tbool Or(tbool pr1, tbool pr2) {
        return Ex.OrElse(pr1, pr2);
    }
    
    /// <summary>
    /// Return true iff the first argument is equal to the second.
    /// </summary>
    [Alias("=")]
    public static tbool Eq(tfloat b1, tfloat b2) => Ex.Equal(b1, b2);
    
    /// <summary>
    /// Return true iff the first argument is not equal to the second.
    /// </summary>
    [Alias("=/=")]
    public static tbool Neq(tfloat b1, tfloat b2) => Ex.NotEqual(b1, b2);


    /// <summary>
    /// Return true iff the first argument is greater than the second.
    /// </summary>
    /// <param name="b1">First BPY function</param>
    /// <param name="b2">Second BPY function</param>
    /// <returns></returns>
    [Alias(">")]
    public static tbool Gt(tfloat b1, tfloat b2) {
        return Ex.GreaterThan(b1, b2);
    }
    /// <summary>
    /// Return true iff the first argument is greater than or equal to the second.
    /// </summary>
    /// <param name="b1">First BPY function</param>
    /// <param name="b2">Second BPY function</param>
    /// <returns></returns>
    [Alias(">=")]
    public static tbool Geq(tfloat b1, tfloat b2) {
        return Ex.GreaterThanOrEqual(b1, b2);
    }
    /// <summary>
    /// Return true iff the first argument is less than the second.
    /// </summary>
    /// <param name="b1">First BPY function</param>
    /// <param name="b2">Second BPY function</param>
    /// <returns></returns>
    [Alias("<")]
    public static tbool Lt(tfloat b1, tfloat b2) {
        return Ex.LessThan(b1, b2);
    }
    /// <summary>
    /// Return true iff the first argument is less than or equal to the second.
    /// </summary>
    /// <param name="b1">First BPY function</param>
    /// <param name="b2">Second BPY function</param>
    /// <returns></returns>
    [Alias("<=")]
    public static tbool Leq(tfloat b1, tfloat b2) {
        return Ex.LessThanOrEqual(b1, b2);
    }
    /// <summary>
    /// Return true iff the first argument is strictly between the second and third.
    /// </summary>
    /// <param name="b">First BPY function</param>
    /// <param name="br1">Lower bound BPY function</param>
    /// <param name="br2">Upper bound BPY function</param>
    /// <returns></returns>
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