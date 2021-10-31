using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Linq.Expressions;
using BagoumLib.Expressions;
using Danmokou.Core;
using Danmokou.Expressions;
using JetBrains.Annotations;
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
using static Danmokou.DMath.Functions.ExMConversions;

namespace Danmokou.DMath.Functions {
/// <summary>
/// Functions that return V3.
/// </summary>
[Reflect]
public static partial class ExMV3 {
    /// <summary>
    /// Derive a Vector3 from an XY (Vector2) and a Z-component.
    /// </summary>
    public static tv3 WithZ(ev2 xy, tfloat z) => EEx.ResolveV2(xy, v => ExUtils.V3(v.x, v.y, z));
    /// <summary>
    /// Derive a Vector3 from three floats.
    /// </summary>
    /// <param name="x">Float assigned to X-component</param>
    /// <param name="y">Float assigned to Y-component</param>
    /// <param name="z">Float assigned to Z-component</param>
    /// <returns></returns>
    public static tv3 PXYZ(tfloat x, tfloat y, tfloat z) => ExUtils.V3(x, y, z);
    /// <summary>
    /// Derive a Vector3 from two floats.
    /// </summary>
    /// <param name="x">Float assigned to X-component</param>
    /// <param name="y">Float assigned to Y-component</param>
    /// <returns></returns>
    public static tv3 PXY(tfloat x, tfloat y) => ExUtils.V3(x, y, E0);
    /// <summary>
    /// Derive a Vector3 from one floats.
    /// </summary>
    /// <param name="x">Float assigned to X-component</param>
    /// <returns></returns>
    public static tv3 PX(tfloat x) => ExUtils.V3(x, E0, E0);
    /// <summary>
    /// Derive a Vector3 from one float.
    /// </summary>
    /// <param name="y">Float assigned to Y-component</param>
    /// <returns></returns>
    public static tv3 PY(tfloat y) => ExUtils.V3(E0, y, E0);
    /// <summary>
    /// Derive a Vector3 from one float.
    /// </summary>
    /// <param name="z">Float assigned to Z-component</param>
    /// <returns></returns>
    public static tv3 PZ(tfloat z) => ExUtils.V3(E0, E0, z);
    /// <summary>
    /// Derive a Vector3 from a Vector2 (Z is set to zero)
    /// </summary>
    /// <param name="tp">Parametric function to assign to x,y components</param>
    /// <returns></returns>
    public static tv3 TP3(tv2 tp) => ((Ex) tp).As<Vector3>();
    /// <summary>
    /// The zero vector.
    /// </summary>
    public static tv3 Zero() => PXYZ(E0, E0, E0);

    /// <summary>
    /// Rotate a Vector3 by a quaternion. In Unity the rotation order is ZXY.
    /// The z-axis is mapped to IN.
    /// </summary>
    /// <param name="rotateBy">Quaternion rotation, in degrees, xyz</param>
    /// <param name="target">Target Vector3</param>
    /// <returns></returns>
    public static tv3 QRotate(tv3 rotateBy, tv3 target) => ExMHelpers.QRotate(QuaternionEuler(rotateBy), target);

    /// <summary>
    /// Rotate a v3 by a direction vector.
    /// </summary>
    /// <param name="rotateBy">Direction vector (Normalization not required)</param>
    /// <param name="target">Target v3</param>
    /// <returns></returns>
    public static tv3 V3Rotate(tv3 rotateBy, tv3 target) => EEx.ResolveV2(ToSphere(rotateBy), r =>
        QRotate(PZ(r.x), QRotate(PY(r.y), target)));

    /// <summary>
    /// Create a cylindrical equation along the Y-axis. 
    /// </summary>
    /// <param name="period">Period of rotation in the XZ-plane</param>
    /// <param name="radius">Radius of function</param>
    /// <param name="time">Time of rotation (t=0 -> X-axis)</param>
    /// <param name="h">Height (X-axis)</param>
    /// <returns></returns>
    public static tv3 XZrY(efloat period, efloat radius, efloat time, tfloat h) => EEx.Resolve(period, radius, time,
        (p, r, t) =>
            V3(Cosine(p, r, t), h, Sine(p, r, t)));
    
    /// <summary>
    /// Create a cylindrical equation along the X-axis. 
    /// </summary>
    /// <param name="period">Period of rotation in the YZ-plane</param>
    /// <param name="radius">Radius of function</param>
    /// <param name="time">Time of rotation (t=0 -> Y-axis)</param>
    /// <param name="h">Height (X-axis)</param>
    /// <returns></returns>
    public static tv3 YZrX(efloat period, efloat radius, efloat time, tfloat h) => EEx.Resolve(period, radius, time,
        (p, r, t) =>
            V3(h, Cosine(p, r, t), Sine(p, r, t)));
    
    /// <summary>
    /// Create a cylindrical equation along the Z-axis. 
    /// </summary>
    /// <param name="period">Period of rotation in the XY-plane</param>
    /// <param name="radius">Radius of function</param>
    /// <param name="time">Time of rotation (t=0 -> X-axis)</param>
    /// <param name="h">Height (Z-axis)</param>
    /// <returns></returns>
    public static tv3 XYrZ(efloat period, efloat radius, efloat time, tfloat h) => EEx.Resolve(period, radius, time,
        (p, r, t) =>
            V3(Cosine(p, r, t), Sine(p, r, t), h));

    /// <summary>
    /// Multiply the x-component of a parametric equation by a function of input.
    /// </summary>
    /// <param name="f">Function of input</param>
    /// <param name="tp">Parametric equation</param>
    /// <returns></returns>
    public static tv3 MultiplyX(tfloat f, ev3 tp) => EEx.ResolveV3(tp, v => Ex.Block(
        MulAssign(v.x, f),
        v
    ));
    /// <summary>
    /// Multiply the y-component of a parametric equation by a function of input.
    /// </summary>
    /// <param name="f">Function of input</param>
    /// <param name="tp">Parametric equation</param>
    /// <returns></returns>
    public static tv3 MultiplyY(tfloat f, ev3 tp) => EEx.ResolveV3(tp, v => Ex.Block(
        MulAssign(v.y, f),
        v
    ));
    /// <summary>
    /// Multiply the z-component of a parametric equation by a function of input.
    /// </summary>
    /// <param name="f">Function of input</param>
    /// <param name="tp">Parametric equation</param>
    /// <returns></returns>
    public static tv3 MultiplyZ(tfloat f, ev3 tp)=> EEx.ResolveV3(tp, v => Ex.Block(
        MulAssign(v.z, f),
        v
    ));


    /// <summary>
    /// Wrap a position equation around a cylinder.
    /// </summary>
    /// <param name="radius">Radius of the cylinder</param>
    /// <param name="ang0">Starting angle offset (radians) on the cylinder. 0 = z-axis</param>
    /// <param name="maxWrap">Maximum angle value (radians) of the wrap. After this, the function will continue along the tangent. Starting offset not included. Absolute value tested.</param>
    /// <param name="axisOff">Offset angle (radians) of the axis of the cylinder from the y-axis</param>
    /// <param name="position">Position equation</param>
    /// <returns></returns>
    public static tv3 CylinderWrap(efloat radius, efloat ang0, efloat maxWrap, efloat axisOff, ev2 position) =>
        EEx.Resolve(radius, ang0, axisOff, position, (r, a0, axis, _v2) => {
            var cs = new TExV2();
            var xyd = new TExV2();
            var v2 = new TExV2(_v2);
            var v3 = new TExV3();
            var a = ExUtils.VFloat();
            var aRem = ExUtils.VFloat();
            var aMax = ExUtils.VFloat();
            var a_cs = new TExV2();
            var a0_cs = new TExV2();
            return Ex.Block(new[] {v3, cs, xyd, a, aRem, aMax, a_cs, a0_cs},
                aMax.Is(maxWrap),
                cs.Is(CosSin(axis)),
                xyd.Is(ConvertBasis(v2, cs)),
                a.Is(xyd.x.Div(r)),
                aRem.Is(E0),
                Ex.IfThen(Abs(a).GT(aMax), Ex.Block(
                        Ex.IfThen(a.LT0(), MulAssign(aMax, EN1)),
                        aRem.Is(a.Sub(aMax)),
                        a.Is(aMax)
                    )),
                a0_cs.Is(CosSin(a0)),
                a_cs.Is(CosSin(a.Add(a0))),
                xyd.x.Is(r.Mul(a_cs.y.Sub(a0_cs.y).Add(aRem.Mul(a_cs.x)))),
                v3.Is(TP3(DeconvertBasis(xyd, cs))),
                v3 .z.Is(r.Mul(a_cs.x.Sub(a0_cs.x).Sub(aRem.Mul(a_cs.y)))),
                v3
            );
    });
}
}