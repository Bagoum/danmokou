using System;
using System.Linq;
using System.Linq.Expressions;
using BagoumLib.Expressions;
using Danmokou.Core;
using Danmokou.Expressions;
using Danmokou.Reflection;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;
using static Danmokou.DMath.Functions.ExM;
//ExCoordF does not necessarily return a TEx<V2>, but this is used for consistency; it is compiled to void anyways.
using static Danmokou.DMath.Functions.VTPConstructors;
using static Danmokou.DMath.Functions.ExMConversions;
using static Danmokou.Reflection.Aliases;
using ExCoordF = System.Func<Danmokou.Expressions.TEx<float>, Danmokou.Expressions.TEx<float>, Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TExV2, System.Func<Danmokou.Expressions.TEx<float>, Danmokou.Expressions.TEx<float>, Danmokou.Expressions.TEx<UnityEngine.Vector2>>, Danmokou.Expressions.TEx<UnityEngine.Vector2>>;
using ExVTP = System.Func<Danmokou.Expressions.ITexMovement, Danmokou.Expressions.TEx<float>, Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TExV2, Danmokou.Expressions.TEx>;
using ExBPY = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<float>>;
using ExPred = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<bool>>;
using ExTP = System.Func<Danmokou.Expressions.TExArgCtx, Danmokou.Expressions.TEx<UnityEngine.Vector2>>;


namespace Danmokou.DMath.Functions {
/// <summary>
/// Repository for constructing path expressions by converting lesser computations into Cartesian coordinates
/// and applying appropriate rotation.
/// <br/>These functions should not be invoked by users; instead, use the functions in <see cref="VTPRepo" />.
/// </summary>
public static class VTPConstructors {
    public static ExCoordF CartesianRot(ExTP erv) => (c, s, bpi, nrv, fxy) => {
        var v2 = new TExV2();
        return Ex.Block(new ParameterExpression[] { v2 },
            Ex.Assign(v2, erv(bpi)),
            fxy(Ex.Subtract(Ex.Multiply(c, v2.x), Ex.Multiply(s, v2.y)),
                Ex.Add(Ex.Multiply(s, v2.x), Ex.Multiply(c, v2.y))),
            Expression.Empty()
        );
    };

    public static CoordF CartesianRot(TP rv) => delegate(float c, float s, ParametricInfo bpi, out Vector2 nrv) {
        var v2 = rv(bpi);
        nrv.x = c * v2.x - s * v2.y;
        nrv.y = s * v2.x + c * v2.y;
    };
    public static ExCoordF CartesianNRot(ExTP enrv) => (c, s, bpi, nrv, fxy) => 
        Ex.Block(
            Ex.Assign(nrv, enrv(bpi)), 
            fxy(nrv.x, nrv.y),
            Expression.Empty());

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
                nrv.y.Add(Ex.Add(Ex.Multiply(s, v2.x), Ex.Multiply(c, v2.y)))),
            Expression.Empty()
        );
    }
    
    public static CoordF Cartesian(TP rv, TP tpnrv) => delegate(float c, float s, ParametricInfo bpi, out Vector2 nrv) {
        nrv = tpnrv(bpi);
        var v2 = rv(bpi);
        nrv.x += c * v2.x - s * v2.y;
        nrv.y += s * v2.x + c * v2.y;
    };
    public static ExCoordF Polar(ExBPY r, ExBPY theta) {
        var vr = ExUtils.VFloat();
        var lookup = new TExV2();
        return (c, s, bpi, nrv, fxy) => Ex.Block(new[] { vr, lookup },
            Ex.Assign(lookup, ExM.CosSinDeg(theta(bpi))),
            Ex.Assign(vr, r(bpi)),
            fxy(Ex.Subtract(Ex.Multiply(c, lookup.x), Ex.Multiply(s, lookup.y)).Mul(vr), 
                Ex.Add(Ex.Multiply(s, lookup.x), Ex.Multiply(c, lookup.y)).Mul(vr)),
            Expression.Empty()
        );
    }
    public static ExCoordF Polar2(ExTP radThetaDeg) {
        var rt = new TExV2();
        var lookup = new TExV2();
        return (c, s, bpi, nrv, fxy) => Ex.Block(new ParameterExpression[] { rt, lookup },
            Ex.Assign(rt, radThetaDeg(bpi)),
            Ex.Assign(lookup, ExM.CosSinDeg(rt.y)),
            fxy(Ex.Subtract(Ex.Multiply(c, lookup.x), Ex.Multiply(s, lookup.y)).Mul(rt.x), 
                Ex.Add(Ex.Multiply(s, lookup.x), Ex.Multiply(c, lookup.y)).Mul(rt.x)),
            Expression.Empty()
        );
    }
    
    public static CoordF Polar(BPY r, BPY theta) => delegate(float c, float s, ParametricInfo bpi, out Vector2 nrv) {
        var cs = M.CosSinDeg(theta(bpi));
        var rad = r(bpi);
        nrv.x = rad * (c * cs.x - s * cs.y);
        nrv.y = rad * (s * cs.x + c * cs.y);
    };
    
}

/// <summary>
/// Repository for constructing path expressions by converting coordinates into movement instructions.
/// <br/>These functions should not be invoked by users; instead, use the functions in <see cref="VTPRepo" />.
/// </summary>
public static class VTPControllers {
    private static T InLetCtx<T>(ITexMovement vel, TExArgCtx tac, Func<TExArgCtx, T> exec) {
        using var root = tac.Let(MOV_ROOT_ALIAS, vel.root);
        using var ang = tac.Let(MOV_ANGLE_ALIAS, vel.angle);
        using var angc = tac.Let(MOV_COS_ALIAS, vel.cos);
        using var angs = tac.Let(MOV_SIN_ALIAS, vel.sin);
        return exec(tac);
    }

    public static ExVTP Velocity(ExCoordF cf) => (vel, dt, bpi, delta) => InLetCtx(vel, bpi, tac =>
        cf(vel.cos, vel.sin, tac, delta, (x, y) =>
            Ex.Block(
                delta.x.Is(vel.flipX.Mul(x).Mul(dt)),
                delta.y.Is(vel.flipY.Mul(y).Mul(dt))
            )));
    
    public static VTP Velocity(CoordF coordF) => delegate(ref Movement vel, in float dT, ref ParametricInfo bpi, out Vector2 delta) {
        coordF(vel.cos_rot, vel.sin_rot, bpi, out delta);
        delta.x *= vel.flipX * dT;
        delta.y *= vel.flipY * dT;
    };
    public static ExVTP Offset(ExCoordF cf) => (vel, dt, bpi, delta) => InLetCtx(vel, bpi, tac => 
        cf(vel.cos, vel.sin, tac, delta, (x, y) =>
            Ex.Block(
                delta.x.Is(vel.flipX.Mul(x).Add(vel.rootX).Sub(bpi.locx)),
                delta.y.Is(vel.flipY.Mul(y).Add(vel.rootY).Sub(bpi.locy))
            )));
    
    public static VTP Offset(CoordF coordF) => delegate(ref Movement vel, in float dT, ref ParametricInfo bpi, out Vector2 delta) {
        coordF(vel.cos_rot, vel.sin_rot, bpi, out delta);
        delta.x = delta.x * vel.flipX + vel.rootPos.x - bpi.loc.x;
        delta.y = delta.y * vel.flipY + vel.rootPos.y - bpi.loc.y;
    };
}

/// <summary>
/// Repository for movement functions.
/// </summary>
[Reflect]
public static class VTPRepo {
        [DontReflect]
        public static bool IsNone(this VTP func) => func == NoVTP;
        public static readonly ExVTP ExNoVTP = VTPControllers.Velocity(CartesianNRot(Parametrics.Zero()));
        public static readonly VTP NoVTP = delegate(ref Movement vel, in float dT, ref ParametricInfo bpi, out Vector2 nrv) {
            nrv = Vector2.zero;
        };
        /// <summary>
        /// No movement.
        /// </summary>
        public static ExVTP Null() => ExNoVTP;

        /// <summary>
        /// Switch between path functions based on a condition.
        /// <br/>You can use this to smoothly switch from offset to velocity equations,
        /// but switching from velocity to offset will give you strange results. 
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
            bpi => Mul(radius(bpi), RetrieveHoisted(hoistDir, indexer)(bpi)),
            RetrieveHoisted(hoistLoc, indexer)
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
            bpi => RotateV(RetrieveHoisted(hoistDir, indexer)(bpi), offset(bpi)),
            RetrieveHoisted(hoistLoc, indexer)
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

        private static ExVTP WrapLet<T>((string, Func<TExArgCtx, TEx<T>>)[] aliases, ExVTP inner) => 
            (v,t,bpi,nrv) => ReflectEx.Let(aliases, () => inner(v,t,bpi,nrv), bpi);
        
        /// <summary>
        /// Bind float values to the aliases and then execute the inner content with those aliases.
        /// </summary>
        [Alias("::")]
        public static ExVTP LetFloats((string, ExBPY)[] aliases, ExVTP inner) => WrapLet(aliases, inner);
        /// <summary>
        /// Bind vector2 values to the aliases and then execute the inner content with those aliases.
        /// </summary>
        [Alias("::v2")]
        public static ExVTP LetV2s((string, ExTP)[] aliases, ExVTP inner) => WrapLet(aliases, inner);
        
        /// <summary>
        /// Bind values to the aliases and then execute the inner content with those aliases.
        /// </summary>
        [Alias(":::")]
        public static ExVTP LetDecl(ReflectEx.Alias[] aliases, ExVTP inner) => (c, s, bpi, nrv) =>
            ReflectEx.Let2(aliases, () => inner(c, s, bpi, nrv), bpi);
        
        /// <summary>
        /// (C# code use) Movement with Cartesian rotational velocity only.
        /// </summary>
        /// <param name="rv">Rotational velocity parametric</param>
        /// <returns></returns>
        [DontReflect]
        public static VTP RVelocity(TP rv) => VTPControllers.Velocity(CartesianRot(rv));
        
        /// <summary>
        /// (C# code use) Movement with Cartesian nonrotational velocity only.
        /// </summary>
        /// <param name="nrv">Nonrotational velocity parametric</param>
        /// <returns></returns>
        [DontReflect]
        public static VTP NRVelocity(TP nrv) => VTPControllers.Velocity(CartesianNRot(nrv));
        
        /// <summary>
        /// (C# code use) Movement with Cartesian rotational velocity and nonrotational velocity.
        /// </summary>
        /// <param name="rv">Rotational velocity parametric</param>
        /// <param name="nrv">Nonrotational velocity parametric</param>
        /// <returns></returns>
        [DontReflect]
        public static VTP Velocity(TP rv, TP nrv) => VTPControllers.Velocity(Cartesian(rv, nrv));
        
        /// <summary>
        /// (C# code use) Movement with Cartesian rotational offset only.
        /// </summary>
        /// <param name="rp">Rotational offset parametric</param>
        /// <returns></returns>
        [DontReflect]
        public static VTP ROffset(TP rp) => VTPControllers.Offset(CartesianRot(rp));
        
        /// <summary>
        /// (C# code use) Movement with Cartesian nonrotational offset only.
        /// </summary>
        /// <param name="nrp">Nonrotational offset parametric</param>
        /// <returns></returns>
        [DontReflect]
        public static VTP NROffset(TP nrp) => VTPControllers.Offset(CartesianNRot(nrp));
        
        /// <summary>
        /// (C# code use) Movement with Cartesian rotational offset and nonrotational offset.
        /// </summary>
        /// <param name="rp">Rotational offset parametric</param>
        /// <param name="nrp">Nonrotational offset parametric</param>
        /// <returns></returns>
        [DontReflect]
        public static VTP Offset(TP rp, TP nrp) => VTPControllers.Offset(Cartesian(rp, nrp));
}

}