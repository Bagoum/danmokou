using BagoumLib.Expressions;
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

namespace Danmokou.DMath.Functions {
/// <summary>
/// See <see cref="ExM"/>. This class contains functions related to mod (remainder) operations.
/// </summary>
[Reflect]
public static class ExMMod {
    /// <summary>
    /// Get the modulo (nonnegative) of one number by another. 
    /// </summary>
    /// <param name="x">Target value</param>
    /// <param name="by">Modulo value</param>
    /// <returns></returns>
    public static tfloat Mod(efloat by, efloat x) =>
        EEx.Resolve(x, by, (val, bym) => val.Sub(bym.Mul(Floor(val.Div(bym)))));

    /// <summary>
    /// = Mod(1, 1/phi * x)
    /// </summary>
    public static tfloat Modh(efloat x) => Mod(E1, iphi.Mul(x));
    
    /// <summary>
    /// Get the modulo (nonnegative) of one number by another in double precision. 
    /// </summary>
    /// <param name="x">Target value</param>
    /// <param name="by">Modulo value</param>
    /// <returns></returns>
    private static tfloat dMod(EEx<double> by, EEx<double> x) =>
        EEx.Resolve(x, by, (val, bym) => val.Sub(bym.Mul(dFloor(val.Div(bym)))));


    /// <summary>
    /// Periodize a value,
    /// "bouncing off" the endpoint instead of wrapping around.
    /// </summary>
    /// <example>
    /// <c>
    /// FSoftMod(X(), 4)(3.95) = 3.95
    /// FSoftMod(X(), 4)(4.05) = 3.95
    /// FSoftMod(X(), 4)(4.15) = 3.85
    /// </c>
    /// </example>
    /// <param name="by">Period</param>
    /// <param name="x">Value</param>
    /// <returns></returns>
    public static tfloat SoftMod(efloat by, efloat x) => EEx.Resolve(by, _by => {
        var vd = VFloat();
        return Ex.Block(new[] {vd},
            vd.Is(Mod(E2.Mul(_by), x)),
            Ex.Condition(vd.LT(_by), vd, E2.Mul(_by).Sub(vd))
        );
    });

    /// <summary>
    /// Periodize a value around a positive and negative endpoint.
    /// </summary>
    /// <example>
    /// <c>
    /// FSoftMod(X(), 4)(3.95) = 3.95
    /// FSoftMod(X(), 4)(4.05) = -3.95
    /// FSoftMod(X(), 4)(11) = 3
    /// FSoftMod(X(), 4)(12.05) = -3.95
    /// </c>
    /// </example>
    /// <param name="by"></param>
    /// <param name="x"></param>
    /// <returns></returns>
    public static tfloat RangeMod(efloat by, tfloat x) => EEx.Resolve(by, _by => Mod(E2.Mul(_by), x.Add(_by)).Sub(_by));

    /// <summary>
    /// = RangeMod(1, 2/phi * x)
    /// </summary>
    public static tfloat RangeModh(tfloat x) => RangeMod(E1, E2.Mul(iphi).Mul(x));
    
    /// <summary>
    /// Periodize a value, bouncing it off a positive and negative endpoint.
    /// </summary>
    /// <example>
    /// <c>
    /// FSoftMod(X(), 4)(3.95) = 3.95
    /// FSoftMod(X(), 4)(4.05) = 3.95
    /// FSoftMod(X(), 4)(11) = -3
    /// FSoftMod(X(), 4)(12.05) = -3.95
    /// </c>
    /// </example>
    /// <param name="by"></param>
    /// <param name="x"></param>
    /// <returns></returns>
    public static tfloat RangeSoftMod(efloat by, tfloat x) =>
        EEx.Resolve(by, _by => SoftMod(E2.Mul(_by), x.Add(_by)).Sub(_by));
    
    /// <summary>
    /// Periodize the return value of the target function with a "pause" at the value pauseAt for pauseLen units.
    /// The true period of this function is by + pauseLen, however the output only varies [0, by].
    /// During the pause time, the return value will be stuck at pauseAt.
    /// </summary>
    /// <param name="by">Naive period</param>
    /// <param name="pauseAt">Time at which to pause</param>
    /// <param name="pauseLen">Length for which to pause</param>
    /// <param name="x">Target function</param>
    /// <returns></returns>
    public static tfloat ModWithPause(tfloat by, efloat pauseAt, efloat pauseLen, tfloat x) =>
        EEx.Resolve(pauseAt, pauseLen, (pi, pl) => _ModWithPause(by, pi, pl, x));
    private static tfloat _ModWithPause(tfloat by, tfloat pauseAt, tfloat pauseLen, tfloat x) {
        var val = VFloat();
        return Ex.Block(new[] {val},
            val.Is(Mod(pauseLen.Add(by), x)),
            Ex.Condition(val.LT(pauseAt), val, 
                Ex.Condition(ExUtils.SubAssign(val, pauseLen).LT(pauseAt), pauseAt, val))
        );
    }

    /// <summary>
    /// Use this to draw "wings" where both go in the same direction.
    /// <br/>Odd by: 0 is the center, [1,by/2-0.5] are one wing, and [by/2+0.5,by) are the other.
    /// <br/>Even by: [0, by/2) are one wing, [by/2, by) are the other.
    /// </summary>
    /// <example>
    /// <c>
    /// HMod(X(), 9)(0) = HMod(X(), 9)(9) = 0
    /// HMod(X(), 9)(1) = 1
    /// HMod(X(), 9)(5) = 1
    /// HMod(X(), 9)(8) = 4
    /// HMod(X(), 8)(0) = HMod(X(), 8)(8) = HMod(X(), 8)(4) = 0
    /// HMod(X(), 8)(2) = 2
    /// HMod(X(), 8)(6) = 2
    /// </c>
    /// </example>
    /// <param name="by">Period (note all values are in the range [0, by/2-0.5]</param>
    /// <param name="x">Value</param>
    /// <returns></returns>
    public static tfloat HMod(tfloat by, tfloat x) => EEx.Resolve<float>(by.Div(E2), h => {
        var y = VFloat();
        return Ex.Block(new[] {y},
            y.Is(Mod(h.Mul(E2), x)),
            Ex.Condition(y.LT(h), y, y.Sub(Floor(h)))
        );
    });

    /// <summary>
    /// Use this to draw "wings" where both go in opposite directions.
    /// <br/>Odd by: 0 is the center, [1,by/2-0.5] are one wing, and [by/2+0.5,by) are the other.
    /// <br/>Even by: [0, by/2) are one wing, [by/2, by) are the other.
    /// </summary>
    /// <example>
    /// <c>
    /// HNMod(X(), 9)(0) = HNMod(X(), 9)(9) = 0
    /// HNMod(X(), 9)(1) = 1
    /// HNMod(X(), 9)(5) = -1
    /// HNMod(X(), 9)(8) = -4
    /// HNMod(X(), 8)(0) = HNMod(X(), 8)(8) = 0.5
    /// HNMod(X(), 8)(3) = 3.5
    /// HNMod(X(), 8)(4) = -0.5
    /// </c>
    /// </example>
    /// <param name="by">Period</param>
    /// <param name="x">Target function</param>
    /// <returns></returns>
    public static tfloat HNMod(tfloat by, tfloat x) => EEx.Resolve<float>(by.Div(E2), h => {
        var y = VFloat();
        return Ex.Block(new[] {y},
            y.Is(Mod(h.Mul(E2), x)),
            Ex.Condition(y.LT(h), y.Add(Floor(h)).Add(E05).Sub(h), h.Sub(E05).Sub(y))
        );
    });

    #region PSel
    
    /// <summary>
    /// Returns 1 if the value is even,
    /// and -1 if the value is odd.
    /// </summary>
    [Alias("pm1")]
    public static tfloat PM1Mod(tfloat x) => E1.Sub(E2.Mul(Mod(E2, x)));
    /// <summary>
    /// Returns -1 if the value is even,
    /// and 1 if the value is odd.
    /// </summary>
    [Alias("mp1")]
    public static tfloat MP1Mod(tfloat x) => E2.Mul(Mod(E2, x)).Sub(E1);
    /// <summary>
    /// Returns 0 if the value is even,
    /// and 1 if the value is odd.
    /// </summary>
    [Alias("z1")]
    public static tfloat z1Mod(tfloat x) => Mod(E2, x);
    /// <summary>
    /// Returns v if x is even,
    /// and 180-v if x is odd.
    /// </summary>
    public static tfloat FlipXMod(tfloat x, tfloat v) => ExC(90f).Add(PM1Mod(x).Mul(v.Sub(ExC(90f))));
    /// <summary>
    /// Returns v if x is 1,
    /// and 180-v if x is -1.
    /// </summary>
    public static tfloat FlipXPMMod(tfloat x, tfloat v) => ExC(90f).Add(x.Mul(v.Sub(ExC(90f))));

    /// <summary>
    /// Convert a value 1,-1 to 1,0.
    /// </summary>
    public static tfloat PMZ1(tfloat x) => E05.Add(E05.Mul(x));
    /// <summary>
    /// Convert a value 1,0 to 1,-1.
    /// </summary>
    public static tfloat Z1PM(tfloat x) => E2.Mul(x).Sub(E1);
    
    #endregion
    
    #region Remappers

    /// <summary>
    /// Use Fermat's Little Theorem to reindex integers around a prime number mod.
    /// </summary>
    public static tfloat RemapIndex(efloat mod, tfloat index) => EEx.Resolve(mod, m => Mod(m, index.Mul(m.Sub(E1))));
    
    /// <summary>
    /// Use Fermat's Little Theorem to reindex integers around a prime number mod, localized to the region
    /// [mod\*floor(index/mod), mod+mod\*floor(index/mod)].
    /// </summary>
    public static tfloat RemapIndexLoop(efloat mod, efloat index) => EEx.Resolve(mod, index, (m, i) => {
        var rem = VFloat();
        return Ex.Block(new[] {rem},
            rem.Is(Mod(m, i)),
            i.Sub(rem).Add(RemapIndex(m, rem))
        );
    });
    
    #endregion
}
}