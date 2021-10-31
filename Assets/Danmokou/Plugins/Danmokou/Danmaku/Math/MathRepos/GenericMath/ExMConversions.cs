using System;
using System.Linq.Expressions;
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
/// See <see cref="ExM"/>. This class contains functions related to coordinate conversion.
/// </summary>
[Reflect]
public static class ExMConversions {

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
    
}
}