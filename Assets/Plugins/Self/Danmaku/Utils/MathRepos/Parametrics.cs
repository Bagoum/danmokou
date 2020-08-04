﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Danmaku;
using Ex = System.Linq.Expressions.Expression;
using ExTP = System.Func<DMath.TExPI, TEx<UnityEngine.Vector2>>;
using ExTP3 = System.Func<DMath.TExPI, TEx<UnityEngine.Vector3>>;
using ExBPRV2 = System.Func<DMath.TExPI, TEx<DMath.V2RV2>>;
using ExFXY = System.Func<TEx<float>, TEx<float>>;
using ExBPY = System.Func<DMath.TExPI, TEx<float>>;
using ExPred = System.Func<DMath.TExPI, TEx<bool>>;
using static DMath.ExMHelpers;
using static ExUtils;
using FR = DMath.FXYRepo;
using static DMath.ExM;

namespace DMath {
/// <summary>
/// Functions that take in parametric information and return V2.
/// <br/>Note: most functions in this repository should be rewritten or turned into simple expression functions.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
public static partial class Parametrics {
    private static readonly Ex EPS = Ex.Constant(M.MAG_ERR);
    /// <summary>
    /// Assign local variables that can be repeatedly used without reexecution. Shortcut: ::
    /// </summary>
    /// <param name="aliases">List of each variable and its assigned float value</param>
    /// <param name="inner">Code to execute within the scope of the variables</param>
    public static ExTP LetFloats((string, ExBPY)[] aliases, ExTP inner) => bpi => ReflectEx.Let(aliases, () => inner(bpi), bpi);
    /// <summary>
    /// Assign local variables that can be repeatedly used without reexecution. Shortcut: ::v2
    /// </summary>
    /// <param name="aliases">List of each variable and its assigned vector value</param>
    /// <param name="inner">Code to execute within the scope of the variables</param>
    public static ExTP LetV2s((string, ExTP)[] aliases, ExTP inner) => bpi => ReflectEx.Let(aliases, () => inner(bpi), bpi);
    /// <summary>
    /// Reference a value defined in a let function. Shortcut: &amp;
    /// </summary>
    /// <returns></returns>
    public static ExTP Reference(string alias) => ReflectEx.ReferenceLetBPI<Vector2>(alias);
    
    public static ExTP Zero() => CXY(0, 0);
    
    /// <summary>
    /// Returns the root location of the entity's velocity struct.
    /// </summary>
    public static ExTP Root() => Reference("root");

    #region Shapes

    public static ExTP PRect(float period, float r1, float r2) {
        Ex switchPt = ExC(Mathf.Atan2(r2, r1));
        Ex w = ExC(M.TAU / period);
        var ang = ExUtils.VFloat();
        var eang = ExUtils.VFloat();
        return bpi => Ex.Block(new[] {ang, eang},
            Ex.Assign(eang, Ex.Assign(ang, Ex.Multiply(w, bpi.findex))),
            //Map the angle into the (0,90) space
            eang.Is(Mod(pi, eang)),
            Ex.IfThen(Ex.GreaterThan(eang, hpi), Ex.Assign(eang, Ex.Subtract(pi, eang))),
            //Get the radius, put in eang
            //Note: don't coalesce this, it would execute twice.
            Ex.Assign(eang, Ex.Condition(Ex.GreaterThan(eang, switchPt),
                Ex.Divide(ExC(r2), Sin(eang)),
                Ex.Divide(ExC(r1), Cos(eang))
            )),
            PolarToXY(eang, ang)
        );
    }
    
    #endregion
    
    #region ConstantVectors

    /// <summary>
    /// Returns a constant vector.
    /// </summary>
    /// <param name="x">X-component</param>
    /// <param name="y">Y-component</param>
    /// <returns></returns>
    public static ExTP CXY(float x, float y) {
        return bpi => Ex.Constant(new Vector2(x, y));
    }

    /// <summary>
    /// Returns a constant vector with the y-component set to zero.
    /// </summary>
    /// <param name="x">X-component</param>
    /// <returns></returns>
    public static ExTP CX(float x) {
        return CXY(x, 0);
    }

    /// <summary>
    /// Returns a constant vector with the x-component set to zero.
    /// </summary>
    /// <param name="y">Y-component</param>
    /// <returns></returns>
    public static ExTP CY(float y) {
        return CXY(0, y);
    }

    /// <summary>
    /// Returns a constant vector equal to the Cartesian coordinates for the polar coordinates (r,theta).
    /// </summary>
    /// <param name="r">Radius</param>
    /// <param name="theta">Theta, in degrees</param>
    /// <returns></returns>
    public static ExTP CR(float r, float theta) {
        return bpi => Ex.Constant(new Vector2(r * M.CosDeg(theta), r * M.SinDeg(theta)));
    }

    #endregion

    /// <summary>
    /// Returns a base vector multiplied by time.
    /// </summary>
    /// <param name="x">Base X-component</param>
    /// <param name="y">Base Y-component</param>
    /// <returns></returns>
    public static ExTP CM(float x, float y) {
        return bpi => ExUtils.V2(
            Ex.Multiply(ExC(x), bpi.t), 
            Ex.Multiply(ExC(y), bpi.t)    
        );
    }
    
    /// <summary>
    /// Divide a vector equation by a float equation.
    /// </summary>
    /// <param name="by">Float equation multiplier</param>
    /// <param name="tp">Base vector equation</param>
    /// <returns></returns>
    public static ExTP Divide(ExBPY by, ExTP tp) {
        return bpi => Ex.Divide(tp(bpi), by(bpi));
    }
    
    
    #region FModifiers

    /// <summary>
    /// Multiply the x-component of a parametric equation by a function of input.
    /// </summary>
    /// <param name="f">Function of input</param>
    /// <param name="tp">Parametric equation</param>
    /// <returns></returns>
    public static ExTP MultiplyX(ExBPY f, ExTP tp) {
        var v = TExV2.Variable();
        var by = ExUtils.VFloat();
        return bpi => Ex.Block(
            new[] {v, by},
            Ex.Assign(v, tp(bpi)),
            MulAssign(v.x, f(bpi)),
            v
        );
    }
    /// <summary>
    /// Multiply the y-component of a parametric equation by a function of input.
    /// </summary>
    /// <param name="f">Function of input</param>
    /// <param name="tp">Parametric equation</param>
    /// <returns></returns>
    public static ExTP MultiplyY(ExBPY f, ExTP tp) {
        var v = TExV2.Variable();
        var by = ExUtils.VFloat();
        return bpi => Ex.Block(
            new[] {v, by},
            Ex.Assign(v, tp(bpi)),
            MulAssign(v.y, f(bpi)),
            v
        );
    }

    /// <summary>
    /// Add a function of input to the y-component of a parametric equation.
    /// </summary>
    /// <param name="f">Function of input</param>
    /// <param name="tp">Parametric equation</param>
    /// <returns></returns>
    public static ExTP AddY(ExBPY f, ExTP tp) {
        var v = TExV2.Variable();
        var by = ExUtils.VFloat();
        return bpi => Ex.Block(
            new[] {v, by},
            Ex.Assign(v, tp(bpi)),
            ExUtils.AddAssign(v.y, f(bpi)),
            v
        );
    }

    /// <summary>
    /// Multiply a unit vector at a given angle from the target function by the magnitude,
    /// and add it to the target function.
    /// </summary>
    /// <param name="angle">Angle offset (degrees)</param>
    /// <param name="magnitude">Magnitude of offset vector</param>
    /// <param name="tp">Target function</param>
    /// <returns></returns>
    public static ExTP AddAtAngle(ExBPY angle, ExBPY magnitude, ExTP tp) {
        TExV2 v2 = TExV2.Variable();
        TExV2 v2norm = TExV2.Variable();
        var mag = ExUtils.VFloat();
        return bpi => Ex.Block(new[] {v2, v2norm, mag},
            Ex.Assign(v2, tp(bpi)),
            Ex.Assign(v2norm, Rotate(angle(bpi), Norm(v2))),
            Ex.Assign(mag, magnitude(bpi)),
            Ex.Assign(v2norm.x, v2.x.Add(v2norm.x.Mul(mag))),
            Ex.Assign(v2norm.y, v2.y.Add(v2norm.y.Mul(mag))),
            v2norm
        );
    }
    
    #endregion

    private static TExV2 Box(TEx<Vector2> ex) {
        return new TExV2(ex);
    }

    #region Wrappers
    
    /// <summary>
    /// Get the BPI position.
    /// </summary>
    /// <returns></returns>
    public static ExTP Loc() {
        return bpi => bpi.loc;
    }

    /// <summary>
    /// Derive a parametric equation from two parametric-float functions.
    /// </summary>
    /// <param name="x">Parametric-float function assigned to X-component</param>
    /// <param name="y">Parametric-float function assigned to Y-component</param>
    /// <returns></returns>
    public static ExTP PXY(ExBPY x, ExBPY y) => t => ExUtils.V2(x(t), y(t));
    public static ExTP PX(ExBPY x) => PXY(x, _ => E0);
    public static ExTP PY(ExBPY y) => PXY(_ => E0, y);

    [Fallthrough(100, true)]
    public static ExTP TP3XY(ExTP3 xyz) {
        var v3 = TExV3.Variable();
        return bpi => Ex.Block(new ParameterExpression[] {v3},
            Ex.Assign(v3, xyz(bpi)),
            ExUtils.V2(v3.x, v3.y)
        );
    }

    #endregion

    #region Homing
    
    
    /// <summary>
    /// Home towards a target location at a fixed speed.
    /// </summary>
    /// <remarks>
    /// Use with StopSampling to home for only a few seconds.
    /// <para>This is primarily for use with non-rotational velocity. 
    /// Rotational use creates: contracting spirals (0,90), circle around player [90], expanding spiral (90,180).</para>
    /// </remarks>
    /// <param name="speed">Speed</param>
    /// <param name="location">Target location</param>
    /// <returns></returns>
    public static ExTP VHome(ExBPY speed, ExTP location) {
        TExV2 l = new TExV2();
        return bpi => Ex.Block(new ParameterExpression[] {l},
            Ex.Assign(l, location(bpi).Sub(bpi.loc)),
            l.Mul(Ex.Divide(speed(bpi), Sqrt(Ex.Add(SqrMag(l), EPS))))
        );
    }

    /// <summary>
    /// Home towards a target location at a speed such that the target will be reached in <paramref name="time"/> seconds. Do not set <paramref name="time"/> to 0. Best used with stopsampling 0.
    /// </summary>
    /// <param name="time">Time in seconds</param>
    /// <param name="location">Target location</param>
    /// <returns></returns>
    public static ExTP VHomeTime(ExBPY time, ExTP location) => bpi => location(bpi).Sub(bpi.loc).Div(time(bpi));
    
    /// <summary>
    /// Short for `ss0 vhometime TIME LOCATION`.
    /// </summary>
    /// <returns></returns>
    public static ExTP SSVHomeT(ExBPY time, ExTP location) => SS0(VHomeTime(time, location));

    /// <summary>
    /// Short for `eased EASE TIME ss0 vhometime TIME LOCATION`.
    /// <br/>Note: EaseToTarget with NROFFSET is preferred. It has the same signature and avoids Riemann errors.
    /// </summary>
    public static ExTP EaseDVHomeT(string ease, float time, ExTP location) => EaseD(ease, time, SS0(VHomeTime(_ => ExC(time), location)));

    /// <summary>
    /// Short for `* smooth / t TIME ss0 - LOCATION loc`. Use with NROFFSET.
    /// </summary>
    public static ExTP EaseToTarget(string ease, ExBPY time, ExTP location) => bpi =>
        ExM.Mul(Smooth(ease, Div(bpi.t, time(bpi))), SS0(x => Sub(location(x), x.loc))(bpi));
        
    
    #endregion
    
    
    #region Rotators

    /// <summary>
    /// Rotate a Cartesian parametric function by the firing angle of the entity.
    /// <br/>`tprot p = tpnrot rotify p`
    /// </summary>
    /// <param name="p">Target parametric</param>
    /// <returns></returns>
    public static ExTP Rotify(ExTP p) =>
        bpi => RotateCS(BPYRepo.AC()(bpi), BPYRepo.AS()(bpi), p(bpi));


    #endregion
    
    /// <summary>
    /// If the input time is less than the reference time, evaluate the invokee. Otherwise, return the last returned evaluation.
    /// <para>You can call this with zero sampling time, and it will sample the invokee once. However, in this case SS0 is preferred.</para>
    /// </summary>
    /// <param name="time">Time at which to stop sampling</param>
    /// <param name="p">Target parametric</param>
    /// <returns></returns>
    public static ExTP StopSampling(float time, ExTP p) {
        Ex data = DataHoisting.GetClearableDictV2();
        return bpi => ExUtils.DictIfCondSetElseGet(data, Ex.OrElse(Ex.LessThan(bpi.t, ExC(time)),
            Ex.Not(ExUtils.DictContains<uint, Vector2>(data, bpi.id))), bpi.id, p(bpi));
    }
    /// <summary>
    /// Samples an invokee exactly once.
    /// </summary>
    /// <param name="p">Target parametric</param>
    /// <returns></returns>
    public static ExTP SS0(ExTP p) {
        Ex data = DataHoisting.GetClearableDictV2();
        return bpi => ExUtils.DictIfCondSetElseGet(data, Ex.Not(ExUtils.DictContains<uint, Vector2>(data, bpi.id)), bpi.id, p(bpi));
    }

    
    #region RotateLerp
    /* TO UNDERSTAND THE DIFFERENCE BETWEEN ROTATELERP AND TRUE-ROTATELERP, CONSIDER THESE TWO PATTERNS:
     * 
            true-rotatelerprate 40 0.5 0
                switch 3
                    constant 0 2
                    constant 2 0
     * 
            rotatelerprate 40 constant 0.5 0
                switch 3
                    constant 0 2
                    constant 2 0
     * 
     * WHEN T=3, THE TRUE LERP WILL SLOWLY ROTATE BACK TO ITS ORIGINAL DIRECTION, BUT THE FALSE LERP WILL "SNAP"
     * BACK. THIS IS BECAUSE THE TARGET ExTP IS 0 DEG FROM THE SOURCE ExTP, AND THE FALSE LERP REFERENCES THIS DIFFERENCE,
     * WHILE THE TRUE LERP REFERENCES THE DIFFERENCE BETWEEN THE LAST FRAME RESULT AND THE TARGET ExTP (90 DEG)
     */

    /// <summary>
    /// Rotate between two parametrics (the magnitude of the resulting vector is the magnitude of the <paramref name="from"/> vector), closing <paramref name="ratio"/> fraction of the gap per second.
    /// </summary>
    /// <remarks>
    /// In many cases, TrueRotateLerp is more accurate. If you're experiencing strange behavior, try those functions instead.
    /// </remarks>
    /// <param name="ratio">Fraction of gap to close per second</param>
    /// <param name="from">Source parametric</param>
    /// <param name="to">Target parametric</param>
    /// <returns></returns>
    public static ExTP RotateLerpPercent(float ratio, ExTP from, ExTP to) {
        return bpi => ExMHelpers.RotateLerp(to(bpi), from(bpi), bpi, false, false, ratio);
    }

    /// <summary>
    /// Rotate between two parametrics (the magnitude of the resulting vector is the magnitude of the <paramref name="from"/> vector), closing <paramref name="rate"/> degrees of the gap per second.
    /// </summary>
    /// <remarks>
    /// In many cases, TrueRotateLerp is more accurate. If you're experiencing strange behavior, try those functions instead.
    /// </remarks>
    /// <param name="rate">Degrees of gap to close per second</param>
    /// <param name="from">Source parametric</param>
    /// <param name="to">Target parametric</param>
    /// <returns></returns>
    public static ExTP RotateLerpRate(float rate, ExTP from, ExTP to) {
        return bpi => ExMHelpers.RotateLerp(to(bpi), from(bpi), bpi, true, false, rate);
    }
    
    /// <summary>
    /// Rotate between two parametrics (the magnitude of the resulting vector is the magnitude of the <paramref name="from"/> vector), closing <paramref name="ratio"/> fraction of the gap per second.
    /// <para>This function uses the last returned value as its rotation source, only sampling the source parametric once.</para>
    /// </summary>
    /// <remarks>
    /// Ratio is not multiplicative as in RotateLerpPercent. Instead, it accumulates like the function 1-e^-rt.
    /// This means that the rotation is faster during the first second than the second second, and so on.
    /// Use TrueRotateLerpRate for constant rotation rates.
    /// </remarks>
    /// <param name="ratio">Fraction of gap to close per second</param>
    /// <param name="from">Source parametric</param>
    /// <param name="to">Target parametric</param>
    /// <returns></returns>
    public static ExTP TrueRotateLerpPercent(float ratio, ExTP from, ExTP to) {
        return bpi => ExMHelpers.RotateLerp(to(bpi), from(bpi), bpi, false, true, ratio);
    }

    /// <summary>
    /// Rotate between two parametrics (the magnitude of the resulting vector is the magnitude of the <paramref name="from"/> vector), closing <paramref name="rate"/> degrees of the gap per second.
    /// <para>This function uses the last returned value as its rotation source, only sampling the source parametric once.</para>
    /// </summary>
    /// <param name="rate">Degrees of gap to close per second</param>
    /// <param name="from">Source parametric</param>
    /// <param name="to">Target parametric</param>
    /// <returns></returns>
    public static ExTP TrueRotateLerpRate(float rate, ExTP from, ExTP to) {
        return bpi => ExMHelpers.RotateLerp(to(bpi), from(bpi), bpi, true, true, rate);
    }

    #endregion
    

    #region Switchers

    /// <summary>
    /// If the predicate is true, return the first expression, otherwise the second expression.
    /// </summary>
    /// <param name="pred">Predicate to evaluate over the input</param>
    /// <param name="iftrue">Function to eval if predicate is true</param>
    /// <param name="iffalse">Function to eval if predicate is false</param>
    /// <returns></returns>
    public static ExTP If(ExPred pred, ExTP iftrue, ExTP iffalse) {
        return bpi => Ex.Condition(pred(bpi), iftrue(bpi), iffalse(bpi));
    }
    
    /// <summary>
    /// Switch between two parametrics based on time.
    /// </summary>
    /// <param name="at_time">Switch reference</param>
    /// <param name="from">Parametric to return before switch</param>
    /// <param name="to">Parametric to return after switch</param>
    /// <returns></returns>
    public static ExTP Switch(float at_time, ExTP from, ExTP to) {
        return bpi => Ex.Condition(Ex.GreaterThan(bpi.t, ExC(at_time)), to(bpi), from(bpi));
    }

    /// <summary>
    /// Switch between two parametrics based on firing index.
    /// </summary>
    /// <param name="at_index">Switch reference</param>
    /// <param name="from">Parametric to return before switch</param>
    /// <param name="to">Parametric to return after switch</param>
    /// <returns></returns>
    public static ExTP PSwitch(float at_index, ExTP from, ExTP to) {
        return bpi => Ex.Condition(Ex.GreaterThan(bpi.findex, ExC(at_index)), to(bpi), from(bpi));
    }

    /// <summary>
    /// Switch between functions based on time, and subtract the switch reference from the time
    /// passed to the second function.
    /// </summary>
    /// <param name="at_time">Switch reference</param>
    /// <param name="from">Parametric to return before switch</param>
    /// <param name="to">Parametric to return after switch</param>
    /// <returns></returns>
    public static ExTP SwitchH(float at_time, ExTP from, ExTP to) {
        Ex d = ExC(at_time);
        var copy = new TExPI();
        return bpi => Ex.Condition(Ex.GreaterThan(bpi.t, d),
            Ex.Block(new ParameterExpression[] {copy},
                Ex.Assign(copy, bpi),
                ExUtils.AddAssign(copy.t, Ex.Negate(d)),
                to(copy)
            ), from(bpi));
    }
    
    /// <summary>
    /// Switch between two functions such that the second function continues where the first left off.
    /// Eg. Switching from the polar equation (r=1, th=90t) to the xy equation (x=t,y=0) yields:
    /// t = 3 -> (0, -1)
    /// t = 4 -> (1, -1)
    /// t = 5 -> (2, -1)
    /// </summary>
    /// <param name="pivotVar">The variable upon which pivoting is performed. Should be either "p" (firing index) or "t" (time).</param>
    /// <param name="pivot">The value of the variable at which pivoting is performed</param>
    /// <param name="f1">Starting equation</param>
    /// <param name="f2">Equation after pivot</param>
    /// <returns></returns>
    public static ExTP Pivot(ExBPY pivotVar, ExBPY pivot, ExTP f1, ExTP f2) => ExMHelpers.Pivot(pivot, f1, f2, pivotVar);

    #endregion
    
    /// <summary>
    /// Clamp the magnitude of a parametric equation. Do not allow the child to have zero magnitude.
    /// Not properly tested.
    /// </summary>
    /// <param name="min">Minimum</param>
    /// <param name="max">Maximum</param>
    /// <param name="tp">Target parametric</param>
    /// <returns></returns>
    public static ExTP ClampMag(float min, float max, ExTP tp) {
        var f = ExUtils.VFloat();
        var v = TExV2.Variable();
        var exmin = ExC(min);
        var exmax = ExC(max);
        return bpi => Ex.Block(new[] {v, f},
            Ex.Assign(v, tp(bpi)),
            Ex.Assign(f, Mag(v)),
            Ex.Condition(Ex.LessThan(f, exmin),
                Ex.Multiply(v, Ex.Divide(exmin, f)),
                Ex.Condition(Ex.GreaterThan(f, exmax),
                    Ex.Multiply(v, Ex.Divide(exmax, f)),
                    v))
        );
    }

    /// <summary>
    /// Clamp the X-component of a parametric equation.
    /// </summary>
    /// <param name="min">Minimum</param>
    /// <param name="max">Maximum</param>
    /// <param name="tp">Target parametric</param>
    /// <returns></returns>
    public static ExTP ClampX(float min, float max, ExTP tp) {
        var v = TExV2.Variable();
        var exmin = ExC(min);
        var exmax = ExC(max);
        return bpi => Ex.Block(new ParameterExpression[] {v},
            Ex.Assign(v, tp(bpi)),
            Ex.IfThenElse(Ex.LessThan(v.x, exmin), 
                Ex.Assign(v.x, exmin),
                Ex.IfThen(Ex.GreaterThan(v.x, exmax), Ex.Assign(v.x, exmax))),
            v
        );
    }

    /// <summary>
    /// Clamp the Y-component of a parametric equation.
    /// </summary>
    /// <param name="min">Minimum</param>
    /// <param name="max">Maximum</param>
    /// <param name="tp">Target parametric</param>
    /// <returns></returns>
    public static ExTP ClampY(float min, float max, ExTP tp) {
        var v = TExV2.Variable();
        var exmin = ExC(min);
        var exmax = ExC(max);
        return bpi => Ex.Block(new ParameterExpression[] {v},
            Ex.Assign(v, tp(bpi)),
            Ex.IfThenElse(Ex.LessThan(v.y, exmin), 
                Ex.Assign(v.y, exmin),
                Ex.IfThen(Ex.GreaterThan(v.y, exmax), Ex.Assign(v.y, exmax))),
            v
        );
    }

    #region TimeLerp
    
    /// <summary>
    /// See <see cref="ExM.Lerp{T}"/>
    /// </summary>
    public static ExTP LerpT(ExBPY zeroBound, ExBPY oneBound, ExTP f1, ExTP f2) => bpi =>
        ExM.Lerp(zeroBound(bpi), oneBound(bpi), BPYRepo.T()(bpi), f1(bpi), f2(bpi));

    /// <summary>
    /// Lerp between two parametric equations.
    /// </summary>
    /// <param name="from_time">Time to begin lerping</param>
    /// <param name="end_time">Time to end lerping</param>
    /// <param name="from">Source parametric</param>
    /// <param name="to">Target parametric</param>
    /// <returns></returns>
    public static ExTP LerpFromTo(ExBPY from_time, ExBPY end_time, ExTP from, ExTP to) =>
        bpi => Lerp(from_time(bpi), end_time(bpi), bpi.t, from(bpi), to(bpi));


    /// <summary>
    /// Lerp between two parametric equations.
    /// </summary>
    /// <param name="from_t">Time to begin lerping from source to target</param>
    /// <param name="to_t">Time to end lerping from source to target</param>
    /// <param name="to_t2">Time to begin lerping from target to source</param>
    /// <param name="from_t2">Time to end lerping from target to source</param>
    /// <param name="from">Source parametric</param>
    /// <param name="to">Target parametric</param>
    /// <returns></returns>
    public static ExTP LerpFromToBack(ExBPY from_t, ExBPY to_t, ExBPY to_t2, ExBPY from_t2, ExTP from, ExTP to) => bpi
        => LerpBack(from_t(bpi), to_t(bpi), to_t2(bpi), from_t2(bpi), bpi.t, from(bpi), to(bpi));

    /// <summary>
    /// Lerp from zero to the target parametric.
    /// </summary>
    /// <param name="from_time">Time to start lerping</param>
    /// <param name="end_time">Time to end lerping</param>
    /// <param name="p">Target parametric</param>
    /// <returns></returns>
    public static ExTP LerpIn(float from_time, float end_time, ExTP p) {
        Ex etr = ExC(1f / (end_time - from_time));
        Ex ex_from = ExC(from_time);
        return bpi => Ex.Condition(Ex.LessThan(bpi.t, ex_from), v20,
            Ex.Multiply(p(bpi), Ex.Condition(Ex.GreaterThan(bpi.t, ExC(end_time)), E1,
                Ex.Multiply(etr, Ex.Subtract(bpi.t, ex_from))
            ))
        );
    }

    /// <summary>
    /// Lerp from zero to the target parametric and then back.
    /// </summary>
    /// <param name="start">Time to start lerping from zero to target</param>
    /// <param name="mid">Time to end lerping from zero to target</param>
    /// <param name="mid2">Time to start lerping from target to zero</param>
    /// <param name="end">Time to end lerping from target to zero</param>
    /// <param name="p">Target parametric</param>
    /// <returns></returns>
    public static ExTP LerpInOut(float start, float mid, float mid2, float end, ExTP p) {
        var p_in = LerpIn(start, mid, p);
        var p_out = LerpOut(mid2, end, p);
        return bpi => Ex.Condition(Ex.LessThan(bpi.t, ExC(mid2)), 
            p_in(bpi), p_out(bpi));
    }

    /// <summary>
    /// Lerp from the target parametric to zero.
    /// </summary>
    /// <param name="from_time">Time to start lerping</param>
    /// <param name="end_time">Time to end lerping</param>
    /// <param name="p">Target parametric</param>
    /// <returns></returns>
    public static ExTP LerpOut(float from_time, float end_time, ExTP p) {
        Ex etr = ExC(1f / (end_time - from_time));
        Ex ex_end = ExC(end_time);
        return bpi => Ex.Condition(Ex.GreaterThan(bpi.t, ex_end), v20,
            Ex.Multiply(p(bpi), Ex.Condition(Ex.LessThan(bpi.t, ExC(from_time)), E1,
                Ex.Multiply(etr, Ex.Subtract(ex_end, bpi.t))
            ))
        );
    }

    /// <summary>
    /// Lerp from the target parametric to zero and then back.
    /// </summary>
    /// <param name="start">Time to start lerping from target to zero</param>
    /// <param name="mid">Time to end lerping from target to zero</param>
    /// <param name="mid2">Time to start lerping from zero to target</param>
    /// <param name="end">Time to end lerping from zero to target</param>
    /// <param name="p">Target parametric</param>
    /// <returns></returns>
    public static ExTP LerpOutIn(float start, float mid, float mid2, float end, ExTP p) {
        var p_out = LerpOut(start, mid, p);
        var p_in = LerpIn(mid2, end, p);
        return bpi => Ex.Condition(Ex.LessThan(bpi.t, ExC(mid2)), 
            p_out(bpi), p_in(bpi));
    }

    #endregion

    #region GrowFuncs

    /// <summary>
    /// Grow a target parametric by increasing its X and Y components
    /// at constant additive rates multiplied by time.
    /// </summary>
    /// <remarks>
    /// At time t, returns (p(t).x + xr*t, p(t).y + yr*t).
    /// </remarks>
    /// <param name="xr">Additive rate for X-component</param>
    /// <param name="yr">Additive rate for Y-component</param>
    /// <param name="p">Target parametric</param>
    /// <returns></returns>
    public static ExTP GrowByAdd(float xr, float yr, ExTP p) {
        var v = TExV2.Variable();
        return bpi => Ex.Block(new ParameterExpression[] {v},
            Ex.Assign(v, p(bpi)),
            ExUtils.AddAssign(v.x, Ex.Multiply(ExC(xr), bpi.t)),
            ExUtils.AddAssign(v.y, Ex.Multiply(ExC(yr), bpi.t)),
            v
        );
    }

    /// <summary>
    /// Grow a target parametric by increasing its X and Y components
    /// at constant additive rates multiplied by firing index.
    /// </summary>
    /// <remarks>
    /// At time t and firing index i, returns (p(t).x + xr*i, p(t).y + yr*i).
    /// </remarks>
    /// <param name="xr">Additive rate for X-component</param>
    /// <param name="yr">Additive rate for Y-component</param>
    /// <param name="p">Target parametric</param>
    /// <returns></returns>
    public static ExTP PGrowByAdd(float xr, float yr, ExTP p) {
        var v = TExV2.Variable();
        return bpi => Ex.Block(new ParameterExpression[] {v},
            Ex.Assign(v, p(bpi)),
            ExUtils.AddAssign(v.x, Ex.Multiply(ExC(xr), bpi.findex)),
            ExUtils.AddAssign(v.y, Ex.Multiply(ExC(yr), bpi.findex)),
            v
        );
    }


    /// <summary>
    /// Grow a target parametric at a constant multiplicative rate.
    /// </summary>
    /// <remarks>
    /// At time t, returns (1+r*t)*p(t).
    /// </remarks>
    /// <param name="r">Growth rate</param>
    /// <param name="p">Target parametric</param>
    /// <returns></returns>
    public static ExTP GrowByRatio(float r, ExTP p) {
        var v = TExV2.Variable();
        return bpi => Ex.Block(new ParameterExpression[] {v},
            Ex.Assign(v, p(bpi)),
            ExUtils.MulAssign(v.x, Ex.Add(E1, Ex.Multiply(ExC(r), bpi.t))),
            ExUtils.MulAssign(v.y, Ex.Add(E1, Ex.Multiply(ExC(r), bpi.t))),
            v
        );
    }
    /// <summary>
    /// Grow a target parametric at a constant multiplicative rate, limiting the rate input.
    /// </summary>
    /// <remarks>
    /// At time t, returns (1+r*t)*p(t).
    /// </remarks>
    /// <param name="r">Growth rate</param>
    /// <param name="maxt">Max t value</param>
    /// <param name="p">Target parametric</param>
    /// <returns></returns>
    public static ExTP GrowByRatioC(float r, float maxt, ExTP p) {
        return bpi => Ex.Multiply(p(bpi), Ex.Add(E1, Ex.Multiply(ExC(r),
            Ex.Condition(Ex.GreaterThan(bpi.t, ExC(maxt)),
                ExC(maxt), bpi.t))));
    }

    #endregion

    /// <summary>
    /// Flip the X-component of the target parametric.
    /// </summary>
    /// <param name="p">Target parametric</param>
    /// <returns></returns>
    public static ExTP FlipX(ExTP p) {
        var v = TExV2.Variable();
        return bpi => Ex.Block(new ParameterExpression[] {v},
            Ex.Assign(v, p(bpi)),
            ExUtils.MulAssign(v.x, ExC(-1f)),
            v
        );
    }

    /// <summary>
    /// On every invocation, select the parametric equation with the smaller magnitude.
    /// </summary>
    /// <param name="p1">One parametric equation</param>
    /// <param name="p2">Other parametric eqution</param>
    /// <returns></returns>
    public static ExTP Min(ExTP p1, ExTP p2) {
        var v1 = TExV2.Variable();
        var v2 = TExV2.Variable();
        return bpi => Ex.Block(new ParameterExpression[] {v1, v2},
            Ex.Assign(v1, p1(bpi)),
            Ex.Assign(v2, p2(bpi)),
            Ex.Condition(Ex.LessThan(SqrMag(v1), SqrMag(v2)), v1, v2)
        );
    }

    /// <summary>
    /// On every invocation, select the parametric equation with the larger magnitude.
    /// </summary>
    /// <param name="p1">One parametric equation</param>
    /// <param name="p2">Other parametric eqution</param>
    /// <returns></returns>
    public static ExTP Max(ExTP p1, ExTP p2) {
        var v1 = TExV2.Variable();
        var v2 = TExV2.Variable();
        return bpi => Ex.Block(new ParameterExpression[] {v1, v2},
            Ex.Assign(v1, p1(bpi)),
            Ex.Assign(v2, p2(bpi)),
            Ex.Condition(Ex.LessThan(SqrMag(v1), SqrMag(v2)), v2, v1)
        );
    }


    
    //Bullet Velocity updates use t=0 to get direction, and do not move on that frame. 
    //You therefore can and should use this to set initial direction on bullet parametrics that "stop" at the beginning.
    //Generally, no entity uses t=0 for velocity (it underestimates the integral so we use midpoint instead), so it's not "incorrect" to set this in other places.
    /// <summary>
    /// Set the return value at t=0 for a target parametric.
    /// </summary>
    /// <remarks>
    /// This is generally only used to determine the direction of the firee at t=0,
    /// which is only useful for movement patterns where there is no movement at t=0.
    /// </remarks>
    /// <param name="x">X-component</param>
    /// <param name="y">Y-component</param>
    /// <param name="p">Target parametric</param>
    /// <returns></returns>
    public static ExTP SetZero(float x, float y, ExTP p) {
        return bpi => Ex.Condition(Ex.LessThan(bpi.t, EPS), ExC(new Vector2(x, y)), p(bpi));
    }
    public static ExTP SetZeroTP(ExTP z, ExTP p) {
        return bpi => Ex.Condition(Ex.LessThan(bpi.t, EPS), z(bpi), p(bpi));
    }
    
    #region Easers

    /// <summary>
    /// Apply a ease function on top of a target derivative function that uses time as a controller.
    /// Primarily used for velocity parametrics.
    /// </summary>
    /// <param name="name">Easing function name</param>
    /// <param name="maxTime">Time over which to perform easing</param>
    /// <param name="f">Target parametric (describing velocity)</param>
    /// <returns></returns>
    public static ExTP EaseD(string name, float maxTime, ExTP f) => ExMHelpers.EaseD(name, maxTime, f);

    /// <summary>
    /// Apply a ease function on top of a target function that uses time as a controller.
    /// </summary>
    /// <param name="name">Easing function name</param>
    /// <param name="maxTime">Time over which to perform easing</param>
    /// <param name="f">Target parametric (describing offset)</param>
    /// <returns></returns>
    public static ExTP Ease(string name, float maxTime, ExTP f) => ExMHelpers.Ease(name, maxTime, f);
    #endregion
    
    #region Mathy

    private static readonly ExFunction qRotate = ExUtils.Wrap<Quaternion>("op_Multiply", 
        new[] { typeof(Quaternion), typeof(Vector3)});
    /// <summary>
    /// Project a three-dimensional function onto the screen. The z-axis is mapped to IN.
    /// The rotations (degrees) are applied as a quaternion. In Unity the rotation order is ZXY.
    /// </summary>
    /// <param name="xr">Rotation of view around X-axis</param>
    /// <param name="yr">Rotation of view around Y-axis</param>
    /// <param name="zr">Rotation of view around Z-axis</param>
    /// <param name="bpx">X-component</param>
    /// <param name="bpy">Y-component</param>
    /// <param name="bpz">Z-component</param>
    /// <returns></returns>
    public static ExTP ProjectView(ExBPY xr, ExBPY yr, ExBPY zr, ExBPY bpx, ExBPY bpy, ExBPY bpz) {
        var by = TExV3.Variable();
        return bpi => Ex.Block(new ParameterExpression[] {by},
            Ex.Assign(by.x, bpx(bpi)),
            Ex.Assign(by.y, bpy(bpi)),
            Ex.Assign(by.z, bpz(bpi)),
            Ex.Assign(by, qRotate.Of(ExUtils.QuaternionEuler(xr(bpi), yr(bpi), zr(bpi)), by)),
            ExUtils.V2(by.x, by.y)
        );
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="Ts">Time for straight section</param>
    /// <param name="Tl">Time for kazari section</param>
    /// <param name="Rx">Radius of x-component of straight section</param>
    /// <param name="Ry">Radius of y-component of straight section</param>
    /// <param name="bit_alias">Alias to which will be bound 0 if drawing kazari on the right side, and 1 if on the left</param>
    /// <param name="kazari">The function to draw the decoration. Must take t=[0,1], and must have p(0)={0,0}</param>
    /// <param name="time"></param>
    /// <returns></returns>
    public static ExTP TreeFireOld(float Ts, float Tl, float Rx, float Ry, string bit_alias, ExTP kazari, ExBPY time) {
        var eTs = ExC(Ts);
        var eTp = ExC(Ts + Tl);
        var J = ExUtils.VFloat(); //period number
        var j = ExUtils.VFloat(); //time in period
        var ts = ExUtils.VFloat(); //simulated time for straight line
        var xs = ExUtils.VFloat();
        var ys = ExUtils.VFloat();
        var t = ExUtils.VFloat();
        var tp = ExUtils.VFloat(); //simulated time for kazari
        var bp = ExUtils.VFloat(); // 0 if draw kazari on right, 1 if on left
        var b = ExUtils.VFloat(); // 0 if in straight phase, 1 if in kazari
        var v2 = TExV2.Variable(); // 0 if in straight phase, 1 if in kazari
        return bpi => {
            var yeet = Ex.Block(new[] {J, j, t, ts, xs, ys, bp, b, tp, v2},
                Ex.Assign(t, time(bpi)),
                Ex.Assign(j, Mod(eTp, t)),
                Ex.Assign(J, Floor(Ex.Divide(t, eTp))),
                Ex.Assign(ts, eTs.Mul(J).Add(ExM.Min(eTs, j))),
                Ex.Assign(xs, ExC(Rx).Div(eTs).Mul(
                    //2 | ts - (t+ts % 2ts) | - ts
                    E2.Mul(Abs(eTs.Sub(
                            Mod(E2.Mul(eTs), ts.Add(eTs)
                        )))).Sub(eTs)
                )),
                Ex.Assign(ys, ExC(Ry).Div(eTs).Mul(E2).Mul(ts)),
                Ex.Assign(bp, Mod(E2, J)),
                Ex.Assign(tp, Ex.Condition(Ex.GreaterThan(j, eTs),
                    Ex.Condition(Ex.Equal(bp, E1),
                        j.Sub(eTs),
                        eTp.Sub(j)),
                    E0)),
                Ex.Assign(v2, ReflectEx.Let<TExPI, Vector2, float>(bit_alias, _ => bp, () => 
                    kazari(bpi.CopyWithT(tp.Div(Tl))), bpi)),
                ExUtils.AddAssign(v2.x, xs),
                ExUtils.AddAssign(v2.y, ys),
                v2
            );
            return yeet;
        };
    }
    
    
    /// <summary>
    /// Draw a tree-based fire. Binds the following aliases:
    /// <br/>lr: 0 if drawing on right, 1 if drawing on left
    /// <br/>tk: Time within the kazari function, in the range 0-1
    /// <br/>J: iteration number
    /// </summary>
    /// <param name="Ts">Time for straight section</param>
    /// <param name="Tl">Time for kazari section</param>
    /// <param name="Rx">Radius of x-component of straight section</param>
    /// <param name="Ry">Radius of y-component of straight section</param>
    /// <param name="kazari">The function to draw the decoration. Must have p(tk=0)={0,0}</param>
    /// <param name="time"></param>
    /// <returns></returns>
    public static ExTP TreeFire(float Ts, float Tl, float Rx, float Ry, ExTP kazari, ExBPY time) => bpi => EEx.Resolve<float>(time(bpi),
        t => {
            var eTs = ExC(Ts);
            var eTp = ExC(Ts + Tl);
            var J = ExUtils.VFloat(); //period number
            var j = ExUtils.VFloat(); //time in period
            var ts = ExUtils.VFloat(); //simulated time for straight line
            var tp = ExUtils.VFloat(); //simulated time for kazari
            var tk = ExUtils.VFloat(); //normalized simulated time for kazari
            var xs = ExUtils.VFloat();
            var ys = ExUtils.VFloat();
            var bp = ExUtils.VFloat(); // 0 if draw kazari on right, 1 if on left
            var b = ExUtils.VFloat(); // 0 if in straight phase, 1 if in kazari
            var v2 = TExV2.Variable();
            return Ex.Block(new[] {J, j, ts, tk, xs, ys, bp, b, tp, v2},
                Ex.Assign(j, Mod(eTp, t)),
                Ex.Assign(J, Floor(Ex.Divide(t, eTp))),
                Ex.Assign(ts, eTs.Mul(J).Add(ExM.Min(eTs, j))),
                Ex.Assign(xs, ExC(Rx).Div(eTs).Mul(
                    //2 | ts - (t+ts % 2ts) | - ts
                    E2.Mul(Abs(eTs.Sub(
                        Mod(E2.Mul(eTs), ts.Add(eTs)
                        )))).Sub(eTs)
                )),
                Ex.Assign(ys, ExC(Ry).Div(eTs).Mul(E2).Mul(ts)),
                Ex.Assign(bp, Mod(E2, J)),
                Ex.Assign(tp, Ex.Condition(Ex.GreaterThan(j, eTs),
                    Ex.Condition(Ex.Equal(bp, E1),
                        j.Sub(eTs),
                        eTp.Sub(j)),
                    E0)),
                tk.Is(tp.Div(Tl)),
                Ex.Assign(v2, ReflectEx.Let(new (string, Func<TExPI, TEx<float>>)[] {("tk", _ => tk), ("lr", _ => bp), 
                    ("J", _ => J)}, () => kazari(bpi), bpi)),
                ExUtils.AddAssign(v2.x, xs),
                ExUtils.AddAssign(v2.y, ys),
                v2
            );
            
            
        });

    #endregion
    
}


/// <summary>
/// A struct containing the input required for a parametric equation.
/// </summary>
public struct ParametricInfo {
    /// <summary>Random ID</summary>
    public readonly uint id;
    /// <summary>Firing index</summary>
    public readonly int index;
    /// <summary>Global position</summary>
    public Vector2 loc;
    /// <summary>Life-time (with minor adjustment)</summary>
    public float t;

    public static ParametricInfo WithRandomId(Vector2 position, int findex, float t) => new ParametricInfo(position, findex, RNG.GetUInt(), t);
    public static ParametricInfo WithRandomId(Vector2 position, int findex) => WithRandomId(position, findex, 0f);
    public ParametricInfo(Vector2 position, int findex, uint id, float t = 0) {
        loc = position;
        index = findex;
        this.id = id;
        this.t = t;
    }
    public static readonly ExFunction withRandomId = ExUtils.Wrap<ParametricInfo>("WithRandomId", new[] {typeof(Vector2), typeof(int), typeof(float)});

    public ParametricInfo Rehash() => new ParametricInfo(loc, index, RNG.Rehash(id), t);
    
    public ParametricInfo CopyWithP(int newP) => new ParametricInfo(loc, newP, id, t);
    public ParametricInfo CopyWithT(float newT) => new ParametricInfo(loc, index, id, newT);

    /// <summary>
    /// Flips the position around an X or Y axis.
    /// </summary>
    /// <param name="y">Iff true, flips Y values around an X axis. Else, flips X values around a Y axis.</param>
    /// <param name="around">Location of the axis.</param>
    public void FlipSimple(bool y, float around) {
        if (y) {
            loc.y = 2 * around - loc.y;
        } else {
            loc.x = 2 * around - loc.x;
        }
    }
}
}