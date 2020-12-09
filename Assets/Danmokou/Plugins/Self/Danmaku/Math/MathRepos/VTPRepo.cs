using System;
using System.Linq.Expressions;
using DMK.Core;
using DMK.Expressions;
using DMK.Reflection;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;
using static DMK.DMath.Functions.ExM;
using ExPred = System.Func<DMK.Expressions.TExPI, DMK.Expressions.TEx<bool>>;
using ExTP = System.Func<DMK.Expressions.TExPI, DMK.Expressions.TEx<UnityEngine.Vector2>>;
using ExTTP = System.Func<DMK.Expressions.TEx<float>, DMK.Expressions.TExPI, DMK.Expressions.TEx<UnityEngine.Vector2>>;
//ExCoordF does not necessarily return a TEx<V2>, but this is used for consistency; it is compiled to void anyways.
using ExCoordF = System.Func<DMK.Expressions.TEx<float>, DMK.Expressions.TEx<float>, DMK.Expressions.TExPI, DMK.Expressions.RTExV2, System.Func<DMK.Expressions.TEx<float>, DMK.Expressions.TEx<float>, DMK.Expressions.TEx<UnityEngine.Vector2>>, DMK.Expressions.TEx<UnityEngine.Vector2>>;
using ExVTP = System.Func<DMK.Expressions.ITExVelocity, DMK.Expressions.TEx<float>, DMK.Expressions.TExPI, DMK.Expressions.RTExV2, DMK.Expressions.TEx<UnityEngine.Vector2>>;
using ExLVTP = System.Func<DMK.Expressions.ITExVelocity, DMK.Expressions.RTEx<float>, DMK.Expressions.RTEx<float>, DMK.Expressions.TExPI, DMK.Expressions.RTExV2, DMK.Expressions.TEx<UnityEngine.Vector2>>;
using ExBPY = System.Func<DMK.Expressions.TExPI, DMK.Expressions.TEx<float>>;
using ExTBPY = System.Func<DMK.Expressions.TEx<float>, DMK.Expressions.TExPI, DMK.Expressions.TEx<float>>;
using static DMK.DMath.Functions.VTPConstructors;
using static DMK.DMath.Functions.ExMConversions;


namespace DMK.DMath.Functions {
/// <summary>
/// Repository for constructing path expressions by converting lesser computations into Cartesian coordinates
/// and applying appropriate rotation.
/// <br/>These functions should not be invoked by users; instead, use the functions in <see cref="VTPRepo" />.
/// </summary>
public static class VTPConstructors {
    //Note for expressions: since this is parented by VTP2, which is not parented,
    //you can reassign values to BPI.t, but NOT to bpi.loc.
    public static ExCoordF CartesianRot(ExTP erv) => (c, s, bpi, nrv, fxy) => {
        var v2 = new TExV2();
        return Ex.Block(new ParameterExpression[] { v2 },
            Ex.Assign(v2, erv(bpi)),
            fxy(Ex.Subtract(Ex.Multiply(c, v2.x), Ex.Multiply(s, v2.y)),
                Ex.Add(Ex.Multiply(s, v2.x), Ex.Multiply(c, v2.y)))
        );
    };

    public static CoordF CartesianRot(TP rv) => delegate(float c, float s, ParametricInfo bpi, out Vector2 nrv) {
        nrv = rv(bpi);
        bpi.t = c * nrv.x - s * nrv.y;
        nrv.y = s * nrv.x + c * nrv.y;
        nrv.x = bpi.t;
    };
    public static ExCoordF CartesianNRot(ExTP enrv) => (c, s, bpi, nrv, fxy) => Ex.Block(Ex.Assign(nrv, enrv(bpi)), fxy(nrv.x, nrv.y));

    public static CoordF CartesianNRot(TP tpnrv) => delegate(float c, float s, ParametricInfo bpi, out Vector2 nrv) {
        nrv = tpnrv(bpi);
    };
   
    public static ExCoordF Cartesian(ExTP erv, ExTP enrv) {
        var v2 = new TExV2();
        return (c, s, bpi, nrv, fxy) => Ex.Block(
            new ParameterExpression[] { v2 },
            Ex.Assign(nrv, enrv(bpi)),
            Ex.Assign(v2, erv(bpi)),
            fxy(nrv.x.Add(Ex.Subtract(Ex.Multiply(c, v2.x), Ex.Multiply(s, v2.y))),
                nrv.y.Add(Ex.Add(Ex.Multiply(s, v2.x), Ex.Multiply(c, v2.y))))
        );
    }
/*
    public static ExCoordF OffsetAsVelocity(ExCoordF vtp, float dT) {
        TExV2 v2 = TExV2.Variable();
        Ex delta = Ex.Constant(dT);
        return (c, s, bpi, nrv) => Ex.Block(new ParameterExpression[] {v2},
            vtp(c, s, bpi, nrv),
            Ex.Assign(v2, nrv),
            Ex.Assign(bpi.t, bpi.t.Sub(delta)),
            vtp(c, s, bpi, nrv),
            Ex.Assign(nrv.x, v2.x.Sub(nrv.x).Div(delta)),
            Ex.Assign(nrv.y, v2.y.Sub(nrv.y).Div(delta))
        );
    }*/
    
    public static CoordF Cartesian(TP rv, TP tpnrv) => delegate(float c, float s, ParametricInfo bpi, out Vector2 nrv) {
        nrv = tpnrv(bpi);
        bpi.loc = rv(bpi);
        nrv.x += c * bpi.loc.x - s * bpi.loc.y;
        nrv.y += s * bpi.loc.x + c * bpi.loc.y;
    };
    public static ExCoordF Polar(ExBPY r, ExBPY theta) {
        var vr = ExUtils.VFloat();
        var lookup = new TExV2();
        return (c, s, bpi, nrv, fxy) => Ex.Block(new[] { vr, lookup },
            Ex.Assign(lookup, ExM.CosSinDeg(theta(bpi))),
            Ex.Assign(vr, r(bpi)),
            fxy(Ex.Subtract(Ex.Multiply(c, lookup.x), Ex.Multiply(s, lookup.y)).Mul(vr), 
                Ex.Add(Ex.Multiply(s, lookup.x), Ex.Multiply(c, lookup.y)).Mul(vr))
        );
    }
    public static ExCoordF Polar2(ExTP radThetaDeg) {
        var rt = new TExV2();
        var lookup = new TExV2();
        return (c, s, bpi, nrv, fxy) => Ex.Block(new ParameterExpression[] { rt, lookup },
            Ex.Assign(rt, radThetaDeg(bpi)),
            Ex.Assign(lookup, ExM.CosSinDeg(rt.y)),
            fxy(Ex.Subtract(Ex.Multiply(c, lookup.x), Ex.Multiply(s, lookup.y)).Mul(rt.x), 
                Ex.Add(Ex.Multiply(s, lookup.x), Ex.Multiply(c, lookup.y)).Mul(rt.x))
        );
    }
    
    public static CoordF Polar(BPY r, BPY theta) => delegate(float c, float s, ParametricInfo bpi, out Vector2 nrv) {
        float baseRot = M.degRad * theta(bpi);
        bpi.t = r(bpi);
        nrv.x = bpi.t * Mathf.Cos(baseRot);
        nrv.y = bpi.t * Mathf.Sin(baseRot);
        bpi.t = c * nrv.x - s * nrv.y;
        nrv.y = s * nrv.x + c * nrv.y;
        nrv.x = bpi.t;
    };
    
}

/// <summary>
/// Repository for constructing path expressions by converting coordinates into movement instructions.
/// <br/>These functions should not be invoked by users; instead, use the functions in <see cref="VTPRepo" />.
/// </summary>
public static class VTPControllers {
    private static T InLetCtx<T>(ITExVelocity vel, Func<T> exec) {
        using (new ReflectEx.LetDirect("root", vel.root)) {
            using (new ReflectEx.LetDirect("a", vel.angle)) {
                using (new ReflectEx.LetDirect("ac", vel.cos)) {
                    using (new ReflectEx.LetDirect("as", vel.sin)) {
                        return exec();
                    }
                }
            }
        }
    }

    public static ExVTP Velocity(ExCoordF cf) => (vel, dt, bpi, nrv) => InLetCtx(vel, () =>
        cf(vel.cos, vel.sin, bpi, nrv, (x, y) =>
            Ex.Block(
                nrv.x.Is(vel.flipX.Mul(x).Mul(dt)),
                nrv.y.Is(vel.flipY.Mul(y).Mul(dt))
            )));
    
    public static VTP Velocity(CoordF coordF) => delegate(in Movement vel, in float dT, ParametricInfo bpi, out Vector2 nrv) {
        coordF(vel.cos_rot, vel.sin_rot, bpi, out nrv);
        nrv.x *= vel.flipX * dT;
        nrv.y *= vel.flipY * dT;
    };
    public static ExVTP Offset(ExCoordF cf) => (vel, dt, bpi, nrv) => InLetCtx(vel, () => 
        cf(vel.cos, vel.sin, bpi, nrv, (x, y) =>
            Ex.Block(
                nrv.x.Is(vel.flipX.Mul(x).Add(vel.rootX).Sub(bpi.locx)),
                nrv.y.Is(vel.flipY.Mul(y).Add(vel.rootY).Sub(bpi.locy))
            )));
    
    public static VTP Offset(CoordF coordF) => delegate(in Movement vel, in float dT, ParametricInfo bpi, out Vector2 nrv) {
        coordF(vel.cos_rot, vel.sin_rot, bpi, out nrv);
        nrv.x = nrv.x * vel.flipX + vel.rootPos.x - bpi.loc.x;
        nrv.y = nrv.y * vel.flipY + vel.rootPos.y - bpi.loc.y;
    };
}

/// <summary>
/// Repository for movement functions.
/// </summary>
public static class VTPRepo {
        [DontReflect]
        public static bool IsNone(this VTP func) => func == NoVTP;
        public static readonly ExVTP ExNoVTP = VTPControllers.Velocity(CartesianNRot(Parametrics.Zero()));
    #if NO_EXPR
        public static readonly VTP NoVTP = VTPControllers.Velocity(CartesianNRot(_ => Vector2.zero));
    #else
        public static readonly VTP NoVTP = Compilers.VTP_Force(ExNoVTP);
    #endif
        /// <summary>
        /// No movement.
        /// </summary>
        public static ExVTP Null() => ExNoVTP;

        /// <summary>
        /// Switch between path functions based on a condition.
        /// <br/>You can use this to smoothly switch from offset to velocity equations,
        /// but switchin from velocity to offset will give you strange results. 
        /// </summary>
        public static ExVTP If(ExPred cond, ExVTP ifTrue, ExVTP ifFalse) => (vel, dt, bpi, nrv) =>
            Ex.Condition(cond(bpi), ifTrue(vel, dt, bpi, nrv), ifFalse(vel, dt, bpi, nrv));
        
        /// <summary>
        /// Movement with Cartesian rotational velocity only.
        /// </summary>
        /// <param name="rv">Rotational velocity parametric</param>
        /// <returns></returns>
        [Alias("tprot")]
        public static ExVTP RVelocity(ExTP rv) => VTPControllers.Velocity(CartesianRot(rv));
        /// <summary>
        /// Movement with Cartesian nonrotational velocity only.
        /// </summary>
        /// <param name="nrv">Nonrotational velocity parametric</param>
        /// <returns></returns>
        [Alias("tpnrot")]
        public static ExVTP NRVelocity(ExTP nrv) => VTPControllers.Velocity(CartesianNRot(nrv));
        /// <summary>
        /// Movement with Cartesian rotational velocity and nonrotational velocity.
        /// </summary>
        /// <param name="rv">Rotational velocity parametric</param>
        /// <param name="nrv">Nonrotational velocity parametric</param>
        /// <returns></returns>
        [Alias("tp")]
        public static ExVTP Velocity(ExTP rv, ExTP nrv) => VTPControllers.Velocity(Cartesian(rv, nrv));
        /// <summary>
        /// Movement with Cartesian rotational offset only.
        /// </summary>
        /// <param name="rp">Rotational offset parametric</param>
        /// <returns></returns>
        public static ExVTP ROffset(ExTP rp) => VTPControllers.Offset(CartesianRot(rp));
        /// <summary>
        /// Movement with Cartesian nonrotational offset only.
        /// </summary>
        /// <param name="nrp">Nonrotational offset parametric</param>
        /// <returns></returns>
        public static ExVTP NROffset(ExTP nrp) => VTPControllers.Offset(CartesianNRot(nrp));
        /// <summary>
        /// Movement with Cartesian rotational offset and nonrotational offset.
        /// </summary>
        /// <param name="rp">Rotational offset parametric</param>
        /// <param name="nrp">Nonrotational offset parametric</param>
        /// <returns></returns>
        public static ExVTP Offset(ExTP rp, ExTP nrp) => VTPControllers.Offset(Cartesian(rp, nrp));

        /// <summary>
        /// Offset function for dependent (empty-guided) fires.
        /// Reduces to `offset (* RADIUS (@ HOISTDIR p)) (@ HOISTLOC p)`
        /// </summary>
        /// <param name="hoistLoc">Location of empty guider</param>
        /// <param name="hoistDir">Direction of empty guider</param>
        /// <param name="indexer">Indexer function for public hoisting</param>
        /// <param name="radius">Radial offset of guided</param>
        /// <returns></returns>
        public static ExVTP DOffset(ReflectEx.Hoist<Vector2> hoistLoc, ReflectEx.Hoist<Vector2> hoistDir, 
            ExBPY indexer, ExBPY radius) => Offset(
            bpi => Mul(radius(bpi), RetrieveHoisted(hoistDir, indexer(bpi))),
            bpi => RetrieveHoisted(hoistLoc, indexer(bpi))
        );
        /// <summary>
        /// Offset function for dependent (empty-guided) fires.
        /// Reduces to `offset (rotatev (@ HOISTDIR p) OFFSET) (@ HOISTLOC p)`
        /// </summary>
        /// <param name="hoistLoc">Location of empty guider</param>
        /// <param name="hoistDir">Direction of empty guider</param>
        /// <param name="indexer">Indexer function for public hoisting</param>
        /// <param name="offset">Parametric offset of guided</param>
        /// <returns></returns>
        public static ExVTP DTPOffset(ReflectEx.Hoist<Vector2> hoistLoc, ReflectEx.Hoist<Vector2> hoistDir, 
            ExBPY indexer, ExTP offset) => Offset(
            bpi => RotateV(RetrieveHoisted(hoistDir, indexer(bpi)), offset(bpi)),
            bpi => RetrieveHoisted(hoistLoc, indexer(bpi))
        );
        
        /// <summary>
        /// Movement with polar rotational offset.
        /// </summary>
        /// <param name="radius">Radius function</param>
        /// <param name="theta">Theta function (degrees)</param>
        /// <returns></returns>
        public static ExVTP Polar(ExBPY radius, ExBPY theta) => VTPControllers.Offset(VTPConstructors.Polar(radius, theta));
        /// <summary>
        /// Movement with polar rotational offset. Uses a vector2 instead of two floats. (This is slower.)
        /// </summary>
        /// <param name="rt">Radius function (X), Theta function (Y) (degrees)</param>
        /// <returns></returns>
        public static ExVTP Polar2(ExTP rt) => VTPControllers.Offset(VTPConstructors.Polar2(rt));
        /// <summary>
        /// Movement with polar rotational velocity.
        /// <br/>Note: I'm pretty sure this doesn't work at all.
        /// </summary>
        /// <param name="radius">Radius derivative function</param>
        /// <param name="theta">Theta derivative function (degrees)</param>
        /// <returns></returns>
        public static ExVTP VPolar(ExBPY radius, ExBPY theta) => VTPControllers.Velocity(VTPConstructors.Polar(radius, theta));

        private static ExVTP WrapLet<T>((string, Func<TExPI, TEx<T>>)[] aliases, ExVTP inner) => 
            (v,t,bpi,nrv) => ReflectEx.Let(aliases, () => inner(v,t,bpi,nrv), bpi);
        [Alias("::")]
        public static ExVTP LetFloats((string, ExBPY)[] aliases, ExVTP inner) => WrapLet(aliases, inner);
        [Alias("::v2")]
        public static ExVTP LetV2s((string, ExTP)[] aliases, ExVTP inner) => WrapLet(aliases, inner);

        public static ExVTP LetDecl(ReflectEx.Alias<TExPI>[] aliases, ExVTP inner) => (c, s, bpi, nrv) =>
            ReflectEx.Let2(aliases, () => inner(c, s, bpi, nrv), bpi);

}

}