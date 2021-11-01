using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using System.Linq.Expressions;
using BagoumLib.Expressions;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Expressions;
using Danmokou.Graphics;
using Danmokou.Player;
using Danmokou.Reflection;
using Danmokou.Services;
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
using static Danmokou.DMath.Functions.ExMMod;

using ExBPY = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<float>>;
using ExPred = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<bool>>;
using ExTP = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<UnityEngine.Vector2>>;

namespace Danmokou.DMath.Functions {
/// <summary>
/// A repository for generic expression mathematics.
/// <br/>Most of the math library is implemented in the DMath classes beginning with ExM.
/// </summary>
[Reflect]
[SuppressMessage("ReSharper", "UnusedMethodReturnValue.Global")]
public static partial class ExM {
    #region Aliasing
    //I have type-generalized the code for Reference/Lets but it's not possible to turn them into math expressions.
    // The reason is because they require binding information before realizing the child expression.
    // This is actually what prevents me from trashing the Func<Ex, Ex> paradigm.

    /// <summary>
    /// Reference a value defined in a let function, or bound within a GCX, or bound within bullet data,
    /// or saved within bullet data.
    /// <br/>&amp;x = &amp; x = reference(x)
    /// </summary>
    /// <returns></returns>
    [Alias(Parser.SM_REF_KEY)]
    public static Func<TExArgCtx, TEx<T>> Reference<T>(string alias) => ReflectEx.ReferenceLet<T>(alias);
    
    [Alias("s" + Parser.SM_REF_KEY)]
    public static Func<TExArgCtx, TEx<T>> ReferenceSafe<T>(string alias, Func<TExArgCtx, TEx<T>> deflt) => b => 
        ReflectEx.ReferenceExpr(alias, b, deflt(b));
    
    [Alias("@")]
    public static Func<TExArgCtx, TEx<T>> RetrieveHoisted<T>(ReflectEx.Hoist<T> hoist, Func<TExArgCtx, TEx<float>> indexer) => 
        tac => hoist.Retrieve(indexer(tac), tac);
    [Alias("@0")]
    public static Func<TExArgCtx, TEx<T>> RetrieveHoisted0<T>(ReflectEx.Hoist<T> hoist) => 
        tac => hoist.Retrieve(E0, tac);

    /// <summary>
    /// Assign local variables that can be repeatedly used without reexecution via the Reference (&amp;) function.
    /// Shortcut: ::
    /// </summary>
    /// <param name="aliases">List of each variable and its assigned vector value</param>
    /// <param name="inner">Code to execute within the scope of the variables</param>
    [Alias("::")]
    public static Func<TExArgCtx, TEx<T>> LetFloats<T>((string, ExBPY)[] aliases, Func<TExArgCtx, TEx<T>> inner) => bpi => 
        ReflectEx.Let(aliases, () => inner(bpi), bpi);
    
    /// <summary>
    /// Assign local variables that can be repeatedly used without reexecution via the Reference (&amp;) function.
    /// Shortcut: ::v2
    /// </summary>
    /// <param name="aliases">List of each variable and its assigned vector value</param>
    /// <param name="inner">Code to execute within the scope of the variables</param>
    [Alias("::v2")]
    public static Func<TExArgCtx, TEx<T>> LetV2s<T>((string, ExTP)[] aliases, Func<TExArgCtx, TEx<T>> inner) => bpi => 
        ReflectEx.Let(aliases, () => inner(bpi), bpi);
    
    #endregion
    
    #region Components
    
    private static TExV2 Box(TEx<Vector2> ex) => new TExV2(ex);
    /// <summary>
    /// Get the x-component of a Vector2.
    /// </summary>
    [Alias(".x")]
    public static tfloat V2X(tv2 tp) => Box(tp).x;
    /// <summary>
    /// Get the y-component of a Vector2.
    /// </summary>
    [Alias(".y")]
    public static tfloat V2Y(tv2 tp) => Box(tp).y;
    private static TExRV2 Box(TEx<V2RV2> ex) => new TExRV2(ex);
    
    /// <summary>
    /// Get the nonrotational X-component of an RV2.
    /// </summary>
    /// <returns></returns>
    [Alias(".nx")]
    public static tfloat RV2NX(trv2 rv2) => Box(rv2).nx;
    /// <summary>
    /// Get the nonrotational Y-component of an RV2.
    /// </summary>
    /// <returns></returns>
    [Alias(".ny")]
    public static tfloat RV2NY(trv2 rv2)=> Box(rv2).ny;
    /// <summary>
    /// Get the rotational X-component of an RV2.
    /// </summary>
    /// <returns></returns>
    [Alias(".rx")]
    public static tfloat RV2RX(trv2 rv2) => Box(rv2).rx;
    /// <summary>
    /// Get the rotational Y-component of an RV2.
    /// </summary>
    /// <returns></returns>
    [Alias(".ry")]
    public static tfloat RV2RY(trv2 rv2) => Box(rv2).ry;
    /// <summary>
    /// Get the rotational angle of an RV2.
    /// </summary>
    /// <returns></returns>
    [Alias(".a")]
    public static tfloat RV2A(trv2 rv2) => Box(rv2).angle;
    
    
    //Used for additive parametrization.
    private const int SHIFT = 1 << 10;
    private static readonly Expression ExSHIFT = Ex.Constant(SHIFT);

    /// <summary>
    /// When two firing indices have been combined via additive parametrization (see <see cref="Core.Parametrization"/>), this retrieves the parent firing index.
    /// </summary>
    /// <returns></returns>
    public static Ex P1(Ex t) => P1M(SHIFT, t);
    /// <summary>
    /// When two firing indices have been combined via modular parametrization (see <see cref="Core.Parametrization"/>), this retrieves the parent firing index.
    /// </summary>
    /// <returns></returns>
    public static Ex P1M(int mod, Ex t) {
        return t.As<int>().Div(ExC(mod)).As<float>();
    }
    public static Ex exP1M(Ex mod, Ex t) {
        return t.As<int>().Div(mod.As<int>()).As<float>();
    }

    /// <summary>
    /// When two firing indices have been combined via additive parametrization (see <see cref="Core.Parametrization"/>), this retrieves the child firing index.
    /// </summary>
    /// <returns></returns>
    public static Ex P2(Ex t) => P2M(SHIFT, t);
    /// <summary>
    /// When two firing indices have been combined via modular parametrization (see <see cref="Core.Parametrization"/>), this retrieves the child firing index.
    /// </summary>
    /// <returns></returns>
    public static Ex P2M(int mod, Ex t) {
        return Ex.Modulo(t.As<int>(), ExC(mod)).As<float>();
    }
    
    public static Ex exP2M(Ex mod, Ex t) {
        return Ex.Modulo(t.As<int>(), mod.As<int>()).As<float>();
    }
    /// <summary>
    /// When two firing indices have been combined via modular or additive parametrization (see <see cref="Core.Parametrization"/>), this retrieves the firing index of any point in the chain.
    /// Roughly equivalent to mod SELF p1m CHILDREN.
    /// </summary>
    /// <param name="self">Mod size of the target point. Set to 0 to get the effect of additive parametrization.</param>
    /// <param name="children">Product of the mod sizes of all children. Set to 1 if this is the final point.</param>
    /// <param name="t">Index</param>
    /// <returns></returns>
    public static Ex PM(int self, int children, Ex t) {
        if (self == 0) self = SHIFT;
        return Ex.Modulo(t.As<int>().Div(ExC(children)), ExC(self)).As<float>();
    }
    
    public static Ex exPM(Ex self, Ex children, Ex t) {
        if (self is ConstantExpression {Value: int smod} && smod == 0) 
            self = ExC(SHIFT);
        return Ex.Modulo(t.As<int>().Div(children.As<int>()), self.As<int>()).As<float>();
    }
    public static int __Combine(int x, int y, int mod = SHIFT) {
        return (x * mod) + y;
    }


    #endregion

    #region RNG

    /// <summary>
    /// Returns a random number.
    /// This will return a random number every time it is called. It is unseeded. Do not use for movement functions.
    /// </summary>
    /// <param name="from">Minimum</param>
    /// <param name="to">Maximum</param>
    /// <returns></returns>
    public static tfloat Rand(tfloat from, tfloat to) => RNG.GetFloat(from, to);

    /// <summary>
    /// Randomly returns either -1 or 1.
    /// </summary>
    public static tfloat Randpm1() => Ex.Condition(Rand(EN1, E1).GT0(), E1, EN1);
    private static readonly ExFunction SeedRandInt = ExFunction.Wrap(typeof(RNG), "GetSeededFloat", new[] {typeof(float), typeof(float), typeof(int)});
    /// <summary>
    /// Returns a pseudorandom value based on the seed function.
    /// The seed function only has integer discrimination.
    /// </summary>
    /// <param name="from">Minimum</param>
    /// <param name="to">Maximum</param>
    /// <param name="seed">Seed function</param>
    /// <returns></returns>
    public static tfloat SRand(tfloat from, tfloat to, tfloat seed)  => SeedRandInt.Of(from, to, Ex.Convert(seed, typeof(int)));
    /// <summary>
    /// Returns either 0 or 1 based on the seed function.
    /// The seed function only has integer discrimination.
    /// </summary>
    /// <param name="seed">Seed function</param>
    /// <returns></returns>
    public static tfloat SRand01(tfloat seed)  => Ex.Condition(SRand(EN1, E1, seed).GT(E0), E1, E0);
    /// <summary>
    /// Returns either -1 or 1 based on the seed function.
    /// The seed function only has integer discrimination.
    /// </summary>
    /// <param name="seed">Seed function</param>
    /// <returns></returns>
    public static tfloat SRandpm1(tfloat seed)  => Ex.Condition(SRand(EN1, E1, seed).GT(E0), E1, EN1);
    
    #endregion

    #region Compositional

    /// <summary>
    /// Returns `c1*x1 + c2*x2`.
    /// </summary>
    /// <returns></returns>
    public static TEx<T> Superpose<T>(tfloat c1, TEx<T> x1, tfloat c2, TEx<T> x2) => c1.Mul(x1).Add(c2.Mul(x2));

    /// <summary>
    /// Returns `c*x1 + (1-c)*x2`.
    /// </summary>
    public static TEx<T> SuperposeC<T>(efloat c, TEx<T> x1, TEx<T> x2) =>
        EEx.Resolve(c, _c => _c.Mul(x1).Add(E1.Sub(c).Mul(x2)));

    /// <summary>
    /// Returns `1-opacity + opacity*x`.
    /// </summary>
    public static tfloat Opacity(efloat opacity, tfloat x) => EEx.Resolve(opacity, op => E1.Sub(op).Add(op.Mul(x)));

    #endregion
    
    #region Aggregators
    /// <summary>
    /// Calculate the softmax of several values ( (Sum xe^ax) / (Sum e^ax) )
    /// </summary>
    /// <param name="sharpness">The higher the absolute value of this, the more quickly the result will converge.
    /// Set negative for softmin.</param>
    /// <param name="against">Values</param>
    /// <returns></returns>
    public static tfloat Softmax(efloat sharpness, tfloat[] against) => EEx.Resolve(sharpness, sharp => {
        var num = V<double>();
        var denom = V<double>();
        var x = VFloat();
        var exp = V<double>();
        List<Ex> stmts = new List<Ex> { num.Is(denom.Is(ExC(0.0))) };
        for (int ii = 0; ii < against.Length; ++ii) {
            stmts.Add(x.Is(against[ii]));
            stmts.Add(exp.Is(ExpDb(x.Mul(sharp))));
            stmts.Add(ExUtils.AddAssign(num, x.As<double>().Mul(exp)));
            stmts.Add(ExUtils.AddAssign(denom, exp));
        }
        stmts.Add(num.Div(denom).As<float>());
        return Ex.Block(new[] {num, denom, x, exp}, stmts);
    });
    
    /// <summary>
    /// Calculate the logsum of several values ( (ln Sum e^ax) / a ), which is approximately equal to the largest number (smallest if sharpness is negative).
    /// </summary>
    /// <param name="sharpness">The higher the absolute value of this, the more quickly the result will converge.</param>
    /// <param name="against">Values</param>
    /// <returns></returns>
    public static tfloat Logsum(efloat sharpness, tfloat[] against)  => EEx.Resolve(sharpness, sharp => {
        var num = V<double>();
        List<Ex> stmts = new List<Ex> { num.Is(ExC(0.0)) };
        for (int ii = 0; ii < against.Length; ++ii) {
            stmts.Add(ExUtils.AddAssign(num, ExpDb(sharp.Mul(against[ii]))));
        }
        stmts.Add(((Ex)LnDb(num)).As<float>().Div(sharp));
        return Ex.Block(new[] {num}, stmts);
    });

    #endregion
    
    #region SineLikes

    /// <summary>
    /// A sine-like function (phase is that of cos) that quickly moves downwards
    /// on its falling sections, meant to simulate the slow flapping of wings.
    /// </summary>
    /// <remarks>
    /// See https://www.desmos.com/calculator/jeo6rrqzsd
    /// </remarks>
    /// <param name="period">Period</param>
    /// <param name="peakHeight">Peak height</param>
    /// <param name="x">Time</param>
    /// <returns></returns>
    public static tfloat SWing(efloat period, efloat peakHeight, tfloat x) => EEx.Resolve(period, peakHeight,
        (per, h) => {
            var pt = VFloat();
            return Ex.Block(new[] {pt},
                //Note the shift here to get cosine phase
                pt.Is(Mod(per, x.Add(per.Div(E2)))),
                Ex.Condition(pt.LT(per.Div(E2)),
                    h.Mul(Cos(pt.Mul(tau.Div(per)))).Neg(),
                    Ex.Negate(h).Add(h.Mul(ExC(-2f)).Mul(
                            Pow(pt.Mul(E2).Div(per).Sub(E2), ExC(3f))
                        ))
            ));
        });

    /// <summary>
    /// A better sine-like swing function (also cosine phase).
    /// <br/>See https://www.desmos.com/calculator/uwwfsslxrj
    /// </summary>
    /// <param name="halfwayRatio">Ratio of the period that the function is going from max to min.</param>
    /// <param name="period">Period of swing.</param>
    /// <param name="min">Minimum value.</param>
    /// <param name="max">Value at the beginning of the period (not actually the maximum value)</param>
    /// <param name="overshoot">The actual maximum value. The function rises from min to overshoot, then slowly returns to max.</param>
    /// <param name="time"></param>
    /// <returns></returns>
    public static tfloat SWing2(tfloat halfwayRatio, efloat period, efloat min, efloat max, efloat overshoot, tfloat time)
        => EEx.Resolve(period, min, max, overshoot, (per, h3, h2, h1) => {
            var pt = VFloat();
            var t1 = VFloat();
            var hm = VFloat();
            return Ex.Block(new[] {pt, t1, hm},
                t1.Is(per.Mul(halfwayRatio)),
                pt.Is(Mod(per, time)),
                hm.Is(E05.Mul(h1.Add(h3))),
                Ex.Condition(pt.LT(t1),
                    h3.Add(h2.Sub(h3).Mul(Pow(pt.Div(t1).Complement(), ExC(3)))),
                    hm.Sub(h1.Sub(h3).Div(E2).Mul(Cos(
                        pi.Add(ACosR(h2.Sub(hm).Div(h1.Sub(hm)))).Div(per.Sub(t1)).Mul(pt.Sub(t1)))))
                )
            );
            // xm = mod(x, t1 + t2)
            // sh = (h1 + h3) / 2
            // Ps = 2pi t2 / (pi + acos ((h2 - sh)/(h1 - sh)))
            // y = (xm > t1) ? 
            //        sh - (h1 - h3) / 2 * cos(2pi / Ps * (xm - t1))
            //        h3 + (h2 - h3) (1 - xm / t1)^3
            //ref https://www.desmos.com/calculator/uwwfsslxrj

        });
    
    
    
    #endregion
    
    #region ExtLinkers
    
    /// <summary>
    /// Get the time (in frames) of the given timer.
    /// </summary>
    public static tfloat Timer(ETime.Timer timer) => timer.exFrames;
    /// <summary>
    /// Get the time (in seconds) of the given timer.
    /// </summary>
    public static tfloat TimerSec(ETime.Timer timer) => timer.exSeconds;
    
    #endregion
    
    #region VerySpecific

    /// <summary>
    /// Returns the acceleration displacement function `h0 + v0*t + 0.5*g*t^2`.
    /// </summary>
    public static tfloat Height(tfloat h0, tfloat v0, tfloat g, efloat time) =>
        EEx.Resolve(time, t => h0.Add(v0.Mul(t)).Add(E05.Mul(g).Mul(t).Mul(t)));

    /// <summary>
    /// Find the radius of a regular polygon at a given ratio relative to one of its vertices (max radius).
    /// </summary>
    /// <param name="R">Max radius</param>
    /// <param name="n">Number of sides</param>
    /// <param name="theta">Angle, radians (0-2pi)</param>
    /// <returns></returns>
    public static tfloat RegPolyR(tfloat R, tfloat n, tfloat theta) {
        var f = VFloat();
        return Ex.Block(new[] {f},
            f.Is(pi.Div(n)),
            R.Mul(Cos(f)).Div(Cos(Mod(f.Mul(2), theta).Sub(f)))
        );
        // R cos(f) / cos( mod(2f, theta) - f)
    }

    /// <summary>
    /// Same as RegPolyR, with theta in degrees (0-360).
    /// </summary>
    public static tfloat RegPoly(tfloat R, tfloat n, tfloat theta) => RegPolyR(R, n, DegRad(theta));

    /// <summary>
    /// Find the radius of a regular star at a given ratio relative to one of its vertices (max radius).
    /// Ie. a polygram with n/2 "sides".
    /// Only works well for odd n.
    /// Draws the star by drawing straight lines between points, ie. there are line overlaps.
    /// </summary>
    /// <param name="R">Max radius</param>
    /// <param name="n">Number of points</param>
    /// <param name="theta">Angle (0-4pi) (2*2pi, this 2star requires two iterations)</param>
    /// <returns></returns>
    public static tfloat Reg2StarR(tfloat R, tfloat n, tfloat theta) =>
        RegPolyR(R, n.Div(E2), theta);

    /// <summary>
    /// Same as Reg2StarR, with theta in degrees (0-720).
    /// </summary>
    public static tfloat Reg2Star(tfloat R, tfloat n, tfloat theta) => Reg2StarR(R, n, DegRad(theta));

    /// <summary>
    /// Find the radius of a regular star at a given ratio relative to one of its vertices (max radius).
    /// Ie. a polygram with n/2 "sides".
    /// Only works well for odd n.
    /// Draws the star by drawing an outline, ie. there are no line overlaps.
    /// </summary>
    /// <param name="R">Max radius</param>
    /// <param name="n">Number of points</param>
    /// <param name="theta">Angle (0-2pi)</param>
    /// <returns></returns>
    public static tfloat RegSoftStarR(tfloat R, tfloat n, tfloat theta) =>
        RegPolyR(R, n.Div(E2), theta.Mul(E2));

    /// <summary>
    /// Same as RegSoftStarR, with theta in degrees (0-360).
    /// </summary>
    public static tfloat RegSoftStar(tfloat R, tfloat n, tfloat theta) => RegSoftStarR(R, n, DegRad(theta));

    #endregion
    
    #region Geometry
    
    /// <summary>
    /// Find the angle of fire such that a ray fired from the source bouncing off the wall X=W would hit the target.
    /// </summary>
    public static tfloat BounceX(tfloat w, ev2 source, ev2 target) => EEx.ResolveV2(source, target,
        (s, t) => ATan2(t.y.Sub(s.y), w.Mul(E2).Sub(s.x).Sub(t.x)));
    /// <summary>
    /// Find the angle of fire such that a ray fired from the source bouncing off the wall Y=W would hit the target.
    /// </summary>
    public static tfloat BounceY(tfloat w, ev2 source, ev2 target) => EEx.ResolveV2(source, target,
        (s, t) => ATan2(w.Mul(E2).Sub(s.y).Sub(t.y), t.x.Sub(s.x)));
    
    #endregion
    
    #region External

    /// <summary>
    /// Get the HP ratio (0-1) of the BehaviorEntity.
    /// <br/>The BEH must be an enemy, or this will cause errors.
    /// </summary>
    public static tfloat HPRatio(BEHPointer beh) =>
        ExC(beh).Field("beh").Field("Enemy").Field("EffectiveBarRatio");

    /// <summary>
    /// Get the number of photos taken of the given boss.
    /// <br/>The BEH must be an enemy, or this will cause errors.
    /// <br/>This number resets every card.
    /// </summary>
    public static tfloat PhotosTaken(BEHPointer beh) => 
        ExC(beh).Field("beh").Field("Enemy").Field("PhotosTaken");

    /// <summary>
    /// Returns true if the instance has not continued.
    /// </summary>
    public static tbool Is1CC() => Ex.Not(Ex.Property(null, typeof(GameManagement), "Continued"));

    public static Ex Instance => Ex.Property(null, typeof(GameManagement), "Instance");


    /// <summary>
    /// Returns the amount of time for which the player has *not* been focusing.
    /// Resets to zero while the player is focusing.
    /// </summary>
    public static ExBPY PlayerFreeT(Func<TExArgCtx, TEx<PlayerController>> p) => tac => p(tac).Field("TimeFree");
    /// <summary>
    /// Returns the amount of time for which the player has been focusing.
    /// Resets to zero while the player is not focusing.
    /// </summary>
    public static ExBPY PlayerFocusT(Func<TExArgCtx, TEx<PlayerController>> p) => tac => p(tac).Field("TimeFocus");
    /// <summary>
    /// Returns the amount of time for which the player has been firing.
    /// Resets to zero while the player is not firing.
    /// </summary>
    public static ExBPY PlayerFiringT(Func<TExArgCtx, TEx<PlayerController>> p) => tac => p(tac).Field("FiringTime");
    /// <summary>
    /// Returns the amount of time for which the player has been firing while *not* focusing.
    /// Resets to zero while the player is not firing or is focusing.
    /// </summary>
    public static ExBPY PlayerFiringFreeT(Func<TExArgCtx, TEx<PlayerController>> p) => tac => p(tac).Field("FiringTimeFree");
    /// <summary>
    /// Returns the amount of time for which the player has been firing while focusing.
    /// Resets to zero while the player is not firing or is not focusing.
    /// </summary>
    public static ExBPY PlayerFiringFocusT(Func<TExArgCtx, TEx<PlayerController>> p) => tac => p(tac).Field("FiringTimeFocus");
    /// <summary>
    /// Returns the amount of time for which the player has *not* been firing.
    /// Resets to zero while the player is firing.
    /// </summary>
    public static ExBPY PlayerUnFiringT(Func<TExArgCtx, TEx<PlayerController>> p) => tac => p(tac).Field("UnFiringTime");
    /// <summary>
    /// Returns the amount of time for which the player has *not* been firing or been focusing.
    /// Resets to zero while the player is firing AND *not* focusing.
    /// </summary>
    public static ExBPY PlayerUnFiringFreeT(Func<TExArgCtx, TEx<PlayerController>> p) => tac => p(tac).Field("UnFiringTimeFree");
    /// <summary>
    /// Returns the amount of time for which the player has *not* been firing or *not* been focusing.
    /// Resets to zero while the player is firing AND focusing.
    /// </summary>
    public static ExBPY PlayerUnFiringFocusT(Func<TExArgCtx, TEx<PlayerController>> p) => tac => p(tac).Field("UnFiringTimeFocus");
    
    public static ExBPY PlayerID(Func<TExArgCtx, TEx<PlayerController>> p) =>
        tac => PlayerController.playerID.InstanceOf(p(tac));

    public static ExBPY PlayerLerpFreeToFocus(Func<TExArgCtx, TEx<PlayerController>> p, ExBPY over) => 
        tac => Clamp01(PlayerFocusT(p)(tac).Div(over(tac)));
    public static ExTP PlayerPastPos(Func<TExArgCtx, TEx<PlayerController>> p, ExBPY ago) => 
        tac => PlayerController.pastPosition.InstanceOf(p(tac), ago(tac));
    public static ExTP PlayerMarisaAPos(Func<TExArgCtx, TEx<PlayerController>> p, ExBPY ago) => 
        tac => PlayerController.marisaAPosition.InstanceOf(p(tac), ago(tac));
    public static ExTP PlayerPastDir(Func<TExArgCtx, TEx<PlayerController>> p, ExBPY ago) => 
        tac => PlayerController.pastDirection.InstanceOf(p(tac), ago(tac));
    public static ExTP PlayerMarisaADir(Func<TExArgCtx, TEx<PlayerController>> p, ExBPY ago) => 
        tac => PlayerController.marisaADirection.InstanceOf(p(tac), ago(tac));

    //These types are not funcified, so they need to be explicit
    
    /// <summary>
    /// Returns true if the laser is colliding with an enemy (only applicable to player lasers).
    /// </summary>
    public static ExPred LaserColliding(Func<TExArgCtx, TEx<CurvedTileRenderLaser>> ctr) => tac => 
        ctr(tac).Field("playerBulletIsColliding");
    
    /// <summary>
    /// Returns the last active time of the laser. This is the first time at which the "deactivate" option
    /// on the laser returns true. If the deactivate option does not exist or has not yet returned true,
    /// this returns "effectively infinity".
    /// </summary>
    public static ExBPY LaserLastActiveT(Func<TExArgCtx, TEx<CurvedTileRenderLaser>> ctr) => tac => 
        ctr(tac).Field("LastActiveTime");
    
    
    /// <summary>
    /// Returns the location of the FireOption. Primarily used for player lasers.
    /// </summary>
    public static ExTP OptionLocation(Func<TExArgCtx, TEx<FireOption>> ctr) => tac => 
        ctr(tac).Field("Loc");
    
    /// <summary>
    /// Returns the direction of the FireOption. Primarily used for player lasers.
    /// </summary>
    public static ExBPY OptionAngle(Func<TExArgCtx, TEx<FireOption>> ctr) => tac => 
        ctr(tac).Field("original_angle");

    /// <summary>
    /// Return the player's power value.
    /// </summary>
    public static tfloat Power() => Instance.Field("Power");
    
    /// <summary>
    /// Return the player's power value, floored.
    /// </summary>
    public static tfloat PowerF() => Floor(Instance.Field("Power"));
    
    /// <summary>
    /// Return the player's power index.
    /// </summary>
    public static tfloat PowerIndex() => Floor(Instance.Field("PowerIndex"));

    /// <summary>
    /// If the player's power (floored) is strictly than the firing index,
    /// return the child, otherwise return zero.
    /// </summary>
    public static Func<TExArgCtx, TEx<T>> IfPowerGTP<T>(Func<TExArgCtx, TEx<T>> inner) =>
        b => Ex.Condition(PowerF().GT(b.findex), inner(b), ExC(default(T)!));
    

    /// <summary>
    /// Returns the object of type T associated with the object calling this function.
    /// <br/>eg. If this is used by a laser fired by a player option, and T = FireOption,
    /// then this function returns the FireOption that created this laser.
    /// </summary>
    /// <typeparam name="T">One of CurvedTileRenderLaser, PlayerInput, FireOption</typeparam>
    /// <returns></returns>
    public static Func<TExArgCtx, TEx<T>> Mine<T>() => tac => {
        var t = typeof(T);
        var fctx = tac.FCTX;
        if (t == typeof(CurvedTileRenderLaser)) {
            return fctx.Field("LaserController");
        } else if (t == typeof(PlayerController)) {
            return fctx.Field("PlayerController");
        } else if (t == typeof(FireOption)) {
            return fctx.Field("OptionFirer");
        }
        throw new Exception($"FCTX has no handling for `Mine` constructor of type {t.RName()}");
    };
    
    /*
    [Alias("mine?")]
    public static Func<TExArgCtx, TEx<T>> MineOrNull<T>() => tac => {
        var t = typeof(T);
        var fctx = tac.FCTX;
        if (t == typeof(CurvedTileRenderLaser)) {
            return fctx.Field("laserController");
        } else if (t == typeof(PlayerInput)) {
            return fctx.Field("playerController");
        }
        throw new Exception($"FCTX has no handling for `Mine?` constructor of type {t.RName()}");
    };*/

    #endregion
}
}