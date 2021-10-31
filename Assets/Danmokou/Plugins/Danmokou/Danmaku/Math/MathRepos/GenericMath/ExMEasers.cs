using System;
using System.Linq.Expressions;
using BagoumLib.Expressions;
using BagoumLib.Mathematics;
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
    //Signatures are tfloat => tfloat even when EEx.Resolve is required, in order to enable LookupMethod.
    
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

    public static tfloat EInQuad(tfloat x) => EEx.ResolveF(x, y => y.Mul(y));
    public static tfloat EOutQuad(tfloat x) => EEx.ResolveF(E1.Sub(x), y => E1.Sub(y.Mul(y)));
    public static tfloat EIOQuad(tfloat x) => EEx.ResolveF(E2.Mul(x), y =>
        Ex.Condition(y.LT(E1),
            E05.Mul(y).Mul(y),
            E1.Sub(E05.Mul(y.Sub(E2)).Mul(y.Sub(E2)))
        )
    );

    public static tfloat EInQuart(tfloat x) => EEx.ResolveF(x, y => y.Mul(y).Mul(y).Mul(y));
    public static tfloat EOutQuart(tfloat x) => EEx.ResolveF(E1.Sub(x), y => E1.Sub(y.Mul(y).Mul(y).Mul(y)));
    public static tfloat EIOQuart(tfloat x) => EEx.ResolveF(E2.Mul(x), y =>
        Ex.Condition(y.LT(E1),
            E05.Mul(y).Mul(y).Mul(y).Mul(y),
            E1.Sub(E05.Mul(y.Sub(E2)).Mul(y.Sub(E2)).Mul(y.Sub(E2)).Mul(y.Sub(E2)))
        )
    );

    private static readonly Ex BackElast = Ex.Constant(Easers.BackElasticity);
    private static readonly Ex IOBackElast = Ex.Constant(Easers.IOBackElasticity);
    public static tfloat EInBack(tfloat x) => CEInBack(BackElast, x);
    public static tfloat EOutBack(tfloat x) => CEOutBack(BackElast, x);
    public static tfloat EIOBack(tfloat x) => CEIOBack(IOBackElast, x);

    public static tfloat EInElastic(tfloat x) => EEx.ResolveF(x, y => 
        Pow(E2, y.Mul(10).Sub(10)).Mul(Sin(y.Sub(1.075f).Mul(pi).Div(-0.15f))));
    public static tfloat EOutElastic(tfloat x) => EEx.ResolveF(x, y => 
        Pow(E2, y.Mul(-10)).Mul(Sin(y.Sub(.075f).Mul(pi).Div(0.15f))).Add(E1));
    public static tfloat EIOElastic(tfloat x) => EEx.ResolveF(x, y => 
        Ex.Condition(y.LT(E05),
            Pow(E2, y.Mul(20).Sub(11))
                .Mul(Sin(y.Mul(E2).Sub(1.1f).Mul(pi)
                    .Div(-0.2f))),
            E1.Sub(Pow(E2, ExC(9f).Sub(y.Mul(20)))
                .Mul(Sin(ExC(1.9f).Sub(y.Mul(E2)).Mul(pi)
                    .Div(0.2f))))
        ));

    public static tfloat EInBounce(tfloat x) => E1.Sub(EOutBounce(E1.Sub(x)));
    public static tfloat EOutBounce(tfloat x) => EEx.ResolveF(x, y => 
        Ex.Condition(y.LT(ExC(1/3f)),
            y.Mul(y).Mul(9f),
            Ex.Condition(y.LT(ExC(2/3f)),
                y.Sub(0.5f).Mul(y.Sub(0.5f)).Mul(9f).Add(0.75f),
                Ex.Condition(y.LT(ExC(5.3f/6)),
                    y.Sub(4.65f/6).Mul(y.Sub(4.65f/6)).Mul(9f).Add(.894375f),
                    y.Sub(5.65f/6).Mul(y.Sub(5.65f/6)).Mul(9f).Add(.969375f)
                )
            )
        ));
    public static tfloat EIOBounce(tfloat x) => EEx.ResolveF(x, y => 
        Ex.Condition(y.LT(E05),
            E05.Sub(E05.Mul(EOutBounce(E1.Sub(y.Mul(2))))),
            E05.Add(E05.Mul(EOutBounce(y.Mul(2).Sub(1))))
        ));


    /// <summary>
    /// Linear easing function (ie. y = x).
    /// </summary>
    public static tfloat ELinear(tfloat x) => x;

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

    
    public static tfloat CEInBack(efloat p1, efloat x) => EEx.Resolve(p1, x, (p, y) => y.Mul(y).Mul(
        E1.Add(p).Mul(y).Sub(p)
    ));
    public static tfloat CEOutBack(efloat p1, tfloat x) => EEx.Resolve<float, float>(p1, x.Sub(E1), (p, y) => y.Mul(y).Mul(
        E1.Add(p).Mul(y).Add(p)
    ).Add(E1));
    public static tfloat CEIOBack(efloat p1, tfloat x) => EEx.Resolve<float, float>(p1, E2.Mul(x), (p, y) =>
        Ex.Condition(y.LT(E1),
            E05.Mul(y).Mul(y).Mul(E1.Add(p).Mul(y).Sub(p)),
            E1.Sub(E05.Mul(y.Sub(E2)).Mul(y.Sub(E2)).Mul(EN1.Sub(p).Mul(y.Sub(E2)).Sub(p)))
        )
    );

    #endregion

}
}