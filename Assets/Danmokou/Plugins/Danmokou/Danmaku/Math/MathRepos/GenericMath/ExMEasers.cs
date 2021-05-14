using System;
using System.Linq.Expressions;
using Danmokou.Core;
using Danmokou.Expressions;
using Ex = System.Linq.Expressions.Expression;
using static Danmokou.Expressions.ExUtils;
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
using static Danmokou.DMath.Functions.ExM;
using static Danmokou.DMath.Functions.ExMConditionals;
using static Danmokou.DMath.Functions.ExMConversions;
using static Danmokou.DMath.Functions.ExMMod;
using ExBPY = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<float>>;

namespace Danmokou.DMath.Functions {
/// <summary>
/// See <see cref="Danmokou.DMath.Functions.ExM"/>. This class contains easing functions.
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


    //These functions aren't 0-1 contortions, or they take multiple arguments.
    #region EaseLikes
    
    /// <summary>
    /// Sine easing function with 010 pattern.
    /// </summary>
    public static tfloat ESine010(tfloat x) => Sin(pi.Mul(x));
    /// <summary>
    /// Softmod easing function with 010 pattern.
    /// </summary>
    [Alias("smod-010")]
    public static tfloat ESoftmod010(tfloat x) => Mul(E2, SoftMod(E05, x));

    /// <summary>
    /// Overshoot 1 and then ease back.
    /// </summary>
    /// <param name="p1">Parameter controlling overshoot</param>
    /// <param name="x">0-1 time</param>
    public static tfloat EOutBack(efloat p1, efloat x) => EEx.Resolve<float, float>(p1, Ex.Subtract(x, E1), (a, y) =>
        E1.Add(a.Add(E1).Mul(y).Mul(y).Mul(y)).Add(a.Mul(y).Mul(y))
    );

    #endregion

}
}