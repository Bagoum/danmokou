using System;
using BagoumLib.Expressions;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Expressions;
using UnityEngine;
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

namespace Danmokou.DMath.Functions {
public static partial class ExM {
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
    [Alias("+")] [WarnOnStrict]
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
    [Alias("-")] [WarnOnStrict]
    public static TEx<T> Sub<T>(TEx<T> x, TEx<T> y) => x.Sub(y);
    /// <summary>
    /// Multiply a vectype by a number.
    /// </summary>
    [Alias("*")] [WarnOnStrict]
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
    [Alias("/")] [WarnOnStrict]
    public static tfloat Div(tfloat x, tfloat y) => x.Div(y);
    /// <summary>
    /// Divide two numbers in reverse order (the same as / y x). 
    /// </summary>
    [Alias("/i")]
    public static tfloat DivInv(tfloat x, tfloat y) => y.Div(x);
    /// <summary>
    /// Divide two numbers and returns the floor.
    /// </summary>
    [Alias("//")] [WarnOnStrict]
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
    
    private static readonly ExFunction _ExpDb = ExFunction.Wrap<double>(typeof(Math), "Exp");
    private static TEx<double> ExpDb(tfloat x) => _ExpDb.Of(Ex.Convert(x, typeof(double)));
    /// <summary>
    /// Returns e^x.
    /// </summary>
    public static tfloat Exp(tfloat x) => OfDFD(_ExpDb, x);
    private static readonly ExFunction _LnDb = ExFunction.Wrap<double>(typeof(Math), "Log");
    private static TEx<double> LnDb(tfloat x) => _LnDb.Of(Ex.Convert(x, typeof(double)));
    /// <summary>
    /// Returns ln(x).
    /// </summary>
    public static tfloat Ln(tfloat x) => OfDFD(_LnDb, x);
    
    
    private static readonly ExFunction _Sqrt = ExFunction.Wrap<double>(typeof(Math), "Sqrt");
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
    public static tv3 Norm3(ev3 v3) => EEx.ResolveV3(v3, xyz => {
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
    
    private static readonly ExFunction _Pow = ExFunction.Wrap<double>(typeof(Math), "Pow", 2);

    /// <summary>
    /// Returns (bas)^(exp).
    /// </summary>
    [Alias("^")] [WarnOnStrict]
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

    private static readonly ExFunction _Round = ExFunction.Wrap<double>(typeof(Math), "Round");
    private static readonly ExFunction _Floor = ExFunction.Wrap<double>(typeof(Math), "Floor");
    private static readonly ExFunction _Ceil = ExFunction.Wrap<double>(typeof(Math), "Ceiling");
    
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
    /// <summary>
    /// = Floor(ex / block) * block
    /// </summary>
    public static tfloat BlockFloor(efloat block, tfloat ex) => EEx.Resolve(block,
        b => Floor(ex.Div(b)).Mul(b));
    public static Ex dFloor(Ex ex) => _Floor.Of(ex);
    /// <summary>
    /// Returns the ceil of a float value.
    /// </summary>
    public static tfloat Ceil(tfloat ex) => OfDFD(_Ceil, ex);

    private static readonly ExFunction _Abs = ExFunction.Wrap<float>(typeof(Math), "Abs", 1);

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
    
    private static readonly ExFunction _Min = ExFunction.Wrap<float>(typeof(Math), "Min", 2);
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
    private static readonly ExFunction _Max = ExFunction.Wrap<float>(typeof(Math), "Max", 2);
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
    public static tfloat Limit(efloat by, efloat x) => EEx.Resolve(by, x, (_by, _x) => 
        Ex.Condition(_x.GT0(), Min(_x, _by), Max(_x, Ex.Negate(_by))));

    /// <summary>
    /// If x's absolute value is less than by, then return 0 instead.
    /// </summary>
    public static tfloat HighPass(tfloat by, efloat x) => 
        EEx.Resolve(x, _x => Ex.Condition(Abs(_x).LT(by), E0, _x));
    /// <summary>
    /// If x's absolute value is greater than by, then return 0 instead.
    /// </summary>
    public static tfloat HighCut(tfloat by, efloat x) => 
        EEx.Resolve(x, _x => Ex.Condition(Abs(_x).GT(by), E0, _x));
    
    private static readonly ExFunction _Clamp = ExFunction.Wrap<Mathf, float>("Clamp", 3);
    
    /// <summary>
    /// Clamp a value to a [min, max] range.
    /// </summary>
    public static tfloat Clamp(tfloat min, tfloat max, tfloat x) => _Clamp.Of(x, min, max);
    private static readonly ExFunction _Clamp01 = ExFunction.Wrap<Mathf, float>("Clamp01", 1);
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
    /// Move a value in the range [-2pi, 2pi] to the range [-2pi, 0] by subtracting tau.
    /// </summary>
    /// <param name="ang_rad"></param>
    /// <returns></returns>
    public static tfloat RadToNeg(efloat ang_rad) => EEx.Resolve(ang_rad, a =>
        Ex.Condition(a.GT0(), a.Sub(tau), a));
    /// <summary>
    /// Move a value in the range [-2pi, 2pi] to the range [0,2pi] by adding tau.
    /// </summary>
    /// <param name="ang_rad"></param>
    /// <returns></returns>
    public static tfloat RadToPos(efloat ang_rad) => EEx.Resolve(ang_rad, a =>
        Ex.Condition(a.LT0(), a.Add(tau), a));
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
    /// Get the rotation required, in degrees, to rotate SOURCE to TARGET, in the range [-180,180].
    /// </summary>
    public static tfloat DegDiff(ev2 target, ev2 source) => DegIntoRange(ATan(target).Sub(ATan(source)));
    /// <summary>
    /// Get the rotation required, in radians, to rotate SOURCE to TARGET, in the range [-pi,pi].
    /// </summary>
    public static tfloat RadDiff(ev2 target, ev2 source) => RadIntoRange(ATanR(target).Sub(ATanR(source)));
    /// <summary>
    /// Get the rotation required, in radians, to rotate SOURCE to TARGET, in the range [0,2pi].
    /// </summary>
    public static tfloat RadDiffCCW(ev2 target, ev2 source) => RadToPos(ATanR(target).Sub(ATanR(source)));

    /// <summary>
    /// Get the rotation required, in radians, to rotate SOURCE to TARGET, in the range [-2pi,0].
    /// </summary>
    public static tfloat RadDiffCW(ev2 target, ev2 source) => RadToNeg(ATanR(target).Sub(ATanR(source)));
    
    #region Sines
    
    private static readonly ExFunction _Sin = ExFunction.Wrap<float>(typeof(M), "Sin");
    private static readonly ExFunction _Cos = ExFunction.Wrap<float>(typeof(M), "Cos");
    private static readonly ExFunction _CosSin = ExFunction.Wrap<float>(typeof(M), "CosSin");
    
    private static readonly ExFunction _SinDeg = ExFunction.Wrap<float>(typeof(M), "SinDeg");
    private static readonly ExFunction _CosDeg = ExFunction.Wrap<float>(typeof(M), "CosDeg");
    private static readonly ExFunction _CosSinDeg = ExFunction.Wrap<float>(typeof(M), "CosSinDeg");
    
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

    private static readonly ExFunction _ACos = ExFunction.Wrap<double>(typeof(Math), "Acos");
    /// <summary>
    /// Get the arccosine in radians of a number.
    /// </summary>
    public static tfloat ACosR(tfloat x) => OfDFD(_ACos, x);

    public static tfloat ACos(tfloat x) => ACosR(x).Mul(radDeg);
    private static readonly ExFunction _Tan = ExFunction.Wrap<double>(typeof(Math), "Tan");
    /// <summary>
    /// The raw tangent function.
    /// </summary>
    public static tfloat Tan(tfloat x) => OfDFD(_Tan, x);
    
    private static readonly ExFunction _AtanYX = ExFunction.Wrap<Mathf, float>("Atan2", 2);
    
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
    
    
    
}
}