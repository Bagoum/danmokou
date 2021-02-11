using System;
using System.Linq.Expressions;
using DMK.Core;
using DMK.Expressions;
using Ex = System.Linq.Expressions.Expression;
using static DMK.Expressions.ExUtils;
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
using static DMK.DMath.Functions.ExM;
using static DMK.DMath.Functions.ExMConditionals;
using static DMK.DMath.Functions.ExMConversions;
using static DMK.DMath.Functions.ExMMod;
using ExBPY = System.Func<DMK.Expressions.TExArgCtx, DMK.Expressions.TEx<float>>;

namespace DMK.DMath.Functions {
/// <summary>
/// See <see cref="DMK.DMath.Functions.ExM"/>. This class contains easing functions.
/// </summary>
[Reflect]
public static class ExMEasers {
    /// <summary>
    /// In-Sine easing function.
    /// </summary>
    [Alias("in-sine")]
    public static tfloat EInSine(tfloat x) => E1.Sub(Cos(hpi.Mul(x)));
    /// <summary>
    /// Out-Sine easing function.
    /// </summary>
    [Alias("out-sine")]
    public static tfloat EOutSine(tfloat x) => Sin(hpi.Mul(x));
    /// <summary>
    /// In-Out-Sine easing function.
    /// </summary>
    [Alias("io-sine")]
    public static tfloat EIOSine(tfloat x) => E05.Sub(E05.Mul(Cos(pi.Mul(x))));
    /// <summary>
    /// Linear easing function (ie. y = x).
    /// </summary>
    public static tfloat ELinear(tfloat x) => x;
    /// <summary>
    /// In-Quad easing function.
    /// </summary>
    public static tfloat EInQuad(tfloat x) => Sqr(x);
    /// <summary>
    /// Sine easing function with 010 pattern.
    /// </summary>
    public static tfloat ESine010(tfloat x) => Sin(pi.Mul(x));
    /// <summary>
    /// Softmod easing function with 010 pattern.
    /// </summary>
    [Alias("smod-010")]
    public static tfloat ESoftmod010(tfloat x) => Mul(E2, SoftMod(E05, x));

    public static tfloat EBounce2(tfloat x) => EEx.Resolve<float>((Ex)x, c => {
        var c1 = VFloat();
        var c2 = VFloat();
        return Ex.Block(new[] {c1, c2},
            c1.Is(Min(E05, c.Mul(ExC(0.95f)))),
            c2.Is(Max(E0, c.Sub(E05))),
            c1.Add(c2).Add(ExC(0.4f).Mul(
                    Sin(tau.Mul(c1)).Add(Sin(tau.Mul(c2)))
                ))
        );
    }); //https://www.desmos.com/calculator/ix37mllnyp

}
}