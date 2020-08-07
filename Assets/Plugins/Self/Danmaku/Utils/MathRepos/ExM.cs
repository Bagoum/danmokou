using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Linq.Expressions;
using Danmaku;
using JetBrains.Annotations;
using Ex = System.Linq.Expressions.Expression;
using static ExUtils;
using static DMath.ExMHelpers;
using tfloat = TEx<float>;
using tbool = TEx<bool>;
using tv2 = TEx<UnityEngine.Vector2>;
using tv3 = TEx<UnityEngine.Vector3>;
using trv2 = TEx<DMath.V2RV2>;
using efloat = DMath.EEx<float>;
using ev2 = DMath.EEx<UnityEngine.Vector2>;
using ev3 = DMath.EEx<UnityEngine.Vector3>;
using erv2 = DMath.EEx<DMath.V2RV2>;
using static GameManagement;

namespace DMath {
/// <summary>
/// A repository for purely expression-based mathematics.
/// <br/>All functions here can be used by FXY/BPY/TP as long as the return types match.
/// <br/>Because this repository deals with expression-based mathematics, it does not cover complex usages which
/// involve regenerating expressions over contorted inputs. See Ease, PivotShift.
/// </summary>
public static partial class ExM {
    #region Aliasing
    //Can't use lets here since they require performing bindings before children are resolved.
    //If I could put lets here and also figure out how to do pivoting, I could get rid of the entire Func<Ex,Ex> paradigm...

    /// <summary>
    /// Reference a value defined in a let function.
    /// </summary>
    /// <returns></returns>
    /// This only works for FXY, since BPY/TP must also pass the bpi object for private hoisting access
    [Alias(Parser.SM_REF_KEY)]
    public static TEx<T> Reference<T>(string alias) => ReflectEx.ReferenceExpr<T>(alias, null);
    [Alias("@")]
    public static TEx<T> RetrieveHoisted<T>(ReflectEx.Hoist<T> hoist, tfloat indexer) => hoist.Retrieve(indexer);
    [Alias("@0")]
    public static TEx<T> RetrieveHoisted0<T>(ReflectEx.Hoist<T> hoist) => hoist.Retrieve(E0);
    
    
    #endregion
    
    #region BasicFunctions
    
    /// <summary>
    /// The constant pi/2.
    /// </summary>
    /// <returns></returns>
    public static tfloat HPi() => hpi;
    /// <summary>
    /// The constant pi.
    /// </summary>
    /// <returns></returns>
    public static tfloat Pi() => pi;
    /// <summary>
    /// The constant pi*2.
    /// </summary>
    /// <returns></returns>
    public static tfloat Tau() => tau;
    /// <summary>
    /// The constant pi*4.
    /// </summary>
    /// <returns></returns>
    public static tfloat Twau() => twau;
    
    /// <summary>
    /// Add two vectypes.
    /// </summary>
    [Alias("+")]
    public static TEx<T> Add<T>(TEx<T> x, TEx<T> y) => x.Add(y);
    /// <summary>
    /// Add to the nonrotational components of an RV2.
    /// </summary>
    public static trv2 AddNV(trv2 rv2, tv2 nv) => Ex.Add(rv2, nv);
    /// <summary>
    /// Add to the angle of an RV2.
    /// </summary>
    public static trv2 AddA(trv2 rv2, tfloat ang) => Ex.Add(rv2, ang);
    /// <summary>
    /// Add to the nonrotational components and angle of an RV2.
    /// </summary>
    public static trv2 AddNVA(trv2 rv2, tv2 nv, tfloat ang) => Ex.Add(Ex.Add(rv2, nv), ang);
    /// <summary>
    /// Add to the rotational components and angle of an RV2.
    /// </summary>
    public static trv2 AddRVA(trv2 rv2, tv3 rva) => Ex.Add(rv2, rva);
    /// <summary>
    /// Add to the rotational components of an RV2.
    /// </summary>
    public static trv2 AddRV(trv2 rv2, tv2 rv) => Ex.Add(rv2, Ex.Convert(rv, typeof(Vector3)));

    /// <summary>
    /// Subtract two vectypes.
    /// </summary>
    [Alias("-")]
    public static TEx<T> Sub<T>(TEx<T> x, TEx<T> y) => x.Sub(y);
    /// <summary>
    /// Multiply a vectype by a number.
    /// </summary>
    [Alias("*")]
    public static TEx<T> Mul<T>(tfloat x, TEx<T> y) => x.Mul(y);

    /// <summary>
    /// Convert a number from degrees to radians.
    /// </summary>
    /// <param name="x"></param>
    /// <returns></returns>
    public static tfloat DegRad(tfloat x) => x.Mul(degRad);

    /// <summary>
    /// Convert a number from radians to degrees.
    /// </summary>
    /// <param name="x"></param>
    /// <returns></returns>
    public static tfloat RadDeg(tfloat x) => x.Mul(radDeg);
    /// <summary>
    /// Divide two numbers. Alias: / x y
    /// </summary>
    /// <returns></returns>
    [Alias("/")]
    public static tfloat Div(tfloat x, tfloat y) => x.Div(y);
    /// <summary>
    /// Divide two numbers in reverse order (the same as / y x). 
    /// </summary>
    [Alias("/i")]
    public static tfloat DivInv(tfloat x, tfloat y) => y.Div(x);
    /// <summary>
    /// Divide two numbers and returns the floor.
    /// </summary>
    [Alias("//")]
    public static tfloat FDiv(tfloat x, tfloat y) => Floor(x.Div(y));
    /// <summary>
    /// (1-x)
    /// </summary>
    [Alias("c")]
    public static tfloat Complement(tfloat x) => E1.Sub(x);
    /// <summary>
    /// x*(1-y)
    /// </summary>
    [Alias("*c")]
    public static tfloat MulComplement(tfloat x, tfloat y) => x.Mul(E1.Sub(y));
    /// <summary>
    /// max(0, x-by).
    /// </summary>
    [Alias("-m")]
    public static tfloat SubMax0(tfloat x, tfloat by) => Max(E0, Sub(x, by));
    
    
    /// <summary>
    /// x+1
    /// </summary>
    [Alias("++")]
    public static tfloat Increment(tfloat x) => x.Add(E1);
    /// <summary>
    /// x-1
    /// </summary>
    [Alias("--")]
    public static tfloat Decrement(tfloat x) => x.Sub(E1);

    /// <summary>
    /// x-1-y
    /// </summary>
    [Alias("---")]
    public static tfloat DecrementSubtract(tfloat x, tfloat y) => x.Sub(E1).Sub(y);

    /// <summary>
    /// Returns -x.
    /// </summary>
    public static tfloat Neg(tfloat x) => EN1.Mul(x);

    /// <summary>
    /// Returns -1 if x lt 0 and 1 otherwise. (Note: Sign(0) = 1)
    /// </summary>
    public static tfloat Sign(efloat x) => EEx.Resolve(x, y => Ex.Condition(y.LT0(), EN1, E1));
    
    #endregion
    
    #region Conversions

    public static tv2 Polar2ToXY(ev2 rt) => EEx.ResolveV2(rt, v2 => PolarToXY(v2.x, v2.y));
    /// <summary>
    /// Convert polar coordinates (theta in degrees) to Cartesian coordinates.
    /// </summary>
    [Alias("rx")]
    public static tv2 PolarToXY(tfloat r, tfloat theta) => r.Mul(CosSinDeg(theta));
    /// <summary>
    /// Convert polar coordinates (theta in radians) to Cartesian coordinates.
    /// </summary>
    public static tv2 PolarToXYRad(tfloat r, tfloat theta) => r.Mul(CosSin(theta));

    /// <summary>
    /// Convert Cartesian coordinates to polar coordinates (theta in degrees).
    /// </summary>
    public static tv2 XYToPolar(ev2 v2) => EEx.Resolve(v2, xy => V2(Mag(xy), ATan(xy)));
    /// <summary>
    /// Convert Cartesian coordinates to polar coordinates (theta in radians).
    /// </summary>
    public static tv2 XYToPolarRad(ev2 v2) => EEx.Resolve(v2, xy => V2(Mag(xy), ATanR(xy)));

    /// <summary>
    /// Converts an RV2 to Cartesian coordinates via TrueLocation.
    /// </summary>
    public static tv2 RV2ToXY(erv2 v2rv2) => EEx.Resolve(v2rv2, rv2 => {
        var tex = new TExRV2(rv2);
        return Rotate2(tex.angle, tex.rx, tex.ry).Add(V2(tex.nx, tex.ny));
    });
    /// <summary>
    /// Converts an RV2 to polar coordinates (theta in degrees) via TrueLocation and XYToPolar.
    /// </summary>
    public static tv2 RV2ToPolar(trv2 v2rv2) => XYToPolar(RV2ToXY(v2rv2));
    
    /// <summary>
    /// Rotate a V2 by some degrees counterclockwise.
    /// </summary>
    public static tv2 Rotate(tfloat ang_deg, tv2 v2) => RotateV(CosSinDeg(ang_deg), v2);

    /// <summary>
    /// Rotate a V2 by some radians counterclockwise.
    /// </summary>
    public static tv2 RotateRad(tfloat ang_rad, tv2 v2) => RotateV(CosSin(ang_rad), v2);

    /// <summary>
    /// Rotate an (x,y) pair by some degrees counterclockwise.
    /// </summary>
    [Alias("rxy")]
    public static tv2 Rotate2(tfloat ang_deg, tfloat xv, tfloat yv) => RotateV2(CosSinDeg(ang_deg), xv, yv);
    /// <summary>
    /// Rotate an (x,y) pair by some radians counterclockwise.
    /// </summary>
    public static tv2 RotateRad2(tfloat ang_rad, tfloat xv, tfloat yv) => RotateV2(CosSin(ang_rad), xv, yv);
    /// <summary>
    /// Rotate a V2 by a vector containing cosine and sine values counterclockwise.
    /// </summary>
    public static tv2 RotateV(ev2 cossin, ev2 v2) => EEx.Resolve(cossin, v2, (cs, vec) => {
        var _cs = new TExV2(cs);
        var tv2 = new TExV2(vec);
        return RotateCS2(_cs.x, _cs.y, tv2.x, tv2.y);
    });
    /// <summary>
    /// Rotate a V2 by a vector containing cosine and sine values counterclockwise.
    /// </summary>
    public static tv2 RotateV2(ev2 cossin, efloat xv, efloat yv) => EEx.Resolve(cossin, (cs) => {
        var _cs = new TExV2(cs);
        return RotateCS2(_cs.x, _cs.y, xv, yv);
    });
    /// <summary>
    /// Rotate a V2 by a calculated cosine and sine value counterclockwise.
    /// </summary>
    public static tv2 RotateCS(tfloat cos, tfloat sin, ev2 v2) => EEx.Resolve(v2, vec => {
        var tv2 = new TExV2(vec);
        return RotateCS2(cos, sin, tv2.x, tv2.y);
    });
    /// <summary>
    /// Rotate an (x,y) pair by a calculated cosine and sine value counterclockwise.
    /// </summary>
    public static tv2 RotateCS2(efloat cos, efloat sin, efloat xv, efloat yv) => EEx.Resolve(cos, sin, xv, yv,
        (c, s, x, y) => V2(c.Mul(x).Sub(s.Mul(y)), s.Mul(x).Add(c.Mul(y))));

    //Note that basis conversion is the same as inverse rotation,
    //and basis deconversion is the same as rotation.
    public static tv2 ConvertBasis(ev2 source, ev2 basis1) => EEx.ResolveV2(source, basis1,
        // [ b1.x  -b1.y ]^T  [ x ]
        // [ b1.y   b1.x ]    [ y ]
        (s, b1) => ExUtils.V2(
            b1.x.Mul(s.x).Add(b1.y.Mul(s.y)),
            b1.x.Mul(s.y).Sub(b1.y.Mul(s.x))
        ));

    public static tv2 DeconvertBasis(ev2 source, ev2 basis1) => RotateV(basis1, source);

    /// <summary>
    /// Get the unit spherical coordinates for a vector3.
    /// </summary>
    public static tv2 ToSphere(ev3 source) => EEx.ResolveV3(source, v => Ex.Block(V2(
        ATan2(v.y, v.x),
        ACos(v.z.Div(v3Mag(v)))
    )));

    public static tv3 FromSphere(tfloat radius, ev2 sphere) => radius.Mul(EEx.ResolveV2(sphere, s => {
        var cst = new TExV2();
        var csp = new TExV2();
        return Ex.Block(new ParameterExpression[] {cst, csp},
            cst.Is(CosSinDeg(s.x)),
            csp.Is(CosSinDeg(s.y)),
            V3(cst.x.Mul(csp.y), cst.y.Mul(csp.y), csp.x)
        );
    }));

    public static tv3 CrossProduct(ev3 v1, ev3 v2) => EEx.ResolveV3(v1, v2, (a, b) => V3(
        a.y.Mul(b.z).Sub(a.z.Mul(b.y)), 
        a.z.Mul(b.x).Sub(a.x.Mul(b.z)), 
        a.x.Mul(b.y).Sub(a.y.Mul(b.x))));

    /// <summary>
    /// Note: requires that normalVec and planeVec are perpendicular.
    /// </summary>
    public static tv3 RotateInPlane(tfloat rot, tv3 normalVec, ev3 planeVec) => EEx.Resolve(planeVec, p => {
            var cs = new TExV2();
            var cross = new TExV3();
            return Ex.Block(new ParameterExpression[] {cs, cross},
                cs.Is(CosSinDeg(rot)),
                cross.Is(CrossProduct(normalVec, p)),
                cs.x.Mul(p).Add(cs.y.Mul(v3Mag(p).Div(v3Mag(cross)).Mul(cross)))
            );
        });
    
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
    /// When two firing indices have been combined via additive parametrization (see <see cref="Danmaku.Enums.Parametrization"/>), this retrieves the parent firing index.
    /// </summary>
    /// <returns></returns>
    public static Ex P1(Ex t) => P1M(SHIFT, t);
    /// <summary>
    /// When two firing indices have been combined via modular parametrization (see <see cref="Danmaku.Enums.Parametrization"/>), this retrieves the parent firing index.
    /// </summary>
    /// <returns></returns>
    public static Ex P1M(int mod, Ex t) {
        if (t.Type == typeof(float)) {
            Ex m = ExC((float) mod);
            return Ex.Divide(Ex.Subtract(t, Ex.Modulo(t, m)), m);
        } else return Ex.Convert(Ex.Divide(t, ExC(mod)), typeof(float));
    }

    /// <summary>
    /// When two firing indices have been combined via additive parametrization (see <see cref="Danmaku.Enums.Parametrization"/>), this retrieves the child firing index.
    /// </summary>
    /// <returns></returns>
    public static Ex P2(Ex t) => P2M(SHIFT, t);
    /// <summary>
    /// When two firing indices have been combined via modular parametrization (see <see cref="Danmaku.Enums.Parametrization"/>), this retrieves the child firing index.
    /// </summary>
    /// <returns></returns>
    public static Ex P2M(int mod, Ex t) {
        bool isFloat = t.Type == typeof(float);
        Ex m = isFloat ? ExC((float) mod) : ExC(mod);
        Ex modded_t = Ex.Modulo(t, m);
        return isFloat ? modded_t : Ex.Convert(modded_t, typeof(float));
    }
    /// <summary>
    /// When two firing indices have been combined via modular or additive parametrization (see <see cref="Danmaku.Enums.Parametrization"/>), this retrieves the firing index of any point in the chain.
    /// Roughly equivalent to mod SELF p1m CHILDREN.
    /// </summary>
    /// <param name="self">Mod size of the target point. Set to 0 to get the effect of additive parametrization.</param>
    /// <param name="children">Product of the mod sizes of all children. Set to 1 if this is the final point.</param>
    /// <param name="t">Index</param>
    /// <returns></returns>
    public static Ex PM(int self, int children, Ex t) {
        if (self == 0) self = SHIFT;
        if (t.Type == typeof(float)) {
            Ex m = ExC((float) children);
            var divided = Ex.Divide(Ex.Subtract(t, Ex.Modulo(t, m)), m);
            return Ex.Modulo(divided, ExC((float)self));
        } else return Ex.Modulo(Ex.Divide(t, ExC(children)), ExC(self)).As<float>();
    }
    public static int __Combine(int x, int y, int mod = SHIFT) {
        return (x * mod) + y;
    }
    
    
    
    #endregion

    #region Sines
    
    private static readonly ExFunction _Sin = ExUtils.Wrap<float>(typeof(M), "Sin");
    private static readonly ExFunction _Cos = ExUtils.Wrap<float>(typeof(M), "Cos");
    private static readonly ExFunction _CosSin = ExUtils.Wrap<float>(typeof(M), "CosSin");
    
    private static readonly ExFunction _SinDeg = ExUtils.Wrap<float>(typeof(M), "SinDeg");
    private static readonly ExFunction _CosDeg = ExUtils.Wrap<float>(typeof(M), "CosDeg");
    private static readonly ExFunction _CosSinDeg = ExUtils.Wrap<float>(typeof(M), "CosSinDeg");
    
    /// <summary>
    /// The raw sine function (period 2pi, peakheight 1).
    /// </summary>
    /// <returns></returns>
    public static tfloat Sin(tfloat x) => _Sin.Of(x);
    /// <summary>
    /// The raw cosine function (period 2pi, peakheight 1).
    /// </summary>
    /// <returns></returns>
    public static tfloat Cos(tfloat x) => _Cos.Of(x);
    /// <summary>
    /// Get the raw cosine and sine functions together.
    /// </summary>
    public static TEx<Vector2> CosSin(tfloat x) => _CosSin.Of(x); 
    /// <summary>
    /// The raw degree sine function (period 360, peakheight 1).
    /// </summary>
    /// <returns></returns>
    public static tfloat SinDeg(tfloat x) => _SinDeg.Of(x);
    /// <summary>
    /// The raw degree cosine function (period 360,, peakheight 1).
    /// </summary>
    /// <returns></returns>
    public static tfloat CosDeg(tfloat x) => _CosDeg.Of(x);
    /// <summary>
    /// Get the raw degree cosine and sine functions together.
    /// </summary>
    public static TEx<Vector2> CosSinDeg(tfloat x) => _CosSinDeg.Of(x); 
    
    //Note: the flattening process will replace these functions with the lookup equivalents
    
    
    /// <summary>
    /// Return a sine with a custom period/amplitude.
    /// </summary>
    /// <param name="period">Sine period</param>
    /// <param name="peakHeight">Sine peak height</param>
    /// <param name="f">Target function</param>
    /// <returns></returns>
    public static tfloat Sine(tfloat period, tfloat peakHeight, tfloat f) => Ex.Multiply(peakHeight, Sin(tau.Mul(f).Div(period)));
    /// <summary>
    /// Return the derivative of a sine with a custom period/amplitude.
    /// </summary>
    /// <param name="period">Sine period</param>
    /// <param name="peakHeight">Sine peak height</param>
    /// <param name="f">Target function</param>
    /// <returns></returns>
    public static tfloat DSine(tfloat period, tfloat peakHeight, tfloat f) {
        var w = VFloat();
        return Ex.Block(new[] {w},
            w.Is(tau.Div(period)),
            w.Mul(peakHeight).Mul(Cos(w.Mul(f)))
        );
    }
    /// <summary>
    /// Return a cosine with a custom period/amplitude.
    /// </summary>
    /// <param name="period">Cosine period</param>
    /// <param name="peakHeight">Cosine peak height</param>
    /// <param name="f">Target function</param>
    /// <returns></returns>
    public static tfloat Cosine(tfloat period, tfloat peakHeight, tfloat f) => Ex.Multiply(peakHeight, Cos(tau.Mul(f).Div(period)));
    /// <summary>
    /// Return the derivative of a cosine with a custom period/amplitude.
    /// </summary>
    /// <param name="period">Cosine period</param>
    /// <param name="peakHeight">Cosine peak height</param>
    /// <param name="f">Target function</param>
    /// <returns></returns>
    public static tfloat DCosine(tfloat period, tfloat peakHeight, tfloat f) {
        var w = VFloat();
        return Ex.Block(new[] {w},
            w.Is(tau.Div(period)),
            w.Mul(peakHeight).Mul(Sin(w.Mul(f))).Neg()
        );
    }

    private static readonly ExFunction _ACos = ExUtils.Wrap<double>(typeof(Math), "Acos");
    /// <summary>
    /// Get the arccosine in radians of a number.
    /// </summary>
    public static Ex ACosR(Ex x) => OfDFD(_ACos, x);

    public static Ex ACos(Ex x) => ACosR(x).Mul(radDeg);
    private static readonly ExFunction _Tan = ExUtils.Wrap<double>(typeof(Math), "Tan");
    /// <summary>
    /// The raw tangent function.
    /// </summary>
    public static Ex Tan(Ex x) => OfDFD(_Tan, x);
    
    private static readonly ExFunction _AtanYX = ExUtils.Wrap<Mathf, float>("Atan2", 2);
    
    /// <summary>
    /// Return the angle in radians whose tangent is y/x.
    /// </summary>
    public static tfloat ATanR2(tfloat y, tfloat x) => _AtanYX.Of(y, x);

    /// <summary>
    /// Return the angle in radians whose tangent is v2.y/v2.x.
    /// </summary>
    public static tfloat ATanR(ev2 f) => EEx.Resolve(f, v2 => {
        var tv2 = new TExV2(v2);
        return _AtanYX.Of(tv2.y, tv2.x);
    });
    /// <summary>
    /// Return the angle in degrees whose tangent is y/x.
    /// </summary>
    public static tfloat ATan2(tfloat y, tfloat x) => ATanR2(y, x).Mul(radDeg);
    /// <summary>
    /// Return the angle in degrees whose tangent is v2.y/v2.x.
    /// </summary>
    public static tfloat ATan(ev2 f) => ATanR(f).Mul(radDeg);
    #endregion
    
    #region BasicUtility
    private static readonly ExFunction _ExpDb = ExUtils.Wrap<double>(typeof(Math), "Exp");
    private static TEx<double> ExpDb(tfloat x) => _ExpDb.Of(Ex.Convert(x, typeof(double)));
    /// <summary>
    /// Returns e^x.
    /// </summary>
    public static tfloat Exp(tfloat x) => OfDFD(_ExpDb, x);
    private static readonly ExFunction _LnDb = ExUtils.Wrap<double>(typeof(Math), "Log");
    public static TEx<double> LnDb(tfloat x) => _LnDb.Of(Ex.Convert(x, typeof(double)));
    /// <summary>
    /// Returns ln(x).
    /// </summary>
    public static tfloat Ln(tfloat x) => OfDFD(_LnDb, x);
    
    
    private static readonly ExFunction _Sqrt = ExUtils.Wrap<double>(typeof(Math), "Sqrt");
    /// <summary>
    /// Returns the square root of a number.
    /// </summary>
    public static tfloat Sqrt(tfloat x) => OfDFD(_Sqrt, x);
    
    /// <summary>
    /// Returns the square of a number.
    /// </summary>
    public static tfloat Sqr(tfloat f) => Pow(f, E2);
    /// <summary>
    /// Returns the quantity sqrt(x^2+y^2).
    /// </summary>
    public static tfloat Mag2(tfloat x, tfloat y) => Sqrt(SqrMag2(x, y));
    /// <summary>
    /// Returns the quantity x^2+y^2.
    /// </summary>
    public static tfloat SqrMag2(efloat x, efloat y) => EEx.Resolve(x,y, (_x, _y) => _x.Mul(_x).Add(_y.Mul(_y)));
    /// <summary>
    /// Returns the quantity x^2+y^2+z^2.
    /// </summary>
    public static tfloat SqrMag3(efloat x, efloat y, efloat z) => EEx.Resolve(x,y,z, (_x, _y,_z) => 
        _x.Mul(_x).Add(_y.Mul(_y).Add(_z.Mul(_z))));

    /// <summary>
    /// Get the magnitude of a vector.
    /// </summary>
    public static tfloat Mag(tv2 v2) => Sqrt(SqrMag(v2));
    /// <summary>
    /// Get the magnitude of a vector.
    /// </summary>
    public static tfloat v3Mag(tv3 v3) => Sqrt(v3SqrMag(v3));

    /// <summary>
    /// Normalize a vector.
    /// </summary>
    public static tv2 Norm(ev2 v2) => EEx.ResolveV2(v2, xy => {
        var mag = VFloat();
        return Ex.Block(new[] {mag},
            mag.Is(Mag(xy)),
            Ex.Condition(mag.GT(ExC(M.MAG_ERR)),
                xy.Mul(E1.Div(mag)),
                xy
            )
        );
    });
    /// <summary>
    /// Normalize a vector.
    /// </summary>
    public static tv3 Norm(ev3 v3) => EEx.ResolveV3(v3, xyz => {
        var mag = VFloat();
        return Ex.Block(new[] {mag},
            mag.Is(v3Mag(xyz)),
            Ex.Condition(mag.GT(ExC(M.MAG_ERR)),
                xyz.Mul(E1.Div(mag)),
                xyz
            )
        );
    });

    /// <summary>
    /// Get the square magnitude of a vector.
    /// </summary>
    public static tfloat SqrMag(ev2 v2) => EEx.ResolveV2(v2, xy => SqrMag2(xy.x, xy.y));
    /// <summary>
    /// Get the square magnitude of a vector.
    /// </summary>
    public static tfloat v3SqrMag(ev3 v3) => EEx.ResolveV3(v3, xyz => SqrMag3(xyz.x, xyz.y, xyz.z));
    
    private static readonly ExFunction _Pow = Wrap<double>(typeof(Math), "Pow", 2);

    /// <summary>
    /// Returns (bas)^(exp).
    /// </summary>
    [Alias("^")]
    public static tfloat Pow(tfloat bas, tfloat exp) => OfDFD(_Pow, bas, exp);
    /// <summary>
    /// Returns one function raised to the power of the other, subtracted by the first function. (Alias: ^- bas exp)
    /// Useful for getting polynomial curves that start at zero, eg. ^- t 1.1
    /// </summary>
    /// <returns></returns>
    [Alias("^-")]
    public static tfloat PowSub(tfloat bas, tfloat exp) {
        var val = VFloat();
        return Ex.Block(new[] {val},
            Ex.Assign(val, bas),
            Pow(val, exp).Sub(val)
        );
    }

    /// <summary>
    /// Returns one number raised to the power of the other.
    /// If bas is negative, then returns - (-bas)^exp. This allows fractional powers on negatives.
    /// (Alias: ^^ bas exp)
    /// </summary>
    /// <param name="bas">Base</param>
    /// <param name="exp">Exponent</param>
    /// <returns></returns>
    [Alias("^^")]
    public static tfloat NPow(efloat bas, efloat exp) => EEx.Resolve(bas, exp, (x, y) =>
        Ex.Condition(Ex.LessThan(x, E0),
            Ex.Negate(Pow(Ex.Negate(x), y)),
            Pow(x, y)));

    public static readonly ExFunction _Round = ExUtils.Wrap<double>(typeof(Math), "Round");
    public static readonly ExFunction _Floor = ExUtils.Wrap<double>(typeof(Math), "Floor");
    public static readonly ExFunction _Ceil = ExUtils.Wrap<double>(typeof(Math), "Ceiling");
    
    /// <summary>
    /// Round a number to the nearest intereger.
    /// </summary>
    public static tfloat Round(tfloat ex) => OfDFD(_Round, ex);

    /// <summary>
    /// = Round(ex / block) * block
    /// </summary>
    public static tfloat BlockRound(efloat block, tfloat ex) => EEx.Resolve(block,
        b => Round(ex.Div(b)).Mul(b));
    
    /// <summary>
    /// Returns the floor of a float value.
    /// </summary>
    public static tfloat Floor(tfloat ex) => OfDFD(_Floor, ex);
    private static Ex dFloor(Ex ex) => _Floor.Of(ex);
    /// <summary>
    /// Returns the ceil of a float value.
    /// </summary>
    public static tfloat Ceil(tfloat ex) => OfDFD(_Ceil, ex);

    private static readonly ExFunction _Abs = ExUtils.Wrap<float>(typeof(Math), "Abs", 1);

    /// <summary>
    /// Get the absolute value of a number.
    /// </summary>
    /// <param name="var">Target</param>
    /// <returns></returns>
    public static tfloat Abs(tfloat var) => _Abs.Of(var);

    /// <summary>
    /// Returns the nonnegative difference between two numbers.
    /// </summary>
    public static tfloat Diff(tfloat x, tfloat y) => _Abs.Of(x.Sub(y));
    
    /// <summary>
    /// Get the normalized distance (square root) between two parametric equations.
    /// </summary>
    /// <returns></returns>
    public static tfloat Dist(tv2 f1, tv2 f2) => Mag(Sub(f1, f2));
    
    /// <summary>
    /// Get the normalized square distance between two parametric equations.
    /// </summary>
    /// <returns></returns>
    public static tfloat SqrDist(tv2 f1, tv2 f2) => SqrMag(Sub(f1, f2));
    
    private static readonly ExFunction _Min = ExUtils.Wrap<float>(typeof(Math), "Min", 2);
    /// <summary>
    /// Return the smaller of two numbers.
    /// </summary>
    public static tfloat Min(tfloat x1, tfloat x2) => _Min.Of(x1, x2);

    /// <summary>
    /// Of two numbers, return the one with the smaller absolute value.
    /// Not well-defined when x1 = -x2.
    /// </summary>
    public static tfloat MinA(efloat x1, efloat x2) =>
        EEx.Resolve(x1, x2, (a, b) => Ex.Condition(Abs(a).LT(Abs(b)), a, b));
    private static readonly ExFunction _Max = ExUtils.Wrap<float>(typeof(Math), "Max", 2);
    /// <summary>
    /// Return the larger of two numbers.
    /// </summary>
    public static tfloat Max(tfloat x1, tfloat x2) => _Max.Of(x1, x2);
    /// <summary>
    /// Of two numbers, return the one with the larger absolute value.
    /// Not well-defined when x1 = -x2.
    /// </summary>
    public static tfloat MaxA(efloat x1, efloat x2) =>
        EEx.Resolve(x1, x2, (a, b) => Ex.Condition(Abs(a).GT(Abs(b)), a, b));

    /// <summary>
    /// Limit a value x to have absolute value leq by.
    /// </summary>
    /// <param name="by">Positive number for absolute value comparison</param>
    /// <param name="x">Number to be limited</param>
    /// <returns></returns>
    public static tfloat Limit(efloat by, efloat x) => EEx.Resolve(by, x, Limit);
    private static Ex Limit(tfloat by, tfloat x) => Ex.Condition(x.GT0(), Min(x, by), Max(x, Ex.Negate(by)));
    
    private static readonly ExFunction _Clamp = ExUtils.Wrap<Mathf, float>("Clamp", 3);
    
    /// <summary>
    /// Clamp a value to a [min, max] range.
    /// </summary>
    public static tfloat Clamp(tfloat min, tfloat max, tfloat x) => _Clamp.Of(x, min, max);
    private static readonly ExFunction _Clamp01 = ExUtils.Wrap<Mathf, float>("Clamp01", 1);
    /// <summary>
    /// Clamp a value to the [0, 1] range.
    /// </summary>
    public static tfloat Clamp01(tfloat x) => _Clamp01.Of(x);

    /// <summary>
    /// Return the linear function a+b*x.
    /// </summary>
    public static tfloat Linear(tfloat a, tfloat b, tfloat x) => a.Add(b.Mul(x));

    /// <summary>
    /// Move a value in the range [-3pi, 3pi] to the range [-pi, pi] by adding or subtracting tau.
    /// </summary>
    /// <param name="ang_rad"></param>
    /// <returns></returns>
    public static tfloat RadIntoRange(efloat ang_rad) =>
        EEx.Resolve(ang_rad, a => Ex.Condition(a.GT(pi), 
            a.Sub(tau), 
            Ex.Condition(a.LT(npi), 
                a.Add(tau), 
                a)));
    /// <summary>
    /// Move a value in the range [-540, 540] to the range [-180, 180] by adding or subtracting 360.
    /// </summary>
    /// <param name="ang_rad"></param>
    /// <returns></returns>
    public static tfloat DegIntoRange(efloat ang_rad) =>
        EEx.Resolve(ang_rad, a => Ex.Condition(a.GT(ExC(180f)), 
            a.Sub(ExC(360f)), 
            Ex.Condition(a.LT(ExC(-180f)), 
                a.Add(ExC(360f)), 
                a)));

    /// <summary>
    /// Get the rotation required, in radians, to rotate SOURCE to TARGET, in the range [-pi,pi].
    /// </summary>
    public static tfloat RadDiff(ev2 target, ev2 source) =>
        EEx.Resolve(target, source, (t, s) => RadIntoRange(ATanR(t).Sub(ATanR(s))));
    
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
    private static readonly ExFunction SeedRandInt = Wrap(typeof(RNG), "GetSeededFloat", new[] {typeof(float), typeof(float), typeof(int)});
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

    #region Mod
    
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
    
    #endregion
    
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
    
    #region Easing

    /// <summary>
    /// Apply a contortion to a 0-1 range.
    /// This returns an approximately linear function.
    /// </summary>
    /// <param name="name">Name of easing method</param>
    /// <param name="controller">0-1 value</param>
    /// <returns></returns>
    public static tfloat Smooth(string name, tfloat controller) => EaseHelpers.GetFunc(name)(controller);
    /// <summary>
    /// Apply a contortion to a clamped 0-1 range.
    /// This returns an approximately linear function.
    /// </summary>
    /// <param name="name">Name of easing method</param>
    /// <param name="controller">0-1 value (clamped if outside)</param>
    /// <returns></returns>
    public static tfloat SmoothC(string name, tfloat controller) => EaseHelpers.GetFunc(name)(Clamp01(controller));

    /// <summary>
    /// Apply a contortion to a 0-x range, returning:
    /// <br/> 0-1 in the range [0,s1]
    /// <br/> 1 in the range [s1,x-s2]
    /// <br/> 1-0 in the range [x-s2,x]
    /// </summary>
    /// <param name="name1">First easing method</param>
    /// <param name="name2">Second easing method</param>
    /// <param name="total">Total time</param>
    /// <param name="smth1">Smooth-in time</param>
    /// <param name="smth2">Smooth-out time</param>
    /// <param name="controller">0-x value</param>
    /// <returns></returns>
    public static tfloat SmoothIO(string name1, string name2, efloat total, efloat smth1, efloat smth2, efloat controller) =>
        EEx.Resolve(total, smth1, smth2, controller,
            (T, s1, s2, t) => Ex.Condition(t.LT(T.Sub(smth2)), 
                    SmoothC(name1, t.Div(s1)),
                    E1.Sub(SmoothC(name2, t.Sub(T.Sub(s2)).Div(s2)))
                ));

    /// <summary>
    /// Apply SmoothIO where name=name1=name2 and smth=smth1=smth2.
    /// </summary>
    public static tfloat SmoothIOe(string name, tfloat total, efloat smth, tfloat controller) =>
        EEx.Resolve(smth, s => SmoothIO(name, name, total, s, s, controller));

    /// <summary>
    /// Get the value of an easer at a given point between 0 and 1.
    /// The return value is periodized, so if the input is 5.4, then the output is 5 + ease(0.4).
    /// </summary>
    /// <param name="name"></param>
    /// <param name="controller"></param>
    /// <returns></returns>
    public static tfloat SmoothLoop(string name, efloat controller) => EEx.Resolve(controller, x => {
        var per = VFloat();
        return Ex.Block(new[] {per},
            per.Is(Floor(x)),
            per.Add(Smooth(name, x.Sub(per)))
        );
    });

    /// <summary>
    /// Apply a contortion to a 0-R range, returning R * Smooth(name, controller/R).
    /// This returns an approximately linear function.
    /// </summary>
    /// <param name="name">Name of easing method</param>
    /// <param name="range">Range</param>
    /// <param name="controller">0-R value</param>
    /// <returns></returns>
    public static tfloat SmoothR(string name, efloat range, tfloat controller) =>
        EEx.Resolve(range, r => r.Mul(Smooth(name, controller.Div(r))));
    
    /// <summary>
    /// Returns R * SmoothLoop(name, controller/R).
    /// </summary>
    public static tfloat SmoothLoopR(string name, efloat range, tfloat controller) =>
        EEx.Resolve(range, r => r.Mul(SmoothLoop(name, controller.Div(r))));
    
    /// <summary>
    /// In-Sine easing function.
    /// </summary>
    public static tfloat EInSine(tfloat x) => E1.Sub(Cos(hpi.Mul(x)));
    /// <summary>
    /// Out-Sine easing function.
    /// </summary>
    public static tfloat EOutSine(tfloat x) => Sin(hpi.Mul(x));
    /// <summary>
    /// In-Out-Sine easing function.
    /// </summary>
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
    
    /// <summary>
    /// Quadratic function that joins an ease-out and an ease-in, ie. two joined parabolas.
    /// </summary>
    /// <param name="midp"></param>
    /// <param name="period"></param>
    /// <param name="controller"></param>
    /// <returns></returns>
    public static tfloat EQuad0m10(efloat midp, tfloat period, tfloat controller) => EEx.Resolve(midp, m => {
        var t = VFloat();
        return Ex.Block(new[] {t},
            t.Is(controller.Sub(m)),
            Sqr(t).Div(Ex.Condition(t.LT0(), Sqr(m), Sqr(period.Sub(m)))).Complement()
        );
    });
    
    #endregion
    
    #region Conditionals

    /// <summary>
    /// Convert a boolean into a 1/0 value.
    /// </summary>
    public static tfloat Pred10(tbool pred) => Ex.Condition(pred, E1, E0);
    /// <summary>
    /// If the predicate is true, return the true branch, otherwise the false branch.
    /// </summary>
    public static TEx<T> If<T>(tbool pred, TEx<T> iftrue, TEx<T> iffalse) => Ex.Condition(pred, iftrue, iffalse);

    /// <summary>
    /// If the switcher is nonzero, return the result, otherwise the default value.
    /// </summary>
    public static TEx<T> IfN0<T>(tfloat switcher, TEx<T> result) =>
        Ex.Condition(Ex.Equal(switcher, E0), Ex.Default(typeof(T)), result);
    /// <summary>
    /// If the switcher is zero, return the result, otherwise the default value.
    /// </summary>
    public static TEx<T> If0<T>(tfloat switcher, TEx<T> result) =>
        Ex.Condition(Ex.Equal(switcher, E0), result, Ex.Default(typeof(T)));
    /// <summary>
    /// If the switcher is not 1, return the result, otherwise the default value.
    /// </summary>
    public static TEx<T> IfN1<T>(tfloat switcher, TEx<T> result) =>
        Ex.Condition(Ex.Equal(switcher, E1), Ex.Default(typeof(T)), result);
    /// <summary>
    /// If the switcher is 1, return the result, otherwise the default value.
    /// </summary>
    public static TEx<T> If1<T>(tfloat switcher, TEx<T> result) =>
        Ex.Condition(Ex.Equal(switcher, E1), result, Ex.Default(typeof(T)));
    
    #endregion
    
    /// <summary>
    /// Lerp between two functions.
    /// <br/>Note: Unless marked otherwise, all lerp functions clamp the controller.
    /// </summary>
    /// <param name="zeroBound">Lower bound for lerp controller</param>
    /// <param name="oneBound">Upper bound for lerp controller</param>
    /// <param name="controller">Lerp controller</param>
    /// <param name="f1">First function (when controller leq zeroBound, return this)</param>
    /// <param name="f2">Second function (when controller geq oneBound, return this)</param>
    /// <returns></returns>
    public static TEx<T> Lerp<T>(efloat zeroBound, efloat oneBound, efloat controller, TEx<T> f1, TEx<T> f2) => 
        EEx.Resolve(zeroBound, oneBound, controller, (z, o, c) => {
            var rc = VFloat();
            return Ex.Block(new[] {rc},
                rc.Is(Clamp(z, o, c).Sub(z).Div(o.Sub(z))),
                rc.Mul(f2).Add(rc.Complement().Mul(f1))
            );
        });

    /// <summary>
    /// Lerp between two functions with 0-1 as the bounds for the controller.
    /// </summary>
    public static TEx<T> Lerp01<T>(efloat controller, TEx<T> f1, TEx<T> f2) => Lerp(E0, E1, controller, f1, f2);
    /// <summary>
    /// Lerp between two functions with smoothing applied to the controller.
    /// </summary>
    public static TEx<T> LerpSmooth<T>(string smoother, efloat zeroBound, efloat oneBound, efloat controller, 
        TEx<T> f1, TEx<T> f2) => EEx.Resolve(zeroBound, oneBound, controller, (z, o, c) => {
            var rc = VFloat();
            return Ex.Block(new[] {rc},
                rc.Is(Smooth(smoother, Clamp(z, o, c).Sub(z).Div(o.Sub(z)))),
                rc.Mul(f2).Add(rc.Complement().Mul(f1))
            );
        });
    
    /// <summary>
    /// Lerp between two functions. The controller is not clamped.
    /// </summary>
    /// <param name="zeroBound">Lower bound for lerp controller</param>
    /// <param name="oneBound">Upper bound for lerp controller</param>
    /// <param name="controller">Lerp controller</param>
    /// <param name="f1">First function</param>
    /// <param name="f2">Second function</param>
    /// <returns></returns>
    public static TEx<T> LerpU<T>(efloat zeroBound, efloat oneBound, efloat controller, TEx<T> f1, TEx<T> f2) => 
        EEx.Resolve(zeroBound, oneBound, controller, (z, o, c) => {
            var rc = VFloat();
            return Ex.Block(new[] {rc},
                rc.Is(c.Sub(z).Div(o.Sub(z))),
                rc.Mul(f2).Add(rc.Complement().Mul(f1))
            );
        });
    
    /// <summary>
    /// Lerp between three functions.
    /// Between zeroBound and oneBound, lerp from the first to the second.
    /// Between zeroBound2 and oneBound2, lerp from the second to the third.
    /// </summary>
    public static TEx<T> Lerp3<T>(efloat zeroBound, efloat oneBound,
        efloat zeroBound2, efloat oneBound2, efloat controller, TEx<T> f1, TEx<T> f2, TEx<T> f3) => 
        EEx.Resolve(zeroBound, oneBound, zeroBound2, oneBound2, controller, (z1, o1, z2, o2, c) => 
            Ex.Condition(c.LT(z2), Lerp(z1, o1, c, f1, f2), Lerp(z2, o2, c, f2, f3)));

    /// <summary>
    /// Lerp between three functions.
    /// Between zeroBound and oneBound, lerp from the first to the second.
    /// Between oneBound and twoBound, lerp from the second to the third.
    /// </summary>
    public static TEx<T> Lerp3c<T>(tfloat zeroBound, efloat oneBound, tfloat twoBound,
        tfloat controller, TEx<T> f1, TEx<T> f2, TEx<T> f3) => EEx.Resolve(oneBound,
        ob => Lerp3(zeroBound, ob, ob, twoBound, controller, f1, f2, f3));

    /// <summary>
    /// Lerp between two functions.
    /// Between zeroBound and oneBound, lerp from the first to the second.
    /// Between oneBound2 and zeroBound2, lerp from the second back to the first.
    /// </summary>
    /// <param name="zeroBound">Lower bound for lerp controller</param>
    /// <param name="oneBound">Upper bound for lerp controller</param>
    /// <param name="oneBound2">Upper bound for lerp controller</param>
    /// <param name="zeroBound2">Lower bound for lerp controller</param>
    /// <param name="controller">Lerp controller</param>
    /// <param name="f1">First function (when controller leq zeroBound, return this)</param>
    /// <param name="f2">Second function (when controller geq oneBound, return this)</param>
    /// <returns></returns>
    public static TEx<T> LerpBack<T>(tfloat zeroBound, tfloat oneBound, tfloat oneBound2,
        tfloat zeroBound2, tfloat controller, TEx<T> f1, TEx<T> f2) =>
        Lerp3(zeroBound, oneBound, oneBound2, zeroBound2, controller, f1, f2, f1);

    /// <summary>
    /// Lerp between many functions.
    /// </summary>
    public static TEx<T> LerpMany<T>((tfloat bd, TEx<T> val)[] points, tfloat controller) {
        Ex ifLt = points[0].val;
        for (int ii = 0; ii < points.Length - 1; ++ii) {
            ifLt = Ex.Condition(controller.LT(points[ii].bd), ifLt,
                LerpU(points[ii].bd, points[ii+1].bd, controller, points[ii].val, points[ii+1].val));
        }
        return Ex.Condition(controller.LT(points[points.Length-1].bd), ifLt, points[points.Length-1].val);
    }

    /// <summary>
    /// Return 0 if the controller is leq the lower bound, 1 if the controller is geq the lower bound, and
    /// a linear interpolation in between.
    /// </summary>
    public static tfloat SStep(efloat zeroBound, tfloat oneBound, efloat controller) => EEx.Resolve(zeroBound, controller, (z, c) => Clamp01(c.Sub(z).Div(oneBound.Sub(z))));
    
    /// <summary>
    /// Provide a soft ceiling for the value, multiplying any excess by the value RATIO.
    /// </summary>
    public static tfloat Damp(efloat ceiling, tfloat ratio, efloat value) => EEx.Resolve(ceiling, value, (c, x) =>
        If(x.GT(c), c.Add(ratio.Mul(x.Sub(c))), x));

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
    /// Calculate the logsum of several values ( (ln Sum e^ax) / a )
    /// </summary>
    /// <param name="sharpness">The higher the absolute value of this, the more quickly the result will converge.
    /// Set negative for softmin.</param>
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
    
    #region Oscillators

    /// <summary>
    /// Reference https://www.desmos.com/calculator/obzdqxmrq4
    /// </summary>
    /// <param name="start_move"></param>
    /// <param name="end_move"></param>
    /// <param name="end_oscillate"></param>
    /// <param name="osc_rad"></param>
    /// <param name="osc_per"></param>
    /// <param name="controller"></param>
    /// <returns></returns>
    public static tfloat Oscillate1(efloat start_move, efloat end_move, efloat end_oscillate, tfloat osc_rad,
        tfloat osc_per, efloat controller) => EEx.Resolve(start_move, end_move, end_oscillate, controller,
        (t1, t2, t3, t) => Ex.Add(
                Ex.Condition(t.GT(t2),
                    E1,
                    Ex.Condition(t.LT(t1),
                        E0,
                        E05.Sub(E05.Mul(Cos(pi.Mul(t.Sub(t1).Div(t2.Sub(t1)))))))),
                osc_rad.Mul(Sin(tau.Div(osc_per).Mul(t))).Mul(
                    Ex.Condition(t.GT(t2),
                        Max(E0, E1.Sub(Sqr(t.Sub(t2).Div(t3.Sub(t2))))),
                        Ex.Condition(t.LT(t1),
                            Sqr(t.Div(t1)),
                            ExC(0.5f).Add(ExC(0.5f).Mul(Cos(tau.Div(t2.Sub(t1)).Mul(t.Sub(t1)))))
                    ))
                )));
    
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

    #region Difficulty

    /// <summary>
    /// Get the difficulty multiplier. 1 is easy, 2.5 is lunatic. POSITIVE values outside this range are possible.
    /// </summary>
    public static tfloat D() => Ex.Constant(DifficultyValue);
    /// <summary>
    /// Get the difficulty counter. 0 is easy, 3 is lunatic.
    /// </summary>
    public static tfloat Dc() => Ex.Constant(DifficultyCounter);
    
    /// <summary>
    /// Get the difficulty multiplier centered on normal.
    /// </summary>
    public static tfloat DN() => Ex.Constant(DifficultyValue / DifficultySet.Normal.Value());
    /// <summary>
    /// Get the difficulty multiplier centered on hard.
    /// </summary>
    public static tfloat DH() => Ex.Constant(DifficultyValue / DifficultySet.Hard.Value());
    /// <summary>
    /// Get the difficulty multiplier centered on lunatic.
    /// </summary>
    public static tfloat DL() => Ex.Constant(DifficultyValue / DifficultySet.Lunatic.Value());

    /// <summary>
    /// 1 / DL
    /// </summary>
    public static tfloat iDL() => Ex.Constant(DifficultySet.Lunatic.Value() / DifficultyValue);

    private static tfloat ResolveD3(tfloat n, tfloat h, tfloat u) =>
        DifficultyValue < DifficultySet.Hard.Value() ? n :
        DifficultyValue < DifficultySet.Ultra.Value() ? h :
        u;

    /// <summary>
    /// Return -2 if the difficulty is less than Hard,
    /// else 0 if less than Ultra,
    /// else 2.
    /// </summary>
    /// <returns></returns>
    public static tfloat D3d2() => ResolveD3(EN2, E0, E2);
    /// <summary>
    /// Return -1 if the difficulty is less than Hard,
    /// else 0 if less than Ultra,
    /// else 1.
    /// </summary>
    /// <returns></returns>
    public static tfloat D3d1() => ResolveD3(EN1, E0, E1);

    #endregion

    #region Remappers

    /// <summary>
    /// Use Fermat's Little Theorem to reindex integers around a prime number mod.
    /// </summary>
    public static tfloat RemapIndex(efloat mod, tfloat index) => EEx.Resolve(mod, m => Mod(m, index.Mul(m.Sub(E1))));
    
    /// <summary>
    /// Use Fermat's Little Theorem to reindex integers around a prime number mod, localized to the region
    /// [mod*floor(index/mod), mod+mod*floor(index/mod)].
    /// </summary>
    public static tfloat RemapIndexLoop(efloat mod, efloat index) => EEx.Resolve(mod, index, (m, i) => {
        var rem = VFloat();
        return Ex.Block(new[] {rem},
            rem.Is(Mod(m, i)),
            i.Sub(rem).Add(RemapIndex(m, rem))
        );
    });
    
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
    public static tfloat HPRatio(BEHPointer beh) => BehaviorEntity.hpRatio.Of(ExC(beh));

    #endregion
}
}